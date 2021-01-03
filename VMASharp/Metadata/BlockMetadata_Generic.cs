using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using VMASharp;

#nullable enable

namespace VMASharp.Metadata
{
    internal sealed class BlockMetadata_Generic : BlockMetadata
    {
        private int freeCount;

        private long sumFreeSize;

        private readonly LinkedList<Suballocation> suballocations = new LinkedList<Suballocation>();

        private readonly List<LinkedListNode<Suballocation>> freeSuballocationsBySize = new List<LinkedListNode<Suballocation>>();

        public override int AllocationCount => suballocations.Count - freeCount;

        public override long SumFreeSize => sumFreeSize;

        public override long UnusedRangeSizeMax
        {
            get
            {
                var count = this.freeSuballocationsBySize.Count;

                if (count != 0)
                {
                    return this.freeSuballocationsBySize[count - 1].Value.Size;
                }

                return 0;
            }
        }

        public override bool IsEmpty => (this.suballocations.Count == 1) && (this.freeCount == 1);

        public BlockMetadata_Generic(long blockSize) : base(blockSize)
        {
            this.freeCount = 1;
            this.sumFreeSize = blockSize;

            Suballocation suballoc = new Suballocation()
            {
                Offset = 0,
                Size = blockSize,
                Type = SuballocationType.Free
            };

            Debug.Assert(blockSize > Helpers.MinFreeSuballocationSizeToRegister);

            var node = this.suballocations.AddLast(suballoc);

            this.freeSuballocationsBySize.Add(node);
        }

        public override void Alloc(in AllocationRequest request, SuballocationType type, long allocSize, BlockAllocation allocation)
        {
            Debug.Assert(request.Type == AllocationRequestType.Normal);
            Debug.Assert(request.Item != null);

            if (!(request.Item is LinkedListNode<Suballocation> requestNode))
            {
                throw new InvalidOperationException();
            }

            Debug.Assert(object.ReferenceEquals(requestNode.List, this.suballocations));

            Suballocation suballoc = requestNode.Value;

            Debug.Assert(suballoc.Type == SuballocationType.Free);
            Debug.Assert(request.Offset >= suballoc.Offset);

            long paddingBegin = request.Offset - suballoc.Offset;

            Debug.Assert(suballoc.Size >= paddingBegin + allocSize);

            long paddingEnd = suballoc.Size - paddingBegin - allocSize;

            UnregisterFreeSuballocation(requestNode);

            suballoc.Offset = request.Offset;
            suballoc.Size = allocSize;
            suballoc.Type = type;
            suballoc.Allocation = allocation;

            if (paddingEnd > 0)
            {
                Suballocation paddingSuballoc = new Suballocation()
                {
                    Offset = request.Offset + allocSize,
                    Size = paddingEnd,
                    Type = SuballocationType.Free
                };

                var newNode = this.suballocations.AddAfter(requestNode, paddingSuballoc);
                RegisterFreeSuballocation(newNode);
            }

            if (paddingBegin > 0)
            {
                Suballocation paddingSuballoc = new Suballocation()
                {
                    Offset = request.Offset - paddingBegin,
                    Size = paddingBegin,
                    Type = SuballocationType.Free
                };

                var newNode = this.suballocations.AddBefore(requestNode, paddingSuballoc);
                RegisterFreeSuballocation(newNode);
            }

            if (paddingBegin > 0)
            { 
                if (paddingEnd > 0)
                {
                    this.freeCount += 1;
                }
            }
            else if (paddingEnd <= 0)
            {
                this.freeCount -= 1;
            }

            this.sumFreeSize -= allocSize;
        }

        public override void CheckCorruption(IntPtr blockData)
        {
            throw new NotImplementedException();
        }

