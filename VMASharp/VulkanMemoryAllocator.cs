using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Numerics;

using Buffer = Silk.NET.Vulkan.Buffer;

#nullable enable

namespace VMASharp
{
    using Defragmentation;
    using VMASharp;

    public sealed unsafe class VulkanMemoryAllocator : IDisposable
    {
        const long SmallHeapMaxSize = 1024L * 1024 * 1024;


        internal static Vk VkApi { get; private set; }

        public static void SetAPI(Vk api)
        {
            if (VkApi != null)
            {
                throw new InvalidOperationException("Tried to set API when API has already been set.");
            }

            VkApi = api ?? throw new ArgumentNullException(nameof(api));
        }

        internal readonly Device Device;
        internal readonly Instance Instance;

        internal readonly KhrBindMemory2? BindMemory2;
        internal readonly KhrGetMemoryRequirements2? MemoryRequirements2;

        internal readonly Version32 VulkanAPIVersion;

        internal bool UseKhrDedicatedAllocation;
        internal bool UseHkrBindMemory2;
        internal bool UseExtMemoryBudget;
        internal bool UseAMDDeviceCoherentMemory;

        internal uint HeapSizeLimitMask;

        internal PhysicalDeviceProperties PhysicalDeviceProperties;
        internal PhysicalDeviceMemoryProperties MemoryProperties;

        internal readonly BlockList[] BlockLists = new BlockList[Vk.MaxMemoryTypes]; //Default Pools

        internal DedicatedAllocationHandler[] DedicatedAllocations = new DedicatedAllocationHandler[Vk.MaxMemoryTypes];

        private long PreferredLargeHeapBlockSize;
        private PhysicalDevice PhysicalDevice;
        private uint CurrentFrame, GPUFragmentationMemoryTypeBits = uint.MaxValue;

        private readonly ReaderWriterLockSlim PoolsMutex = new ReaderWriterLockSlim();
        private readonly List<VulkanMemoryPool> Pools = new List<VulkanMemoryPool>();
        private uint NextPoolID;

        internal CurrentBudgetData Budget = new CurrentBudgetData();

