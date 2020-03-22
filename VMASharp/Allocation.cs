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
        protected static Vk VkApi => VulkanMemoryAllocator.VkApi;

        private long alignment;
        private long size;
        private object? userData;
        private int lastUseFrameIndex;
        private int memoryTypeIndex;
        private SuballocationType suballocationType;
        protected int mapCount;
        private bool LostOrDisposed = false;

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

        /// <summary>
        /// Size of this allocation, in bytes.
        /// Value never changes, unless allocation is lost.
        /// </summary>
        public long Size { get; internal set; }

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

        internal Allocation(VulkanMemoryAllocator allocator, int memTypeIndex, long size)
        {
            this.Allocator = allocator;
            this.memoryTypeIndex = memTypeIndex;
            this.Size = size;
        }


        public abstract IntPtr MappedData { get; }

        public object? UserData { get; set; }

        internal VulkanMemoryAllocator Allocator { get; }

        public void Dispose()
        {
            if (!this.LostOrDisposed)
            {
                this.Allocator.FreeMemory(this);
            }
        }

        public Result BindBufferMemory(Buffer buffer)
        {
            Debug.Assert(this.Offset >= 0);
            return VkApi.BindBufferMemory(this.Allocator.Device, buffer, this.Memory, (ulong)this.Offset);
        }

        public unsafe Result BindBufferMemory2(Buffer buffer, long allocationLocalOffset, IntPtr pNext)
        {
            return BindBufferMemory2(buffer, allocationLocalOffset, (void*)pNext);
        }

        public unsafe Result BindBufferMemory2(Buffer buffer, long allocationLocalOffset, void* pNext)
        {
            if ((ulong)allocationLocalOffset >= (ulong)this.Size)
            {
                throw new ArgumentOutOfRangeException(nameof(allocationLocalOffset));
            }

            BindBufferMemoryInfo info = new BindBufferMemoryInfo
            {
                SType = StructureType.BindBufferMemoryInfo,
                PNext = pNext,
                Buffer = buffer,
                Memory = this.Memory,
                MemoryOffset = (ulong)(allocationLocalOffset + this.Offset)
            };

            if (this.Allocator.VulkanAPIVersion >= Helpers.VulkanAPIVersion_1_1)
            {
                return VkApi.BindBufferMemory2(this.Allocator.Device, 1, &info);
            }
            else if (this.Allocator.BindMemory2 != null)
            {
                return this.Allocator.BindMemory2.BindBufferMemory2(this.Allocator.Device, 1, &info);
            }
            else
            {
                throw new InvalidOperationException("VK_KHR_bind_memory2 not specified or not found");
            }
        }

        public abstract Result BindImageMemory(Image image, long allocationLocalOffset = 0, IntPtr pNext = default);

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

        }

        public void Invalidate(long offset, long size)
        {

        }
    }
}
