using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

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

            this.Allocator = allocator;

            ref int tmpRef = ref Unsafe.As<uint, int>(ref allocator.NextPoolID);

            this.ID = (uint)Interlocked.Increment(ref tmpRef);

            if (this.ID == 0)
                throw new OverflowException();

            this.BlockList = new BlockList(
                allocator,
                this,
                poolInfo.MemoryTypeIndex,
                poolInfo.BlockSize != 0 ? poolInfo.BlockSize : preferredBlockSize,
                poolInfo.MinBlockCount,
                poolInfo.MaxBlockCount,
                (poolInfo.Flags & PoolCreateFlags.IgnoreBufferImageGranularity) != 0 ? 1 : allocator.BufferImageGranularity,
                poolInfo.FrameInUseCount,
                poolInfo.BlockSize != 0,
                poolInfo.AllocationAlgorithmCreate ?? Helpers.DefaultMetaObjectCreate);

            this.BlockList.CreateMinBlocks();
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