        public VulkanMemoryAllocator(in VulkanAllocatorCreateInfo createInfo)
        {
            if (VkApi == null)
            {
                throw new InvalidOperationException("API vtable is null, consider using `VulkanMemoryAllocator.SetAPI()`");
            }

            if (createInfo.Instance.Handle == default)
            {
                throw new ArgumentNullException("createInfo.Instance");
            }

            if (createInfo.LogicalDevice.Handle == default)
            {
                throw new ArgumentNullException("createInfo.LogicalDevice");
            }

            if (createInfo.PhysicalDevice.Handle == default)
            {
                throw new ArgumentNullException("createInfo.PhysicalDevice");
            }

            if (!VkApi.CurrentInstance.HasValue || createInfo.Instance.Handle != VkApi.CurrentInstance.Value.Handle)
            {
                throw new ArgumentException("API Instance does not match the Instance passed with 'createInfo'.");
            }

            if (!VkApi.CurrentDevice.HasValue || createInfo.LogicalDevice.Handle != VkApi.CurrentDevice.Value.Handle)
            {
                throw new ArgumentException("API Device does not match the Instance passed with 'createInfo'.");
            }

            this.Instance = createInfo.Instance;
            this.Device = createInfo.LogicalDevice;

            if (VkApi.IsExtensionPresent("VK_KHR_bind_memory2") && !VkApi.TryGetExtension(out this.BindMemory2))
            {
                throw new InvalidOperationException("Unable to get vtable for VK_KHR_bind_memory2");
            }

            if (VkApi.IsExtensionPresent("VK_KHR_get_memory_requirements2") && !VkApi.TryGetExtension(out this.MemoryRequirements2))
            {
                throw new InvalidOperationException("Unable to get vtable for VK_KHR_get_memory_requirements2");
            }

            this.VulkanAPIVersion = createInfo.VulkanAPIVersion;

            if (this.VulkanAPIVersion == 0)
            {
                //this.VulkanAPIVersion = Vk.Version_1_0;
            }

            VkApi.GetPhysicalDeviceProperties(createInfo.PhysicalDevice, out this.PhysicalDeviceProperties);
            VkApi.GetPhysicalDeviceMemoryProperties(createInfo.PhysicalDevice, out this.MemoryProperties);

            Debug.Assert(Helpers.IsPow2(Helpers.DebugAlignment));
            Debug.Assert(Helpers.IsPow2(Helpers.DebugMinBufferImageGranularity));
            Debug.Assert(Helpers.IsPow2((long)this.PhysicalDeviceProperties.Limits.BufferImageGranularity));
            Debug.Assert(Helpers.IsPow2((long)this.PhysicalDeviceProperties.Limits.NonCoherentAtomSize));

            this.PreferredLargeHeapBlockSize = (createInfo.PreferredLargeHeapBlockSize != 0) ? (long)createInfo.PreferredLargeHeapBlockSize : (256L * 1024 * 1024);

            this.GlobalMemoryTypeBits = this.CalculateGlobalMemoryTypeBits();

            if (createInfo.HeapSizeLimits != null)
            {
                Span<MemoryHeap> memoryHeaps = MemoryMarshal.CreateSpan(ref this.MemoryProperties.MemoryHeaps_0, (int)Vk.MaxMemoryHeaps);

                int heapLimitLength = Math.Min(createInfo.HeapSizeLimits.Length, (int)Vk.MaxMemoryHeaps);

                for (int heapIndex = 0; heapIndex < heapLimitLength; ++heapIndex)
                {
                    long limit = createInfo.HeapSizeLimits[heapIndex];

                    if (limit <= 0)
                    {
                        continue;
                    }

                    this.HeapSizeLimitMask |= 1u << heapIndex;
                    ref MemoryHeap heap = ref memoryHeaps[heapIndex];

                    if ((ulong)limit < heap.Size)
                    {
                        heap.Size = (ulong)limit;
                    }
                }
            }

            for (int memTypeIndex = 0; memTypeIndex < this.MemoryTypeCount; ++memTypeIndex)
            {
                long preferredBlockSize = this.CalcPreferredBlockSize(memTypeIndex);

                this.BlockLists[memTypeIndex] =
                    new BlockList(this, null, memTypeIndex, preferredBlockSize, 0, int.MaxValue, this.BufferImageGranularity, createInfo.FrameInUseCount, false, false);

                ref DedicatedAllocationHandler alloc = ref DedicatedAllocations[memTypeIndex];

                alloc.Allocations = new List<Allocation>();
                alloc.Mutex = new ReaderWriterLockSlim();
            }

        }

        public int CurrentFrameIndex { get; set; }

        internal long BufferImageGranularity
        {
            get
            {
                return (long)Math.Max(1, PhysicalDeviceProperties.Limits.BufferImageGranularity);
            }
        }

        internal int MemoryHeapCount => (int)MemoryProperties.MemoryHeapCount;

        internal int MemoryTypeCount => (int)MemoryProperties.MemoryTypeCount;

        internal bool IsIntegratedGPU
        {
            get => this.PhysicalDeviceProperties.DeviceType == PhysicalDeviceType.IntegratedGpu;
        }

        internal uint GlobalMemoryTypeBits { get; private set; }


        public void Dispose()
        {
            if (this.Pools.Count != 0)
            {
                throw new InvalidOperationException("");
            }

            int i = this.MemoryTypeCount;

            while (i-- != 0)
            {
                if (this.DedicatedAllocations[i].Allocations.Count != 0)
                {
                    throw new InvalidOperationException("Unfreed dedicatedAllocations found");
                }

                this.BlockLists[i].Dispose();
            }
        }

        private void GetPhysicalDeviceProperties(out PhysicalDeviceProperties properties)
        {
            throw new NotImplementedException();
        }

        private void GetMemoryProperties(out PhysicalDeviceMemoryProperties properties)
        {
            throw new NotImplementedException();
        }

        private MemoryPropertyFlags GetMemoryTypeProperties(uint memoryTypeIndex)
        {
            throw new NotImplementedException();
        }

        public Stats CalculateStats()
        {
            throw new NotImplementedException();
        }

