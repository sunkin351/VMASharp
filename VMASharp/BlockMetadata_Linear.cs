using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Text;
using VMASharp;

namespace VMASharp
{
    internal sealed class BlockMetadata_Linear : BlockMetadata
    {
        private long sumFreeSize;
        private readonly List<Suballocation> suballocations0, suballocations1;
        private uint firstVectorIndex;
        private SecondVectorMode secondVectorMode;

        private ulong firstNullItemsBeginCount, firstNullItemsMiddleCount, secondNullItemsCount;

        private List<Suballocation> SuballocationsFirst => firstVectorIndex != 0 ? suballocations1 : suballocations0;
        private List<Suballocation> SuballocationsSecond => firstVectorIndex != 0 ? suballocations0 : suballocations1;

        public BlockMetadata_Linear(VulkanMemoryAllocator allocator) : base(allocator)
        {
            suballocations0 = new List<Suballocation>();
            suballocations1 = new List<Suballocation>();
        }

        public override int AllocationCount => throw new NotImplementedException();

        public override long SumFreeSize => this.sumFreeSize;

        public override long UnusedRangeSizeMax => throw new NotImplementedException();

        public override bool IsEmpty => this.AllocationCount == 0;

        public override void AddPoolStats(ref PoolStats stats)
        {
            throw new NotImplementedException();
        }

        public override Allocation Alloc(in AllocationRequest request, SuballocationType type, long allocSize)
        {
            throw new NotImplementedException();
        }

        public override void CalcAllocationStatInfo(out StatInfo outInfo)
        {
            throw new NotImplementedException();
        }

        public override void CheckCorruption(IntPtr blockData)
        {
            throw new NotImplementedException();
        }

        public override bool CreateAllocationRequest(int currentFrame, int frameInUseCount,
            long bufferImageGranularity, long allocSize, long allocAlignment, bool upperAddress,
            SuballocationType allocType, bool canMakeOtherLost, uint strategy,
            out AllocationRequest request)
        {
            throw new NotImplementedException();
        }

        public override void Free(Allocation allocation)
        {
            throw new NotImplementedException();
        }

        public override void FreeAtOffset(long offset)
        {
            throw new NotImplementedException();
        }

        public override int MakeAllocationsLost(int currentFrame, int frameInUseCount)
        {
            throw new NotImplementedException();
        }

        public override bool MakeRequestedAllocationsLost(int currentFrame, int frameInUseCount, ref AllocationRequest request)
        {
            throw new NotImplementedException();
        }

        public override void Validate()
        {
            throw new NotImplementedException();
        }

        //Private members
        private enum SecondVectorMode
        {
            Empty,
            RingBuffer,
            DoubleStack
        }

        private bool ShouldCompactFirst()
        {
            throw new NotImplementedException();
        }

        private void CleanupAfterFree()
        {

        }

        private bool CreateAllocationRequest_LowerAddress(uint currentframe, uint frameInUseCount, ulong bufferImageGranularity,
            ulong allocSize, ulong allocAlignment, SuballocationType allocType, bool CanMakeOthersLost,
            AllocationCreateFlags strategy, out AllocationRequest request)
        {
            throw new NotImplementedException();
        }

        private bool CreateAllocationRequest_UpperAddress(uint currentframe, uint frameInUseCount, ulong bufferImageGranularity,
            ulong allocSize, ulong allocAlignment, SuballocationType allocType, bool CanMakeOthersLost,
            AllocationCreateFlags strategy, out AllocationRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