        public override bool TryCreateAllocationRequest(in AllocationContext context, out AllocationRequest request)
        {
            request = default;

            request.Type = AllocationRequestType.Normal;

            if (context.CanMakeOtherLost == false && this.sumFreeSize < context.AllocationSize + 2 * Helpers.DebugMargin)
            {
                return false;
            }

            var contextCopy = context;
            contextCopy.CanMakeOtherLost = false;

            int freeSuballocCount = this.freeSuballocationsBySize.Count;
            if (freeSuballocCount > 0)
            {
                if (context.Strategy == AllocationStrategyFlags.BestFit)
                {
                    var allocSize = context.AllocationSize;
                    var index = this.freeSuballocationsBySize.FindIndex(node => node.Value.Size >= allocSize + 2 * Helpers.DebugMargin);

                    for (; index < freeSuballocCount; ++index)
                    {
                        var suballocNode = this.freeSuballocationsBySize[index];

                        if (this.CheckAllocation(in contextCopy, suballocNode, ref request))
                        {
                            request.Item = suballocNode;
                            return true;
                        }
                    }
                }
                else if (context.Strategy == Helpers.InternalAllocationStrategy_MinOffset)
                {
                    for (var node = this.suballocations.First; node != null; node = node.Next)
                    {
                        if (node.Value.Type == SuballocationType.Free
                            && this.CheckAllocation(in contextCopy, node, ref request))
                        {
                            request.Item = node;
                            return true;
                        }
                    }
                }
                else //Worst Fit, First Fit
                {
                    for (int i = freeSuballocCount; i >= 0; --i)
                    {
                        var item = this.freeSuballocationsBySize[i];

                        if (this.CheckAllocation(in contextCopy, item, ref request))
                        {
                            request.Item = item;
                            return true;
                        }
                    }
                }
            }

            if (context.CanMakeOtherLost)
            {
                bool found = false;
                AllocationRequest tmpRequest = default;

                for (LinkedListNode<Suballocation>? tNode = this.suballocations.First; tNode != null; tNode = tNode.Next)
                {
                    if (this.CheckAllocation(in context, tNode, ref tmpRequest))
                    {
                        if (context.Strategy == AllocationStrategyFlags.FirstFit)
                        {
                            request = tmpRequest;
                            request.Item = tNode;
                            break;
                        }

                        if (!found || tmpRequest.CalcCost() < request.CalcCost())
                        {
                            request = tmpRequest;
                            request.Item = tNode;
                            found = true;
                        }
                    }
                }

                return found;
            }

            return false;
        }

        public override void Free(Allocation allocation)
        {
            for (LinkedListNode<Suballocation>? node = this.suballocations.First; node != null; node = node.Next)
            {
                var suballoc = node.Value;

                if (object.ReferenceEquals(suballoc.Allocation, allocation))
                {
                    this.FreeSuballocation(node);
                    return;
                }
            }

            throw new InvalidOperationException("Allocation not found!");
        }

        public override void FreeAtOffset(long offset)
        {
            for (LinkedListNode<Suballocation>? node = this.suballocations.First; node != null; node = node.Next)
            {
                var suballoc = node.Value;

                if (suballoc.Offset == offset)
                {
                    this.FreeSuballocation(node);
                    return;
                }
            }

            throw new InvalidOperationException("Allocation not found!");
        }

        public override int MakeAllocationsLost(int currentFrame, int frameInUseCount)
        {
            int lost = 0;

            for (var node = this.suballocations.First; node != null; node = node.Next)
            {
                var value = node.Value;
                if (value.Type != SuballocationType.Free &&
                    value.Allocation.CanBecomeLost &&
                    value.Allocation.MakeLost(currentFrame, frameInUseCount))
                {
                    node = FreeSuballocation(node);
                    lost += 1;
                }
            }

            return lost;
        }

        public override bool MakeRequestedAllocationsLost(int currentFrame, int frameInUseCount, ref AllocationRequest request)
        {
            if (request.Type != AllocationRequestType.Normal)
            {
                throw new ArgumentException("Allocation Request Type was not normal");
            }

            LinkedListNode<Suballocation>? tNode = request.Item as LinkedListNode<Suballocation> ?? throw new InvalidOperationException();

            while (request.ItemsToMakeLostCount > 0)
            {
                if (tNode.Value.Type == SuballocationType.Free)
                {
                    tNode = tNode.Next;
                }

                Debug.Assert(tNode != null);
                Debug.Assert(tNode.Value.Allocation != null);
                Debug.Assert(tNode.Value.Allocation.CanBecomeLost);

                if (tNode.Value.Allocation.MakeLost(currentFrame, frameInUseCount))
                {
                    request.Item = tNode = FreeSuballocation(tNode);
                    request.ItemsToMakeLostCount -= 1;
                }
                else
                {
                    return false;
                }
            }

            Debug.Assert(request.Item != null);
            Debug.Assert(Unsafe.As<LinkedListNode<Suballocation>>(request.Item).Value.Type == SuballocationType.Free);

            return true;
        }