        public int? FindMemoryTypeIndex(uint memoryTypeBits, in AllocationCreateInfo allocInfo)
        {
            memoryTypeBits &= this.GlobalMemoryTypeBits;

            if (allocInfo.MemoryTypeBits != 0)
            {
                memoryTypeBits &= allocInfo.MemoryTypeBits;
            }

            MemoryPropertyFlags requiredFlags = allocInfo.RequiredFlags, preferredFlags = allocInfo.PreferredFlags, notPreferredFlags = default;

            switch (allocInfo.Usage)
            {
                case MemoryUsage.Unknown:
                    break;
                case MemoryUsage.GPU_Only:
                    if (this.IsIntegratedGPU || (preferredFlags & MemoryPropertyFlags.MemoryPropertyHostVisibleBit) == 0)
                    {
                        preferredFlags |= MemoryPropertyFlags.MemoryPropertyDeviceLocalBit;
                    }
                    break;
                case MemoryUsage.CPU_Only:
                    requiredFlags |= MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit;
                    break;
                case MemoryUsage.CPU_To_GPU:
                    requiredFlags |= MemoryPropertyFlags.MemoryPropertyHostVisibleBit;
                    if (!this.IsIntegratedGPU || (preferredFlags & MemoryPropertyFlags.MemoryPropertyHostVisibleBit) == 0)
                    {
                        preferredFlags |= MemoryPropertyFlags.MemoryPropertyDeviceLocalBit;
                    }
                    break;
                case MemoryUsage.GPU_To_CPU:
                    requiredFlags |= MemoryPropertyFlags.MemoryPropertyHostVisibleBit;
                    preferredFlags |= MemoryPropertyFlags.MemoryPropertyHostCachedBit;
                    break;
                case MemoryUsage.CPU_Copy:
                    notPreferredFlags |= MemoryPropertyFlags.MemoryPropertyDeviceLocalBit;
                    break;
                case MemoryUsage.GPU_LazilyAllocated:
                    requiredFlags |= MemoryPropertyFlags.MemoryPropertyLazilyAllocatedBit;
                    break;
                default:
                    throw new ArgumentException("Invalid Usage Flags");
            }

            if (((allocInfo.RequiredFlags | allocInfo.PreferredFlags) & (MemoryPropertyFlags.MemoryPropertyDeviceCoherentBitAmd | MemoryPropertyFlags.MemoryPropertyDeviceUncachedBitAmd)) == 0)
            {
                notPreferredFlags |= MemoryPropertyFlags.MemoryPropertyDeviceCoherentBitAmd;
            }

            int? memoryTypeIndex = null;
            int minCost = int.MaxValue;
            uint memTypeBit = 1;

            for (int memTypeIndex = 0; memTypeIndex < this.MemoryTypeCount; ++memTypeIndex, memTypeBit <<= 1)
            {
                if ((memTypeBit & memoryTypeBits) == 0)
                    continue;

                var currFlags = this.MemoryType(memTypeIndex).PropertyFlags;

                int currCost = BitOperations.PopCount((uint)(preferredFlags & ~currFlags)) + BitOperations.PopCount((uint)(currFlags & notPreferredFlags));

                if (currCost < minCost)
                {
                    if (currCost == 0)
                    {
                        return memTypeIndex;
                    }

                    memoryTypeIndex = memTypeIndex;
                    minCost = currCost;
                }
            }

            return memoryTypeIndex;
        }

        public Result FindMemoryTypeIndexForBufferInfo(in BufferCreateInfo bufferInfo, in AllocationCreateInfo allocInfo, out int memoryTypeIndex)
        {
            throw new NotImplementedException();
        }

        public Result FindMemoryTypeIndexForImageInfo(in ImageCreateInfo imageInfo, in AllocationCreateInfo allocInfo, out int memoryTypeIndex)
        {
            throw new NotImplementedException();
        }

        public Result CreatePool(in AllocationPoolCreateInfo poolInfo, out VulkanMemoryPool pool)
        {
            throw new NotImplementedException();
        }

        public Result AllocateMemory(in MemoryRequirements requirements, in AllocationCreateInfo createInfo, out Allocation allocInfo)
        {
            throw new NotImplementedException();
        }

