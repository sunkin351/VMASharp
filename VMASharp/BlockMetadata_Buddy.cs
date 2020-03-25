using Silk.NET.Vulkan;
using System;
using VMASharp;

namespace VMASharp
{
    internal sealed class BlockMetadata_Buddy : BlockMetadata
    {
        const long MinNodeSize = 32;
        const long MaxLevels = 30;

        private long usableSize;
        private int levelCount;

        private Node root;

        private (Node, Node)[] freeList = new (Node, Node)[MaxLevels];

        private int allocationCount;

        private int freeCount;

        private long sumFreeSize;

        public BlockMetadata_Buddy(VulkanMemoryAllocator allocator) : base(allocator)
        {
        }

        public override int AllocationCount => throw new NotImplementedException();

        public override long SumFreeSize => this.sumFreeSize + this.UnusableSize;

        public override long UnusedRangeSizeMax => throw new NotImplementedException();

        public override bool IsEmpty => this.root.Type == NodeType.Free;

        private long UnusableSize => this.Size - this.usableSize;


        public override void AddPoolStats(ref PoolStats stats)
        {
            throw new NotImplementedException();
        }


        public override void CalcAllocationStatInfo(out StatInfo outInfo)
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

        public override void Validate()
        {
            throw new NotImplementedException();
        }

        private void DeleteNode(Node node)
        {
            throw new NotImplementedException();
        }

        private bool ValidateNode(ref ValidationContext ctx, Node parent, Node curr, uint level, ulong levelNodeSize)
        {
            throw new NotImplementedException();
        }

        private uint AllocSizeToLevel(long allocSize)
        {
            throw new NotImplementedException();
        }

        private long LevelToNodeSize(int level)
        {
            return this.usableSize >> level;
        }

        private void FreeAtOffset(Allocation allocation, ulong offset)
        {
            throw new NotImplementedException();
        }

        private void CalcAllocationStatInfoNode(ref StatInfo outInfo, Node node, ulong levelNodeSize)
        {
            throw new NotImplementedException();
        }

        private void AddToFreeListFront(uint level, Node node)
        {
            throw new NotImplementedException();
        }

        private void RemoveFromFreeList(uint level, Node node)
        {
            throw new NotImplementedException();
        }

        public override bool CreateAllocationRequest(int currentFrame, int frameInUseCount, long bufferImageGranularity, long allocSize, long allocAlignment, bool upperAddress, SuballocationType allocType, bool canMakeOtherLost, uint strategy, out AllocationRequest request)
        {
            throw new NotImplementedException();
        }

        public override bool MakeRequestedAllocationsLost(int currentFrame, int frameInUseCount, ref AllocationRequest request)
        {
            throw new NotImplementedException();
        }

        public override int MakeAllocationsLost(int currentFrame, int frameInUseCount)
        {
            throw new NotImplementedException();
        }

        public override void CheckCorruption(IntPtr blockData)
        {
            throw new NotImplementedException();
        }

        public override void Alloc(in AllocationRequest request, SuballocationType type, long allocSize, BlockAllocation allocation)
        {
            throw new NotImplementedException();
        }

        private struct ValidationContext
        {
            public ulong CalculatedAllocationCount, CalculatedFreeCount, CalculatedSumFreeSize;
        }

        private enum NodeType
        {
            Free,
            Allocation,
            Split,
            Count
        }

        private class Node
        {
            public ulong Offset;
            public NodeType Type;
            public Node Parent, Buddy;
            public Node Prev, Next;
            public Allocation Allocation;
            public Node LeftChild;
        }


    }
}
