using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;
using VMASharp;

namespace VMASharp
{
    public sealed class VulkanMemoryPool : IDisposable
    {
        private static Vk VkApi => VulkanMemoryAllocator.VkApi;

        public VulkanMemoryAllocator Allocator { get; }

        public string Name { get; set; }

        internal uint ID { get; }

        internal readonly BlockList BlockList;

        internal VulkanMemoryPool(VulkanMemoryAllocator allocator, in AllocationPoolCreateInfo poolInfo, long preferredBlockSize)
        {
            if (allocator is null)
            {
                throw new ArgumentNullException(nameof(allocator));
            }

            Allocator = allocator;

            BlockList = new BlockList(
                allocator,
                this,
                poolInfo.MemoryTypeIndex,
                poolInfo.BlockSize != 0 ? poolInfo.BlockSize : preferredBlockSize,
                poolInfo.MinBlockCount,
                poolInfo.MaxBlockCount,
                (poolInfo.Flags & PoolCreateFlags.IgnoreBufferImageGranularity) != 0 ? 1 : allocator.BufferImageGranularity,
                poolInfo.FrameInUseCount,
                poolInfo.BlockSize != 0,
                (poolInfo.Flags & (PoolCreateFlags.BuddyAlgorithm | PoolCreateFlags.LinearAlgorithm)) == PoolCreateFlags.LinearAlgorithm);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public uint MakeAllocationsLost()
        {
            throw new NotImplementedException();
        }

        public Result CheckForCorruption()
        {
            throw new NotImplementedException();
        }


    }
}