        public Result AllocateMemoryForBuffer(Buffer buffer, in AllocationCreateInfo createInfo, out Allocation? allocation)
        {
            this.GetBufferMemoryRequirements(buffer, out MemoryRequirements memReq, out bool requireDedicatedAlloc, out bool prefersDedicatedAlloc);

            return this.AllocateMemory(in memReq, requireDedicatedAlloc, prefersDedicatedAlloc, buffer, default, in createInfo, SuballocationType.Buffer, out allocation);
        }

        public Result AllocateMemoryForImage(Image image, in AllocationCreateInfo createInfo, out Allocation allocation)
        {
            this.GetImageMemoryRequirements(image, out var memReq, out bool requireDedicatedAlloc, out bool preferDedicatedAlloc);

            return this.AllocateMemory(in memReq, requireDedicatedAlloc, preferDedicatedAlloc, default, image, in createInfo, SuballocationType.Image_Unknown, out allocation);
        }

        public void FreeMemory(Allocation allocation)
        {
            throw new NotImplementedException();
        }

        public Allocation GetAllocationInfo(IntPtr allocation)
        {
            throw new NotImplementedException();
        }

        public bool TouchAllocation(IntPtr allocation)
        {
            throw new NotImplementedException();
        }

        public Result MapMemory(IntPtr allocation, out IntPtr mappedPointer)
        {
            throw new NotImplementedException();
        }

        public void UnmapMemory(IntPtr allocation)
        {
            throw new NotImplementedException();
        }

        public void FlushAllocation(IntPtr allocation, ulong offset, ulong size)
        {
            throw new NotImplementedException();
        }

        public void InvalidateAllocation(IntPtr Allocation, ulong offset, ulong size)
        {
            throw new NotImplementedException();
        }

        public Result CheckCorruption(uint memoryTypeBits)
        {
            throw new NotImplementedException();
        }

        public Result CreateBuffer(in BufferCreateInfo bufferInfo, in AllocationCreateInfo allocInfo, out Buffer? buffer, out Allocation? allocation)
        {
            Result res;
            Buffer bufferLoc;
            Allocation? allocationLoc;

            buffer = null;
            allocation = null;

            fixed (BufferCreateInfo* pInfo = &bufferInfo)
            {
                res = VkApi.CreateBuffer(this.Device, pInfo, null, &bufferLoc);

                if (res < 0)
                {
                    return res;
                }
            }

            res = this.AllocateMemoryForBuffer(bufferLoc, allocInfo, out allocationLoc);

            if (res < 0)
            {
                VkApi.DestroyBuffer(this.Device, bufferLoc, null);
                return res;
            }
            else if ((allocInfo.Flags & AllocationCreateFlags.DontBind) != 0)
            {
                Debug.Assert(allocationLoc != null);

                res = this.BindBufferMemory(allocationLoc, 0, bufferLoc, default);

                if (res < 0)
                {
                    VkApi.DestroyBuffer(this.Device, bufferLoc, null);
                    this.FreeMemory(allocationLoc);
                    return res;
                }
            }

            buffer = bufferLoc;
            allocation = allocationLoc;

            return Result.Success;
        }

        public Result CreateImage(in ImageCreateInfo imageInfo, in AllocationCreateInfo allocInfo, out Image? image, out Allocation? allocation)
        {
            image = null;
            allocation = null;

            if (imageInfo.Extent.Width == 0 ||
                imageInfo.Extent.Height == 0 ||
                imageInfo.Extent.Depth == 0 || 
                imageInfo.MipLevels == 0 ||
                imageInfo.ArrayLayers == 0)
            {
                return Result.ErrorValidationFailedExt;
            }

            Result res;
            Image imageLoc;

            fixed (ImageCreateInfo* pInfo = &imageInfo)
            {
                res = VkApi.CreateImage(this.Device, pInfo, null, &imageLoc);

                if (res < 0)
                {
                    return res;
                }
            }

            SuballocationType suballocType = imageInfo.Tiling == ImageTiling.Optimal ? SuballocationType.Image_Optimal : SuballocationType.Image_Linear;

            res = this.AllocateMemoryForImage(imageLoc, allocInfo, out var allocationLoc);

            if (res < 0)
            {
                VkApi.DestroyImage(this.Device, imageLoc, null);
                return res;
            }

            if ((allocInfo.Flags & AllocationCreateFlags.DontBind) == 0)
            {
                res = this.BindImageMemory(allocationLoc, 0, imageLoc, default);

                if (res < 0)
                {
                    allocationLoc.Dispose();
                    VkApi.DestroyImage(this.Device, imageLoc, null);
                    return res;
                }
            }

            image = imageLoc;
            allocation = allocationLoc;

            return Result.Success;
        }