        public override void Validate()
        {
            Helpers.Validate(this.suballocations.Count > 0);

            long calculatedOffset = 0, calculatedSumFreeSize = 0;
            int calculatedFreeCount = 0, freeSuballocationsToRegister = 0;

            bool prevFree = false;

            foreach (Suballocation subAlloc in this.suballocations)
            {
                Helpers.Validate(subAlloc.Offset == calculatedOffset);

                bool currFree = subAlloc.Type == SuballocationType.Free;

                if (currFree)
                {
                    Helpers.Validate(!prevFree);
                    Helpers.Validate(subAlloc.Allocation == null);

                    calculatedSumFreeSize += subAlloc.Size;
                    calculatedFreeCount += 1;

                    if (subAlloc.Size >= Helpers.MinFreeSuballocationSizeToRegister)
                    {
                        freeSuballocationsToRegister += 1;
                    }

                    Helpers.Validate(subAlloc.Size >= Helpers.DebugMargin);
                }
                else
                {
                    Helpers.Validate(subAlloc.Allocation != null);
                    Helpers.Validate(subAlloc.Allocation!.Offset == subAlloc.Offset);
                    Helpers.Validate(subAlloc.Allocation.Size == subAlloc.Size);
                    Helpers.Validate(Helpers.DebugMargin == 0 || prevFree);
                }

                calculatedOffset += subAlloc.Size;
                prevFree = currFree;
            }

            Helpers.Validate(this.freeSuballocationsBySize.Count == freeSuballocationsToRegister);
            
            this.ValidateFreeSuballocationList();

            Helpers.Validate(calculatedOffset == this.Size);
            Helpers.Validate(calculatedSumFreeSize == this.sumFreeSize);
            Helpers.Validate(calculatedFreeCount == this.freeCount);
        }
        
        [Conditional("DEBUG")]
        private void ValidateFreeSuballocationList()
        {
            long lastSize = 0;

            for (int i = 0, count = this.freeSuballocationsBySize.Count; i < count; ++i)
            {
                var node = this.freeSuballocationsBySize[i];

                Helpers.Validate(node.Value.Type == SuballocationType.Free);
                Helpers.Validate(node.Value.Size >= Helpers.MinFreeSuballocationSizeToRegister);
                Helpers.Validate(node.Value.Size >= lastSize);

                lastSize = node.Value.Size;
            }
        }

        private bool CheckAllocation(in AllocationContext context, LinkedListNode<Suballocation> node, ref AllocationRequest request)
        {
            if (context.AllocationSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(context.AllocationSize));
            }

            if (context.SuballocationType == SuballocationType.Free)
            {
                throw new ArgumentException("Invalid Allocation Type", nameof(context.SuballocationType));
            }

            if (context.BufferImageGranularity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(context.BufferImageGranularity));
            }

            request.ItemsToMakeLostCount = 0;
            request.SumFreeSize = 0;
            request.SumItemSize = 0;

            Suballocation suballocItem = node.Value, tmpSuballoc;

