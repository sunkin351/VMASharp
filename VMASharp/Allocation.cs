#pragma warning disable CA1063

using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

#nullable enable

namespace VMASharp
{
    public unsafe abstract class Allocation : IDisposable
    {
        internal VulkanMemoryAllocator Allocator { get; }

        protected Vk VkApi => Allocator.VkApi;

        protected long size;
        protected long alignment;
        private int lastUseFrameIndex;
        protected int memoryTypeIndex;
        protected int mapCount;
        private bool LostOrDisposed = false;

        /// <summary>
        /// Size of this allocation, in bytes.
        /// Value never changes, unless allocation is lost.
        /// </summary>
        public long Size
        {
            get
            {
                if (LostOrDisposed || lastUseFrameIndex == Helpers.FrameIndexLost)
                {
                    return 0;
                }

                return size;
            }
        }

        /// <summary>
        /// Memory type index that this allocation is from. Value does not change.
        /// </summary>
        public int MemoryTypeIndex { get => memoryTypeIndex; }

        /// <summary>
        /// Handle to Vulkan memory object.
        /// Same memory object can be shared by multiple allocations.
        /// It can change after call to vmaDefragment() if this allocation is passed to the function, or if allocation is lost.
        /// If the allocation is lost, it is equal to `VK_NULL_HANDLE`.
        /// </summary>
        public abstract DeviceMemory Memory { get; }

        /// <summary>
        /// Offset into deviceMemory object to the beginning of this allocation, in bytes. (deviceMemory, offset) pair is unique to this allocation.
        /// It can change after call to vmaDefragment() if this allocation is passed to the function, or if allocation is lost.
        /// </summary>
        public abstract long Offset { get; internal set; }

        internal abstract bool CanBecomeLost { get; }


        internal bool IsPersistantMapped
        {
            get => this.mapCount < 0;
        }

        internal int LastUseFrameIndex
        {
            get
            {
                return this.lastUseFrameIndex;
            }
        }

        internal long Alignment => this.alignment;

        public object? UserData { get; set; }

        internal Allocation(VulkanMemoryAllocator allocator, int currentFrameIndex)
        {
            this.Allocator = allocator;
            this.lastUseFrameIndex = currentFrameIndex;
        }


        public abstract IntPtr MappedData { get; }

        public void Dispose()
        {
            if (!this.LostOrDisposed)
            {
                this.Allocator.FreeMemory(this);
                LostOrDisposed = true;
            }
        }

        public Result BindBufferMemory(Buffer buffer)
        {
            Debug.Assert(this.Offset >= 0);

            return this.Allocator.BindVulkanBuffer(buffer, this.Memory, this.Offset, null);
        }

        public unsafe Result BindBufferMemory(Buffer buffer, long allocationLocalOffset, IntPtr pNext)
        {
            return this.BindBufferMemory(buffer, allocationLocalOffset, (void*)pNext);
        }

        public unsafe Result BindBufferMemory(Buffer buffer, long allocationLocalOffset, void* pNext = null)
        {
            if ((ulong)allocationLocalOffset >= (ulong)this.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(allocationLocalOffset));
            }

            return this.Allocator.BindVulkanBuffer(buffer, this.Memory, this.Offset + allocationLocalOffset, pNext);
        }

        public unsafe Result BindImageMemory(Image image)
        {
            return this.Allocator.BindVulkanImage(image, this.Memory, this.Offset, null);
        }

        public unsafe Result BindImageMemory(Image image, long allocationLocalOffset, IntPtr pNext)
        {
            return this.BindImageMemory(image, allocationLocalOffset, (void*)pNext);
        }

        public unsafe Result BindImageMemory(Image image, long allocationLocalOffset, void* pNext = null)
        {
            if ((ulong)allocationLocalOffset >= (ulong)this.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(allocationLocalOffset));
            }

            return this.Allocator.BindVulkanImage(image, this.Memory, this.Offset + allocationLocalOffset, pNext);
        }

        internal bool MakeLost(int currentFrame, int frameInUseCount)
        {
            if (!this.CanBecomeLost)
            {
                throw new InvalidOperationException("Internal Exception, tried to make an allocation lost that cannot become lost.");
            }

            int localLastUseFrameIndex = this.lastUseFrameIndex;

            while (true)
            {
                if (localLastUseFrameIndex == Helpers.FrameIndexLost)
                {
                    Debug.Assert(false);
                    return false;
                }
                else if (localLastUseFrameIndex + frameInUseCount >= currentFrame)
                {
                    return false;
                }
                else
                {
                    var tmp = Interlocked.CompareExchange(ref this.lastUseFrameIndex, Helpers.FrameIndexLost, localLastUseFrameIndex);

                    if (tmp == localLastUseFrameIndex)
                    {
                        this.LostOrDisposed = true;
                        return true;
                    }

                    localLastUseFrameIndex = tmp;
                }
            }
        }

        public bool TouchAllocation()
        {
            if (this.LostOrDisposed)
            {
                return false;
            }

            int currFrameIndexLoc = this.Allocator.CurrentFrameIndex;
            int lastUseFrameIndexLoc = this.lastUseFrameIndex;

            if (this.CanBecomeLost)
            {
                while (true)
                {
                    if (lastUseFrameIndexLoc == Helpers.FrameIndexLost)
                    {
                        return false;
                    }
                    else if (lastUseFrameIndexLoc == currFrameIndexLoc)
                    {
                        return true;
                    }

                    lastUseFrameIndexLoc = Interlocked.CompareExchange(ref this.lastUseFrameIndex, currFrameIndexLoc, lastUseFrameIndexLoc);
                }
            }
            else
            {
                while (true)
                {
                    Debug.Assert(lastUseFrameIndexLoc != Helpers.FrameIndexLost);

                    if (lastUseFrameIndexLoc == currFrameIndexLoc)
                        break;

                    lastUseFrameIndexLoc = Interlocked.CompareExchange(ref this.lastUseFrameIndex, currFrameIndexLoc, lastUseFrameIndexLoc);
                }

                return true;
            }
        }

        public void Flush(long offset, long size)
        {
            Allocator.FlushOrInvalidateAllocation(this, offset, size, CacheOperation.Flush);
        }

        public void Invalidate(long offset, long size)
        {
            Allocator.FlushOrInvalidateAllocation(this, offset, size, CacheOperation.Invalidate);
        }

        public abstract IntPtr Map();

        public abstract void Unmap();
    }
}