        private ref MemoryType MemoryType(int index)
        {
            Debug.Assert((uint)index < Vk.MaxMemoryTypes);

            return ref Unsafe.Add(ref this.MemoryProperties.MemoryTypes_0, index);
        }


        internal int MemoryTypeIndexToHeapIndex(int typeIndex)
        {
            Debug.Assert(typeIndex < this.MemoryProperties.MemoryTypeCount);
            return (int)MemoryType(typeIndex).HeapIndex;
        }

        internal bool IsMemoryTypeNonCoherent(int memTypeIndex)
        {
            return (MemoryType(memTypeIndex).PropertyFlags & (MemoryPropertyFlags.MemoryPropertyHostVisibleBit | MemoryPropertyFlags.MemoryPropertyHostCoherentBit)) == MemoryPropertyFlags.MemoryPropertyHostVisibleBit;
        }

        internal long GetMemoryTypeMinAlignment(int memTypeIndex)
        {
            return IsMemoryTypeNonCoherent(memTypeIndex) ? (long)Math.Max(1, this.PhysicalDeviceProperties.Limits.NonCoherentAtomSize) : 1;
        }

        internal void GetBufferMemoryRequirements(Buffer buffer, out MemoryRequirements memReq, out bool requiresDedicatedAllocation, out bool prefersDedicatedAllocation)
        {
            BufferMemoryRequirementsInfo2 req;
            MemoryDedicatedRequirements dedicatedRequirements;
            MemoryRequirements2 memReq2;

            if (this.VulkanAPIVersion >= Helpers.VulkanAPIVersion_1_1)
            {
                req = new BufferMemoryRequirementsInfo2
                {
                    SType = StructureType.BufferMemoryRequirementsInfo2,
                    Buffer = buffer
                };

                dedicatedRequirements = new MemoryDedicatedRequirements
                {
                    SType = StructureType.MemoryDedicatedRequirements,
                };

                memReq2 = new MemoryRequirements2
                {
                    SType = StructureType.MemoryRequirements2,
                    PNext = &dedicatedRequirements
                };

                VkApi.GetBufferMemoryRequirements2(this.Device, &req, &memReq2);

                memReq = memReq2.MemoryRequirements;
                requiresDedicatedAllocation = dedicatedRequirements.RequiresDedicatedAllocation != 0;
                prefersDedicatedAllocation = dedicatedRequirements.PrefersDedicatedAllocation != 0;
            }
            else if (this.UseKhrDedicatedAllocation && this.MemoryRequirements2 != null)
            {
                req = new BufferMemoryRequirementsInfo2
                {
                    SType = StructureType.BufferMemoryRequirementsInfo2,
                    Buffer = buffer
                };

                dedicatedRequirements = new MemoryDedicatedRequirements
                {
                    SType = StructureType.MemoryDedicatedRequirements,
                };

                memReq2 = new MemoryRequirements2
                {
                    SType = StructureType.MemoryRequirements2,
                    PNext = &dedicatedRequirements
                };

                MemoryRequirements2.GetBufferMemoryRequirements2(this.Device, &req, &memReq2);

                memReq = memReq2.MemoryRequirements;
                requiresDedicatedAllocation = dedicatedRequirements.RequiresDedicatedAllocation != 0;
                prefersDedicatedAllocation = dedicatedRequirements.PrefersDedicatedAllocation != 0;
            }
            else
            {
                VkApi.GetBufferMemoryRequirements(this.Device, buffer, out memReq);
                requiresDedicatedAllocation = false;
                prefersDedicatedAllocation = false;
            }
        }

