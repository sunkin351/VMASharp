using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace VMASharp
{
    public unsafe abstract class Allocation : IDisposable
    {
        protected static Vk VkApi => VulkanMemoryAllocator.VkApi;

        private long alignment;
        private long size;
        private object userData;
        private int lastUseFrameIndex;
        private uint memoryTypeIndex;
        private SuballocationType suballocationType;
        protected int mapCount;
        private object allocation;
        private bool LostOrDisposed = false;

        /// <summary>
        /// Memory type index that this allocation is from. Value does not change.
        /// </summary>
        public uint MemoryTypeIndex { get => memoryTypeIndex; }

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

        internal Allocation(VulkanMemoryAllocator allocator, int currentFrameIndex, uint memTypeIndex)
        {
            Allocator = allocator;
            memoryTypeIndex = memTypeIndex;
        }


        public abstract IntPtr MappedData { get; }

        public object UserData { get; set; }

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
            if (allocationLocalOffset >= Size)
            {
                throw new ArgumentOutOfRangeException(nameof(allocationLocalOffset));
            }

            BindBufferMemoryInfo info = new BindBufferMemoryInfo
            {
                SType = StructureType.BindBufferMemoryInfoKhr,
                PNext = pNext.ToPointer(),
                Buffer = buffer,
                Memory = this.Memory,
                MemoryOffset = (ulong)(allocationLocalOffset + this.Offset)
            };

            return VkApi.BindBufferMemory2(this.Allocator.Device, 1, &info);
        }

        public abstract Result BindImageMemory(Image image, long allocationLocalOffset = 0, IntPtr pNext = default);

        internal bool MakeLost(int currentFrame, int frameInUseCount)
        {
            if (!this.CanBecomeLost)
            {
                throw new InvalidOperationException();
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
    }
}
