using System;
using System.Diagnostics;

namespace VMASharp.Metadata
{
    /// <summary>
    /// Allocation book-keeping for individual Device Memory Blocks. 
    /// </summary>
    public abstract class BlockMetadata
    {
        protected internal long Size { get; }

        public abstract int AllocationCount { get; }

        public abstract long SumFreeSize { get; }

        public abstract long UnusedRangeSizeMax { get; }

        /// <summary>
        /// Returns true if this block is empty - contains only a single free suballocation
        /// </summary>
        public abstract bool IsEmpty { get; }

        protected BlockMetadata(long blockSize)
        {
            Size = blockSize;
        }

        /// <summary>
        /// Validates all data structures inside this object. Throws an exception if validation fails.
        /// Only called in Debug builds of VMASharp
        /// </summary>
        [Conditional("DEBUG")]
        public abstract void Validate();

        public abstract void CalcAllocationStatInfo(out StatInfo outInfo);

        /// <summary>
        /// Should not modify block Count.
        /// </summary>
        /// <param name="stats"></param>
        public abstract void AddPoolStats(ref PoolStats stats);

        /// <summary>
        /// Tries to find a place for suballocation with given parameters inside this block.
        /// If succeeded, fills pAllocationRequest and returns true.
        /// If failed, returns false.
        /// </summary>
        /// <param name="currentFrame"></param>
        /// <param name="frameInUseCount"></param>
        /// <param name="bufferImageGranularity"></param>
        /// <param name="allocSize"></param>
        /// <param name="allocAlignment"></param>
        /// <param name="upperAddress"></param>
        /// <param name="allocType"></param>
        /// <param name="canMakeOtherLost"></param>
        /// <param name="strategy">
        ///     Allocation Strategy, feel free to ignore
        /// </param>
        /// <param name="request"></param>
        /// <returns>Returns whether the method succeeds</returns>
        public abstract bool TryCreateAllocationRequest(in AllocationContext context, out AllocationRequest request);

        public abstract bool MakeRequestedAllocationsLost(int currentFrame, int frameInUseCount, ref AllocationRequest request);

        public abstract int MakeAllocationsLost(int currentFrame, int frameInUseCount);

        public abstract void CheckCorruption(IntPtr blockData);

        public abstract void Alloc(in AllocationRequest request, SuballocationType type, long allocSize, BlockAllocation allocation);

        public abstract void Free(Allocation allocation);

        public abstract void FreeAtOffset(long offset);
    }
}