        internal void GetImageMemoryRequirements(Image image, out MemoryRequirements memReq, out bool requiresDedicatedAllocation, out bool prefersDedicatedAllocation)
        {
            ImageMemoryRequirementsInfo2 req;
            MemoryDedicatedRequirements dedicatedRequirements;
            MemoryRequirements2 memReq2;

            if (VulkanAPIVersion >= Helpers.VulkanAPIVersion_1_1)
            {
                req = new ImageMemoryRequirementsInfo2
                {
                    SType = StructureType.BufferMemoryRequirementsInfo2,
                    Image = image
                };

                dedicatedRequirements = new MemoryDedicatedRequirements
                {
                    SType = StructureType.MemoryDedicatedRequirements,
                };

                memReq2 = new MemoryRequirements2
                {
                    SType = StructureType.MemoryRequirements2,
                    PNext = &dedicatedRequirements
                };

                VkApi.GetImageMemoryRequirements2(this.Device, &req, &memReq2);

                memReq = memReq2.MemoryRequirements;
                requiresDedicatedAllocation = dedicatedRequirements.RequiresDedicatedAllocation != 0;
                prefersDedicatedAllocation = dedicatedRequirements.PrefersDedicatedAllocation != 0;
            }
            else if (this.UseKhrDedicatedAllocation && this.MemoryRequirements2 != null)
            {
                req = new ImageMemoryRequirementsInfo2
                {
                    SType = StructureType.BufferMemoryRequirementsInfo2,
                    Image = image
                };

                dedicatedRequirements = new MemoryDedicatedRequirements
                {
                    SType = StructureType.MemoryDedicatedRequirements,
                };

                memReq2 = new MemoryRequirements2
                {
                    SType = StructureType.MemoryRequirements2,
                    PNext = &dedicatedRequirements
                };

                MemoryRequirements2.GetImageMemoryRequirements2(this.Device, &req, &memReq2);

                memReq = memReq2.MemoryRequirements;
                requiresDedicatedAllocation = dedicatedRequirements.RequiresDedicatedAllocation != 0;
                prefersDedicatedAllocation = dedicatedRequirements.PrefersDedicatedAllocation != 0;
            }
            else
            {
                VkApi.GetImageMemoryRequirements(this.Device, image, out memReq);
                requiresDedicatedAllocation = false;
                prefersDedicatedAllocation = false;
            }
        }

