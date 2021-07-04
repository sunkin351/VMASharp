using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Silk.NET.Core;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

using VMASharp;

using VulkanCube.TaskTypes;

namespace VulkanCube
{
    public abstract class AllocatorAndBuffersExample : CommandPoolCreationExample
    {
        protected const Format DepthFormat = Format.D16Unorm;


        protected readonly VulkanMemoryAllocator Allocator;

        protected Buffer VertexBuffer;
        protected Buffer IndexBuffer;
        protected Buffer InstanceBuffer;

        protected Allocation VertexAllocation;
        protected Allocation IndexAllocation;
        protected Allocation InstanceAllocation;

        protected uint VertexCount;
        protected uint IndexCount;
        protected uint InstanceCount;

        protected CameraUniform Camera = new CameraUniform();

        protected Buffer UniformBuffer;
        protected Allocation UniformAllocation;

        protected DepthBufferObject DepthBuffer;

        private WaitScheduler scheduler;

        protected AllocatorAndBuffersExample() : base()
        {
            scheduler = new WaitScheduler(Device);

            this.Allocator = CreateAllocator();

            CreateBuffers();

            CreateUniformBuffer();

            CreateDepthBuffer();
        }

        public override unsafe void Dispose()
        {
            scheduler.Dispose();

            VkApi.DestroyImageView(this.Device, DepthBuffer.View, null);
            VkApi.DestroyImage(this.Device, DepthBuffer.Image, null);
            DepthBuffer.Allocation.Dispose();

            VkApi.DestroyBuffer(this.Device, UniformBuffer, null);
            UniformAllocation.Dispose();

            VkApi.DestroyBuffer(this.Device, VertexBuffer, null);
            VertexAllocation.Dispose();

            VkApi.DestroyBuffer(this.Device, IndexBuffer, null);
            IndexAllocation.Dispose();

            VkApi.DestroyBuffer(this.Device, InstanceBuffer, null);
            InstanceAllocation.Dispose();

            Allocator.Dispose();

            base.Dispose();
        }

