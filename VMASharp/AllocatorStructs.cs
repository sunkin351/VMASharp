using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;

namespace VMASharp
{
    public struct VulkanAllocatorCreateInfo
    {
        /// <summary>
        /// Flags for created allocator
        /// </summary>
        public AllocatorCreateFlags Flags;

        public Instance Instance;

        public PhysicalDevice PhysicalDevice;

        public Device LogicalDevice;

        public long PreferredLargeHeapBlockSize;

        public int FrameInUseCount;

        public long[] HeapSizeLimits;

        public Version32 VulkanAPIVersion;
    }

    public struct AllocationCreateInfo
    {
        public AllocationCreateFlags Flags;

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

    }

    internal struct AllocationRequest
    {
        public const long LostAllocationCost = 1048576;

        public long Offset, SumFreeSize, SumItemSize;

        public LinkedListNode<Suballocation> Item;

        public long ItemsToMakeLostCount;
        public object CustomData;
        public AllocationRequestType Type;
        
        public readonly long CalcCost()
        {
            return SumItemSize + ItemsToMakeLostCount * LostAllocationCost;
        }
    }
}