        internal Result AllocateMemory(in MemoryRequirements memReq, bool requiresDedicatedAllocation,
            bool prefersDedicatedAllocation, Buffer dedicatedBuffer, Image dedicatedImage,
            in AllocationCreateInfo createInfo, SuballocationType suballocType, Allocation[] allocations)
        {
            Array.Clear(allocations, 0, allocations.Length);

            Debug.Assert(Helpers.IsPow2((long)memReq.Alignment));

            if (memReq.Size == 0)
                return Result.ErrorValidationFailedExt;

            const AllocationCreateFlags CheckFlags1 = AllocationCreateFlags.DedicatedMemory | AllocationCreateFlags.NeverAllocate;
            const AllocationCreateFlags CheckFlags2 = AllocationCreateFlags.Mapped | AllocationCreateFlags.CanBecomeLost;

            if ((createInfo.Flags & CheckFlags1) == CheckFlags1)
            {
                throw new ArgumentException("Specifying AllocationCreateFlags.DedicatedMemory with AllocationCreateFlags.NeverAllocate is invalid");
            }
            else if ((createInfo.Flags & CheckFlags2) == CheckFlags2)
            {
                throw new ArgumentException("Specifying AllocationCreateFlags.Mapped with AllocationCreateFlags.CanBecomeLost is invalid");
            }

            if (requiresDedicatedAllocation)
            {
                if ((createInfo.Flags & AllocationCreateFlags.NeverAllocate) != 0)
                {
                    throw new AllocationException("AllocationCreateFlags.NeverAllocate specified while dedicated allocation required", Result.ErrorOutOfDeviceMemory);
                }

                if (createInfo.Pool != null)
                {
                    throw new ArgumentException("Pool specified while dedicated allocation required");
                }
            }

            if (createInfo.Pool != null && (createInfo.Flags & AllocationCreateFlags.DedicatedMemory) != 0)
            {
                throw new ArgumentException("Specified AllocationCreateFlags.DedicatedMemory when createInfo.Pool is not null");
            }

            if (createInfo.Pool != null)
            {
                int memoryTypeIndex = createInfo.Pool.BlockList.MemoryTypeIndex;
                long alignmentForPool = Math.Max((long)memReq.Alignment, this.GetMemoryTypeMinAlignment(memoryTypeIndex));

                AllocationCreateInfo infoForPool = createInfo;

                if ((createInfo.Flags & AllocationCreateFlags.Mapped) != 0 && !this.MemoryType(memoryTypeIndex).PropertyFlags.HasFlag(MemoryPropertyFlags.MemoryPropertyHostVisibleBit))
                {
                    infoForPool.Flags &= ~AllocationCreateFlags.Mapped;
                }

                return createInfo.Pool.BlockList.Allocate(this.CurrentFrameIndex, (long)memReq.Size, alignmentForPool, infoForPool, suballocType, allocations);
            }
            else
            {
                uint memoryTypeBits = memReq.MemoryTypeBits;
                var typeIndex = this.FindMemoryTypeIndex(memoryTypeBits, createInfo);

                if (typeIndex == null)
                {
                    return Result.ErrorFeatureNotPresent;
                }

                long alignmentForType = Math.Max((long)memReq.Alignment, this.GetMemoryTypeMinAlignment((int)typeIndex));

                return this.AllocateMemoryOfType((long)memReq.Size, alignmentForType, requiresDedicatedAllocation | prefersDedicatedAllocation, dedicatedBuffer, dedicatedImage, createInfo, (int)typeIndex, suballocType, allocations);
            }
        }

        internal Result AllocateMemory(in MemoryRequirements memReq, bool requiresDedicatedAllocation,
            bool prefersDedicatedAllocation, Buffer dedicatedBuffer, Image dedicatedImage,
            in AllocationCreateInfo createInfo, SuballocationType suballocType, out Allocation? allocation)
        {

        }

        internal void FreeMemory(params Allocation[] allocations)
        {

        }

        internal void CalculateStats(Stats stats)
        {

        }

        internal void GetBudget(out AllocationBudget outBudget, int firstHeap, int heapCount)
        {

        }

        internal Result DefragmentationBegin(in DefragmentationInfo2 info, DefragmentationStats stats, DefragmentationContext context)
        {

        }

        internal Result DefragmentationEnd(DefragmentationContext context)
        {

        }

        internal Result DefragmentationPassBegin(ref DefragmentationPassMoveInfo[] passInfo, DefragmentationContext context)
        {

        }

        internal Result DefragmentationPassEnd(DefragmentationContext context)
        {

        }

        internal bool TouchAllocation(Allocation allocation)
        {

        }

        internal Result CreatePool(in AllocationPoolCreateInfo createInfo, VulkanMemoryPool pool)
        {

        }

        internal void DestroyPool(VulkanMemoryPool pool)
        {

        }

        internal void GetPoolStats(VulkanMemoryPool pool, out PoolStats stats)
        {

        }

        internal int MakePoolAllocationsLost(VulkanMemoryPool pool)
        {

        }

        internal Result CheckPoolCorruption(VulkanMemoryPool pool)
        {

        }

        internal Result CheckCorruption(uint memoryTypeBits)
        {

        }

        internal Allocation CreateLostAllocation()
        {

        }

        internal Result AllocateVulkanMemory(in MemoryAllocateInfo allocInfo, out DeviceMemory memory)
        {

        }

        internal Result FreeVulkanMemory(uint memoryType, long size, DeviceMemory memory)
        {

        }

        internal Result BindVulkanBuffer(DeviceMemory memory, long offset, Buffer buffer, IntPtr next)
        {

        }

        internal Result BindVulkanImage(DeviceMemory memory, long offset, Image image, IntPtr next)
        {

        }