        private unsafe VulkanMemoryAllocator CreateAllocator()
        {
            uint version;
            var res = VkApi.EnumerateInstanceVersion(&version);

            if (res != Result.Success)
            {
                throw new VulkanResultException("Unable to retrieve instance version", res);
            }

            VulkanMemoryAllocatorCreateInfo createInfo = new VulkanMemoryAllocatorCreateInfo(
                (Version32)version, VkApi, Instance, PhysicalDevice, Device,
                preferredLargeHeapBlockSize: 64L * 1024 * 1024, frameInUseCount: DrawCubeExample.MaxFramesInFlight);

            return new VulkanMemoryAllocator(createInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private async Task<(Buffer, Allocation)> CreateBufferObject<T>(BufferUsageFlags usageFlags, ReadOnlyMemory<T> data) where T: unmanaged
        {
            BufferCreateInfo bufferInfo = new();
            bufferInfo.SType = StructureType.BufferCreateInfo;
            bufferInfo.Usage = usageFlags | BufferUsageFlags.BufferUsageTransferDstBit;
            bufferInfo.Size = (uint)Unsafe.SizeOf<T>() * (uint)data.Length;

            AllocationCreateInfo allocInfo = new(usage: MemoryUsage.GPU_Only);

            var buffer = this.Allocator.CreateBuffer(in bufferInfo, in allocInfo, out Allocation allocation);

            bufferInfo.Usage = BufferUsageFlags.BufferUsageTransferSrcBit;
            allocInfo.Usage = MemoryUsage.CPU_Only;
            allocInfo.Flags = AllocationCreateFlags.Mapped;

            var hostBuffer = this.Allocator.CreateBuffer(in bufferInfo, in allocInfo, out Allocation hostAllocation);

            if (!hostAllocation.TryGetMemory(out Memory<T> span))
            {
                throw new InvalidOperationException("Unable to get Memory<T> to mapped allocation.");
            }

            data.CopyTo(span);

            await TransferBufferData(hostBuffer, buffer, new BufferCopy(0, 0, bufferInfo.Size));

            DestroyBuffer(hostBuffer, hostAllocation);

            return (buffer, allocation);

            unsafe void DestroyBuffer(Buffer buffer, Allocation alloc)
            {
                VkApi.DestroyBuffer(this.Device, buffer, null);
                alloc.Dispose();
            }
        }

        private void CreateBuffers()
        {
            PositionColorVertex[] positionData = VertexData.IndexedCubeData;

            ushort[] indexData = VertexData.CubeIndexData;

            InstanceData[] instanceData = new InstanceData[]
            {
                new InstanceData(new Vector3(0, 0, 0)),
                new InstanceData(new Vector3(2, 0, 0)),
                new InstanceData(new Vector3(-2, 0, 0))
            };

            Task<(Buffer, Allocation)> task1, task2, task3;

            task1 = this.CreateBufferObject<PositionColorVertex>(BufferUsageFlags.BufferUsageVertexBufferBit, positionData);
            task2 = this.CreateBufferObject<ushort>(BufferUsageFlags.BufferUsageIndexBufferBit, indexData);
            task3 = this.CreateBufferObject<InstanceData>(BufferUsageFlags.BufferUsageVertexBufferBit, instanceData);

            Task.WaitAll(task1, task2, task3);

            (this.VertexBuffer, this.VertexAllocation) = task1.Result;

            this.VertexCount = (uint)positionData.Length;

            (this.IndexBuffer, this.IndexAllocation) = task2.Result;

            this.IndexCount = (uint)indexData.Length;

            (this.InstanceBuffer, this.InstanceAllocation) = task3.Result;

            this.InstanceCount = (uint)instanceData.Length;
        }

        protected uint UniformBufferSize = (uint)Unsafe.SizeOf<Matrix4x4>() * 2;

        private unsafe void CreateUniformBuffer() //Simpler setup from the Vertex buffer because there is no staging or device copying
        {
            BufferCreateInfo bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = this.UniformBufferSize,
                Usage = BufferUsageFlags.BufferUsageUniformBufferBit,
                SharingMode = SharingMode.Exclusive
            };

            // Allow this to be updated every frame
            AllocationCreateInfo allocInfo = new AllocationCreateInfo
            {
                Usage = MemoryUsage.CPU_To_GPU,
                RequiredFlags = MemoryPropertyFlags.MemoryPropertyHostVisibleBit
            };

            // Binds buffer to allocation for you
            var buffer = this.Allocator.CreateBuffer(in bufferInfo, in allocInfo, out var allocation);

            // Camera/MVP Matrix calculation
            Camera.LookAt(new Vector3(2f, 2f, -5f), new Vector3(0, 0, 0), new Vector3(0, 1, 0));

            var radFov = MathF.PI / 180f * 45f;
            var aspect = (float)this.SwapchainExtent.Width / this.SwapchainExtent.Height;

            Camera.Perspective(radFov, aspect, 0.5f, 100f);

            Camera.UpdateMVP();

            allocation.Map();

            Matrix4x4* ptr = (Matrix4x4*)allocation.MappedData;

            ptr[0] = Camera.MVPMatrix; // Camera Matrix
            ptr[1] = Matrix4x4.Identity;         // Model Matrix

            allocation.Unmap();

            this.UniformBuffer = buffer;
            this.UniformAllocation = allocation;
        }

        private unsafe void CreateDepthBuffer()
        {
            var depthInfo = new ImageCreateInfo
            {
                SType = StructureType.ImageCreateInfo,
                ImageType = ImageType.ImageType2D,
                Format = DepthFormat,
                Extent = new Extent3D(this.SwapchainExtent.Width, this.SwapchainExtent.Height, 1),
                MipLevels = 1,
                ArrayLayers = 1,
                Samples = SampleCountFlags.SampleCount1Bit,
                InitialLayout = ImageLayout.Undefined,
                Usage = ImageUsageFlags.ImageUsageDepthStencilAttachmentBit,
                SharingMode = SharingMode.Exclusive
            };

            var depthViewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Format = DepthFormat,
                Components = new ComponentMapping(ComponentSwizzle.R, ComponentSwizzle.G, ComponentSwizzle.B, ComponentSwizzle.A),
                SubresourceRange = new ImageSubresourceRange(aspectMask: ImageAspectFlags.ImageAspectDepthBit, levelCount: 1, layerCount: 1),
                ViewType = ImageViewType.ImageViewType2D
            };

            AllocationCreateInfo allocInfo = new AllocationCreateInfo
            {
                Usage = MemoryUsage.GPU_Only
            };

            var image = this.Allocator.CreateImage(depthInfo, allocInfo, out Allocation alloc);

            depthViewInfo.Image = image;

            ImageView view;
            var res = VkApi.CreateImageView(this.Device, &depthViewInfo, null, &view);

            if (res != Result.Success)
            {
                throw new Exception("Unable to create depth image view!");
            }

            this.DepthBuffer.Image = image;
            this.DepthBuffer.View = view;
            this.DepthBuffer.Allocation = alloc;
        }

        //Helper methods

        protected unsafe Fence CreateFence(bool initialState = false)
        {
            FenceCreateInfo info = new FenceCreateInfo(flags: initialState ? FenceCreateFlags.FenceCreateSignaledBit : 0);

            Fence fence;
            var res = VkApi.CreateFence(this.Device, &info, null, &fence);

            if (res != Result.Success)
            {
                throw new VulkanResultException("Unable to create Fence!", res);
            }

            return fence;
        }

        protected unsafe Task TransferBufferData(Buffer source, Buffer destination, BufferCopy copyRegion)
        {
            CommandBuffer cBuffer = AllocateCommandBuffer(CommandBufferLevel.Primary);

            BeginCommandBuffer(cBuffer, CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit);

            VkApi.CmdCopyBuffer(cBuffer, source, destination, 1, in copyRegion);

            EndCommandBuffer(cBuffer);

            var subInfo = new SubmitInfo(commandBufferCount: 1, pCommandBuffers: &cBuffer);

            var fence = CreateFence();

            var res = VkApi.QueueSubmit(GraphicsQueue, 1, &subInfo, fence);

            if (res != Result.Success)
                throw new Exception("Unable to submit to queue. " + res);

            var bufferTmp = cBuffer; //Allows the capture of this command buffer in a lambda

            var task = this.scheduler.WaitForFenceAsync(fence);

            task.GetAwaiter().OnCompleted(() =>
            {
                FreeCommandBuffer(bufferTmp);

                VkApi.DestroyFence(Device, fence, null);
            });

            return task;
        }

        protected struct DepthBufferObject
        {
            public Image Image;
            public Allocation Allocation;
            public ImageView View;
        }
    }
}
