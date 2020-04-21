using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;

#nullable enable

namespace VMASharp
{
    public struct VulkanMemoryAllocatorCreateInfo
    {
        public Vk VulkanAPIObject;
        /// <summary>
        /// Flags for created allocator
        /// </summary>
        public AllocatorCreateFlags Flags;

        public Instance Instance;

        public PhysicalDevice PhysicalDevice;

        public Device LogicalDevice;

        public long PreferredLargeHeapBlockSize;

        public int FrameInUseCount;

        public long[]? HeapSizeLimits;

        public Version32 VulkanAPIVersion;

        public bool UseExtMemoryBudget;
    }

    public struct AllocationCreateInfo
    {
        public AllocationCreateFlags Flags;

        public AllocationStrategyFlags Strategy;

        public MemoryUsage Usage;

        public MemoryPropertyFlags RequiredFlags;

        public MemoryPropertyFlags PreferredFlags;

        public uint MemoryTypeBits;

        public VulkanMemoryPool Pool;

        public object UserData;
    }

    public struct AllocationPoolCreateInfo
    {
        public int MemoryTypeIndex;

        public PoolCreateFlags Flags;

        public long BlockSize;

        public int MinBlockCount;

        public int MaxBlockCount;

        public int FrameInUseCount;

        public Func<long, Metadata.BlockMetadata>? AllocationAlgorithmCreate;

    }

    public struct AllocationContext
    {
        public int CurrentFrame, FrameInUseCount;
        public long BufferImageGranularity;
        public long AllocationSize;
        public long AllocationAlignment;
        public AllocationStrategyFlags Strategy;
        public SuballocationType SuballocationType;
        public bool CanMakeOtherLost;
    }

    public struct AllocationRequest
    {
        public const long LostAllocationCost = 1048576;

        public long Offset, SumFreeSize, SumItemSize;
        public long ItemsToMakeLostCount;

        public object Item;

        public object CustomData;
        public AllocationRequestType Type;
        
        public readonly long CalcCost()
        {
            return SumItemSize + ItemsToMakeLostCount * LostAllocationCost;
        }
    }
}