            if (context.CanMakeOtherLost)
            {
                if (suballocItem.Type == SuballocationType.Free)
                {
                    request.SumFreeSize = suballocItem.Size;
                }
                else
                {
                    if (suballocItem.Allocation.CanBecomeLost && suballocItem.Allocation.LastUseFrameIndex + context.FrameInUseCount < context.CurrentFrame)
                    {
                        request.ItemsToMakeLostCount += 1;
                        request.SumItemSize = suballocItem.Size;
                    }
                    else
                    {
                        return false;
                    }
                }

                if (this.Size - suballocItem.Offset < context.AllocationSize)
                {
                    return false;
                }

                var offset = (Helpers.DebugMargin > 0) ? suballocItem.Offset + Helpers.DebugMargin : suballocItem.Offset;

                request.Offset = Helpers.AlignUp(offset, context.AllocationAlignment);

                AccountForBackwardGranularityConflict(node, context.BufferImageGranularity, context.SuballocationType, ref request);

                if (request.Offset >= suballocItem.Offset + suballocItem.Size)
                {
                    return false;
                }

                long paddingBegin = request.Offset - suballocItem.Offset;
                long requiredEndMargin = Helpers.DebugMargin;
                long totalSize = paddingBegin + context.AllocationSize + requiredEndMargin;

                if (suballocItem.Offset + totalSize > this.Size)
                {
                    return false;
                }

                var prevNode = node;

                if (totalSize > suballocItem.Size)
                {
                    long remainingSize = totalSize - suballocItem.Size;
                    while (remainingSize > 0)
                    {
                        if (prevNode.Next == null)
                        {
                            return false;
                        }

                        prevNode = prevNode.Next;

                        tmpSuballoc = prevNode.Value;

                        if (prevNode.Value.Type == SuballocationType.Free)
                        {
                            request.SumFreeSize += prevNode.Value.Size;
                        }
                        else
                        {
                            Debug.Assert(prevNode.Value.Allocation != null);

                            if (tmpSuballoc.Allocation.CanBecomeLost && tmpSuballoc.Allocation.LastUseFrameIndex + context.FrameInUseCount < context.CurrentFrame)
                            {
                                request.ItemsToMakeLostCount += 1;

                                request.SumItemSize += tmpSuballoc.Size;
                            }
                            else
                            {
                                return false;
                            }
                        }

                        remainingSize = (tmpSuballoc.Size < remainingSize) ? remainingSize - tmpSuballoc.Size : 0;
                    }
                }

                if (context.BufferImageGranularity > 1)
                {
                    var nextNode = prevNode.Next;

                    while (nextNode != null)
                    {
                        Suballocation nextItem = nextNode.Value;

                        if (Helpers.BlocksOnSamePage(request.Offset, context.AllocationSize, nextItem.Offset, context.BufferImageGranularity))
                        {
                            if (Helpers.IsBufferImageGranularityConflict(context.SuballocationType, nextItem.Type))
                            {
                                Debug.Assert(nextItem.Allocation != null);

                                if (nextItem.Allocation.CanBecomeLost && nextItem.Allocation.LastUseFrameIndex + context.FrameInUseCount < context.CurrentFrame)
                                {
                                    request.ItemsToMakeLostCount += 1;
                                }
                                else
                                {
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            break;
                        }

                        nextNode = nextNode.Next;
                    }
                }
            }
            else
            {
                request.SumFreeSize = suballocItem.Size;

                if (suballocItem.Size < context.AllocationSize)
                {
                    return false;
                }

                var offset = suballocItem.Offset;

                if (Helpers.DebugMargin > 0)
                {
                    offset += Helpers.DebugMargin;
                }

                request.Offset = Helpers.AlignUp(offset, context.AllocationAlignment);

                AccountForBackwardGranularityConflict(node, context.BufferImageGranularity, context.SuballocationType, ref request);

                long paddingBegin = request.Offset - suballocItem.Offset, requiredEndMargin = Helpers.DebugMargin;
                
                if (paddingBegin + context.AllocationSize + requiredEndMargin > suballocItem.Size)
                {
                    return false;
                }

                if (context.BufferImageGranularity > 1)
                {
                    var nextNode = node.Next;

                    while (nextNode != null)
                    {
                        var nextItem = nextNode.Value;

                        if (Helpers.BlocksOnSamePage(request.Offset, context.AllocationSize, nextItem.Offset, context.BufferImageGranularity))
                        {
                            if (Helpers.IsBufferImageGranularityConflict(context.SuballocationType, nextItem.Type))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            break;
                        }

                        nextNode = nextNode.Next;
                    }
                }
            }

            return true;

            static void AccountForBackwardGranularityConflict(LinkedListNode<Suballocation> node, long granularity, SuballocationType suballocType, ref AllocationRequest request)
            {
                if (granularity == 1)
                {
                    return;
                }

                LinkedListNode<Suballocation> prevNode = node;

                while (prevNode.Previous != null)
                {
                    prevNode = prevNode.Previous;

                    var prevAlloc = prevNode.Value;

                    if (Helpers.BlocksOnSamePage(prevAlloc.Offset, prevAlloc.Size, request.Offset, granularity))
                    {
                        if (Helpers.IsBufferImageGranularityConflict(prevAlloc.Type, suballocType))
                        {
                            request.Offset = Helpers.AlignUp(request.Offset, granularity);
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void MergeFreeWithNext(LinkedListNode<Suballocation> node)
        {
            Debug.Assert(node != null);
            Debug.Assert(object.ReferenceEquals(node.List, this.suballocations));
            Debug.Assert(node.Value.Type == SuballocationType.Free);

            var nextNode = node.Next;

            Debug.Assert(nextNode != null);
            Debug.Assert(nextNode.Value.Type == SuballocationType.Free);

            Suballocation item = node.Value, nextItem = nextNode.Value;

            item.Size += nextItem.Size;
            this.freeCount -= 1;
            this.suballocations.Remove(nextNode);
        }

        private LinkedListNode<Suballocation> FreeSuballocation(LinkedListNode<Suballocation> item)
        {
            var suballoc = item.Value;

            suballoc.Type = SuballocationType.Free;
            suballoc.Allocation = null;

            this.freeCount += 1;
            this.sumFreeSize += suballoc.Size;

            var nextItem = item.Next;
            var prevItem = item.Previous;

            if (nextItem != null && nextItem.Value.Type == SuballocationType.Free)
            {
                UnregisterFreeSuballocation(nextItem);
                MergeFreeWithNext(item);
            }

            if (prevItem != null && prevItem.Value.Type == SuballocationType.Free)
            {
                UnregisterFreeSuballocation(prevItem);
                MergeFreeWithNext(prevItem);
                RegisterFreeSuballocation(prevItem);
                return prevItem;
            }
            else
            {
                RegisterFreeSuballocation(item);
                return item;
            }
        }

        private void RegisterFreeSuballocation(LinkedListNode<Suballocation> item)
        {
            Debug.Assert(item.Value.Type == SuballocationType.Free);
            Debug.Assert(item.Value.Size > 0);

            this.ValidateFreeSuballocationList();

            if (item.Value.Size >= Helpers.MinFreeSuballocationSizeToRegister)
            {
                if (this.freeSuballocationsBySize.Count == 0)
                {
                    this.freeSuballocationsBySize.Add(item);
                }
                else
                {
                    this.freeSuballocationsBySize.InsertSorted(item, Helpers.SuballocationNodeItemSizeLess);
                }
            }

            //this.ValidateFreeSuballocationList();
        }

        private void UnregisterFreeSuballocation(LinkedListNode<Suballocation> item)
        {
            Debug.Assert(item.Value.Type == SuballocationType.Free);
            Debug.Assert(item.Value.Size > 0);

            this.ValidateFreeSuballocationList();

            if (item.Value.Size >= Helpers.MinFreeSuballocationSizeToRegister)
            {
                int index = this.freeSuballocationsBySize.BinarySearch_Leftmost(item, Helpers.SuballocationNodeItemSizeLess);

                Debug.Assert(index >= 0);

                while (index < this.freeSuballocationsBySize.Count)
                {
                    var tmp = this.freeSuballocationsBySize[index];

                    if (object.ReferenceEquals(tmp, item))
                    {
                        this.freeSuballocationsBySize.RemoveAt(index);
                        return;
                    }
                    else if (tmp.Value.Size != item.Value.Size)
                    {
                        break;
                    }

                    index += 1;
                }

                throw new InvalidOperationException("Suballocation Not Found");
            }
        }

        public override void CalcAllocationStatInfo(out StatInfo outInfo)
        {
            outInfo = default;

            outInfo.BlockCount = 1;

            int rangeCount = this.suballocations.Count;
            outInfo.AllocationCount = rangeCount - this.freeCount;
            outInfo.UnusedRangeCount = this.freeCount;

            outInfo.UnusedBytes = this.sumFreeSize;
            outInfo.UsedBytes = this.Size - outInfo.UnusedBytes;

            outInfo.AllocationSizeMin = long.MaxValue;
            outInfo.AllocationSizeMax = 0;
            outInfo.UnusedRangeSizeMin = long.MaxValue;
            outInfo.UnusedRangeSizeMax = 0;

            foreach (var item in this.suballocations)
            {
                if (item.Type != SuballocationType.Free)
                {
                    if (item.Size < outInfo.AllocationSizeMin)
                        outInfo.AllocationSizeMin = item.Size;

                    if (item.Size > outInfo.AllocationSizeMax)
                        outInfo.AllocationSizeMax = item.Size;
                }
                else
                {
                    if (item.Size < outInfo.UnusedRangeSizeMin)
                        outInfo.UnusedRangeSizeMin = item.Size;

                    if (item.Size > outInfo.UnusedRangeSizeMax)
                        outInfo.UnusedRangeSizeMax = item.Size;
                }
            }
        }

        public override void AddPoolStats(ref PoolStats stats)
        {
            int rangeCount = this.suballocations.Count;

            stats.Size += this.Size;

            stats.UnusedSize += this.sumFreeSize;

            stats.AllocationCount += rangeCount - this.freeCount;

            stats.UnusedRangeCount += this.freeCount;

            var tmp = this.UnusedRangeSizeMax;

            if (tmp > stats.UnusedRangeSizeMax)
                stats.UnusedRangeSizeMax = tmp;
        }

        public bool IsBufferImageGranularityConflictPossible(long bufferImageGranularity, ref SuballocationType type)
        {
            if (bufferImageGranularity == 1 || this.IsEmpty)
            {
                return false;
            }

            long minAlignment = long.MaxValue;
            bool typeConflict = false;

            foreach (var suballoc in this.suballocations)
            {
                SuballocationType thisType = suballoc.Type;

                if (thisType != SuballocationType.Free)
                {
                    minAlignment = Math.Min(minAlignment, suballoc.Allocation.Alignment);

                    if (Helpers.IsBufferImageGranularityConflict(type, thisType))
                    {
                        typeConflict = true;
                    }

                    type = thisType;
                }
            }

            return typeConflict || minAlignment >= bufferImageGranularity;
        }
    }
}
