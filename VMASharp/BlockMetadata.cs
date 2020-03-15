using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using Silk.NET.Vulkan;
using VMASharp;

namespace VMASharp
{
    internal abstract class BlockMetadata
    {
        protected static Vk VkApi => VulkanMemoryAllocator.VkApi;


        public long Size { get; private set; }

        protected BlockMetadata(VulkanMemoryAllocator allocator)
        {

        }

        public virtual void Init(long size)
        {
            Size = size;
        }

        [Conditional("DEBUG")]
        public abstract void Validate();

        public abstract int AllocationCount { get; }

        public abstract long SumFreeSize { get; }

        public abstract long UnusedRangeSizeMax { get; }

        public abstract bool IsEmpty { get; }

        public abstract void CalcAllocationStatInfo(out StatInfo outInfo);

        public abstract void AddPoolStats(ref PoolStats stats);

        public abstract bool CreateAllocationRequest(
            int currentFrame, int frameInUseCount, long bufferImageGranularity, long allocSize, long allocAlignment,
            bool upperAddress, SuballocationType allocType, bool canMakeOtherLost, uint strategy,
            out AllocationRequest request);

        public abstract bool MakeRequestedAllocationsLost(int currentFrame, int frameInUseCount, ref AllocationRequest request);

        public abstract int MakeAllocationsLost(int currentFrame, int frameInUseCount);

        public abstract Result CheckCorruption(IntPtr blockData);

        public abstract Allocation Alloc(in AllocationRequest request, SuballocationType type, long allocSize);

        public abstract void Free(Allocation allocation);

        public abstract void FreeAtOffset(long offset);
    }
}
