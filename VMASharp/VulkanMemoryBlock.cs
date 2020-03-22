using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;
using VMASharp;

#nullable enable

namespace VMASharp
{
    internal class VulkanMemoryBlock : IDisposable
    {
        private static Vk VkApi => VulkanMemoryAllocator.VkApi;

        private readonly VulkanMemoryAllocator Allocator;
        public BlockMetadata MetaData;
        private readonly object SyncLock = new object();
        private int mapCount;


        public VulkanMemoryBlock(VulkanMemoryAllocator allocator, VulkanMemoryPool pool, int memoryTypeIndex, DeviceMemory memory, long size, uint id, uint algorithm)
        {
            Allocator = allocator;
            ParentPool = pool;
            MemoryTypeIndex = memoryTypeIndex;
            DeviceMemory = memory;
            ID = id;

            switch (algorithm)
            {
                case (uint)PoolCreateFlags.LinearAlgorithm:
                    this.MetaData = new BlockMetadata_Linear(allocator);
                    break;
                case (uint)PoolCreateFlags.BuddyAlgorithm:
                    this.MetaData = new BlockMetadata_Buddy(allocator);
                    break;
                case 0:
                    this.MetaData = new BlockMetadata_Generic(allocator);
                    break;
                default:
                    throw new ArgumentException("Invalid algorithm passed to constructor");
            }

            this.MetaData.Init(size);
        }

        public VulkanMemoryPool? ParentPool { get; }

        public DeviceMemory DeviceMemory { get; }

        public int MemoryTypeIndex { get; }

        public uint ID { get; }

        public IntPtr MappedData { get; private set; }

        public void Dispose()
        {
            if (!this.MetaData.IsEmpty)
            {
                throw new InvalidOperationException("Some allocations were not freed before destruction of this memory block!");
            }

            Debug.Assert(this.DeviceMemory.Handle != default);

            var res = this.Allocator.FreeVulkanMemory(this.MemoryTypeIndex, this.MetaData.Size, this.DeviceMemory);

            Debug.Assert(res == Result.Success);

            if (res != Result.Success)
            {
                //TODO: Write error handling
            }
        }

        [Conditional("DEBUG")]
        public void Validate()
        {
            Debug.Assert(this.DeviceMemory.Handle != default && this.MetaData.Size > 0);

            MetaData.Validate();
        }

        public void CheckCorruption(VulkanMemoryAllocator allocator)
        {
            var data = this.Map(1);

            try
            {
                this.MetaData.CheckCorruption(data);
            }
            finally
            {
                this.Unmap(1);
            }
        }

        public unsafe IntPtr Map(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            lock (this.SyncLock)
            {
                Debug.Assert(this.mapCount >= 0);

                if (this.mapCount > 0)
                {
                    Debug.Assert(this.MappedData != default);

                    this.mapCount += count;
                    return this.MappedData;
                }
                else
                {
                    if (count == 0)
                    {
                        return default;
                    }

                    IntPtr pData;
                    var res = VkApi.MapMemory(this.Allocator.Device, this.DeviceMemory, 0, Vk.WholeSize, 0, (void**)&pData);

                    if (res != Result.Success)
                    {
                        throw new MapMemoryException(res);
                    }

                    this.mapCount = count;
                    this.MappedData = pData;

                    return pData;
                }
            }
        }

        public void Unmap(int count)
        {
            if (count == 0)
            {
                return;
            }

            lock (this.SyncLock)
            {
                int newCount = this.mapCount - count;

                if (newCount < 0)
                {
                    throw new InvalidOperationException("Memory block is being unmapped while it was not previously mapped");
                }

                this.mapCount = newCount;
                
                if (newCount == 0)
                {
                    this.MappedData = default;
                    VkApi.UnmapMemory(this.Allocator.Device, this.DeviceMemory);
                }
            }
        }

        public Result BindBufferMemory(Allocation allocation, long allocationLocalOffset, Buffer buffer, IntPtr pNext)
        {
            Debug.Assert(allocation is BlockAllocation blockAlloc && blockAlloc.Block == this);

            Debug.Assert((ulong)allocationLocalOffset < (ulong)allocation.Size, "Invalid allocationLocalOffset. Did you forget that this offset is relative to the beginning of the allocation, not the whole memory block?");

            long memoryOffset = allocationLocalOffset + allocation.Offset;

            lock (SyncLock)
            {
                return this.Allocator.BindVulkanBuffer(this.DeviceMemory, memoryOffset, buffer, pNext);
            }
        }

        public Result BindImageMemory(Allocation allocation, long allocationLocalOffset, Image image, IntPtr pNext)
        {
            Debug.Assert(allocation is BlockAllocation blockAlloc && blockAlloc.Block == this);

            Debug.Assert((ulong)allocationLocalOffset < (ulong)allocation.Size, "Invalid allocationLocalOffset. Did you forget that this offset is relative to the beginning of the allocation, not the whole memory block?");

            long memoryOffset = allocationLocalOffset + allocation.Offset;

            lock (this.SyncLock)
            {
                return this.Allocator.BindVulkanImage(this.DeviceMemory, memoryOffset, image, pNext);
            }
        }
    }
}