        internal Result Map(Allocation allocation, out IntPtr pData)
        {

        }

        internal void Unmap(Allocation allocation)
        {

        }

        internal Result BindBufferMemory(Allocation allocation, long allocationLocalOffset, Buffer buffer, IntPtr pNext)
        {

        }

        internal Result BindImageMemory(Allocation allocation, long allocationLocalOffset, Image buffer, IntPtr pNext)
        {

        }

        internal void FillAllocation(Allocation allocation, byte pattern)
        {

        }

        internal uint GetGPUDefragmentationMemoryTypeBits()
        {

        }

        private long CalcPreferredBlockSize(int memTypeIndex)
        {
            var heapIndex = this.MemoryTypeIndexToHeapIndex(memTypeIndex);

            Debug.Assert((uint)heapIndex < Vk.MaxMemoryHeaps);

            var heapSize = (long)Unsafe.Add(ref this.MemoryProperties.MemoryHeaps_0, heapIndex).Size;

            return Helpers.AlignUp(heapSize <= SmallHeapMaxSize ? (heapSize / 8) : this.PreferredLargeHeapBlockSize, 32);
        }

        private Result AllocateMemoryOfType(long size, long alignment, bool dedicatedAllocation, Buffer dedicatedBuffer,
                                            Image dedicatedImage, in AllocationCreateInfo createInfo,
                                            int memoryTypeIndex, SuballocationType suballocType, Allocation[] allocations)
        {
            var finalCreateInfo = createInfo;

            if ((finalCreateInfo.Flags & AllocationCreateFlags.Mapped) != 0 && (this.MemoryType(memoryTypeIndex).PropertyFlags & MemoryPropertyFlags.MemoryPropertyHostVisibleBit) == 0)
            {
                finalCreateInfo.Flags &= ~AllocationCreateFlags.Mapped;
            }

            if (finalCreateInfo.Usage == MemoryUsage.GPU_LazilyAllocated)
            {
                finalCreateInfo.Flags |= AllocationCreateFlags.DedicatedMemory;
            }

            var blockList = this.BlockLists[memoryTypeIndex];

            long preferredBlockSize = blockList.PreferredBlockSize;
            bool preferDedicatedMemory = dedicatedAllocation || size > preferredBlockSize / 2;

            if (preferDedicatedMemory && (finalCreateInfo.Flags & AllocationCreateFlags.NeverAllocate) == 0 && finalCreateInfo.Pool == null)
            {
                finalCreateInfo.Flags |= AllocationCreateFlags.DedicatedMemory;
            }

            if ((finalCreateInfo.Flags & AllocationCreateFlags.DedicatedMemory) != 0)
            {
                if ((finalCreateInfo.Flags & AllocationCreateFlags.NeverAllocate) != 0)
                {
                    return Result.ErrorOutOfDeviceMemory;
                }

                return this.AllocateDedicatedMemory(size,
                                                    suballocType,
                                                    memoryTypeIndex,
                                                    (finalCreateInfo.Flags & AllocationCreateFlags.WithinBudget) != 0,
                                                    (finalCreateInfo.Flags & AllocationCreateFlags.Mapped) != 0,
                                                    finalCreateInfo.UserData,
                                                    dedicatedBuffer,
                                                    dedicatedImage,
                                                    allocations);
            }


        }

        private Result AllocateDedicatedMemoryPage(
            long size, SuballocationType suballocType, uint memTypeIndex, in MemoryAllocateInfo allocInfo, bool map,
            object userData, out Allocation allocation)
        {

        }

        private Result AllocateDedicatedMemory(long size, SuballocationType suballocType, int memTypeIndex, bool withinBudget, bool map, object userData, Buffer dedicatedBuffer, Image dedicatedImage, Allocation[] allocations)
        {

        }

        private void FreeDedicatedMemory(Allocation allocation)
        {

        }

        private uint CalculateGpuDefragmentationMemoryTypeBits()
        {

        }

        private uint CalculateGlobalMemoryTypeBits()
        {

        }

        internal struct DedicatedAllocationHandler
        {
            public List<Allocation> Allocations;
            public ReaderWriterLockSlim Mutex;
        }
    }
}
