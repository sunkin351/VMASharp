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

        public override void Alloc(in AllocationRequest request, SuballocationType type, ulong allocSize, Allocation allocation)
        {
            throw new NotImplementedException();
        }

        public override void CalcAllocationStatInfo(out StatInfo outInfo)
        {
            throw new NotImplementedException();
        }

        public override Result CheckCorruption(object blockData)
        {
            throw new NotImplementedException();
        }

        public override bool CreateAllocationRequest(uint currentFrame, uint frameInUseCount, ulong bufferImageGranularity, ulong allocSize, ulong allocAlignment, bool upperAddress, SuballocationType allocType, bool canMakeOtherLost, AllocationCreateFlags strategy, out AllocationRequest request)
        {
            throw new NotImplementedException();
        }

        public override void Free(Allocation allocation)
        {
            throw new NotImplementedException();
        }

        public override void FreeAtOffset(ulong offset)
        {
            throw new NotImplementedException();
        }

        public override uint MakeAllocationsLost(uint currentFrame, uint frameInUseCount)
        {
            throw new NotImplementedException();
        }

        public override bool MakeRequestedAllocationsLost(uint currentFrame, uint frameInUseCount, ref AllocationRequest request)
        {
            throw new NotImplementedException();
        }

        public override bool Validate()
        {
            throw new NotImplementedException();
        }

        private void DeleteNode(Node node)
        {

        }

        private bool ValidateNode(ref ValidationContext ctx, Node parent, Node curr, uint level, ulong levelNodeSize)
        {

        }

        private uint AllocSizeToLevel(ulong allocSize)
        {

        }

        private ulong LevelToNodeSize(int level)
        {
            return this.usableSize >> level;
        }

        private void FreeAtOffset(Allocation allocation, ulong offset)
        {

        }

        private void CalcAllocationStatInfoNode(ref StatInfo outInfo, Node node, ulong levelNodeSize)
        {

        }

        private void AddToFreeListFront(uint level, Node node)
        {

        }

        private void RemoveFromFreeList(uint level, Node node)
        {

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
