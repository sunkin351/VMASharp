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

        protected Task BufferCopyPromise;

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

        private unsafe void CreateBuffers()
        {
            PositionColorVertex[] positionData = VertexData.IndexedCubeData;

            ushort[] indexData = VertexData.CubeIndexData;

            InstanceData[] instanceData = new InstanceData[]
            {
                new InstanceData(new Vector3(0, 0, 0)),
                new InstanceData(new Vector3(2, 0, 0)),
                new InstanceData(new Vector3(-2, 0, 0))
            };

            CreateHostBufferWithContent<PositionColorVertex>(positionData, out var hostBuffer1, out var hostAlloc1);
            CreateHostBufferWithContent<ushort>(indexData, out var hostBuffer2, out var hostAlloc2);
            CreateHostBufferWithContent<InstanceData>(instanceData, out var hostBuffer3, out var hostAlloc3);

            CreateDeviceLocalBuffer(BufferUsageFlags.BufferUsageVertexBufferBit, GetByteLength(positionData), out this.VertexBuffer, out this.VertexAllocation);
            CreateDeviceLocalBuffer(BufferUsageFlags.BufferUsageIndexBufferBit, GetByteLength(indexData), out this.IndexBuffer, out this.IndexAllocation);
            CreateDeviceLocalBuffer(BufferUsageFlags.BufferUsageVertexBufferBit, GetByteLength(instanceData), out this.InstanceBuffer, out this.InstanceAllocation);

            var cbuffer = AllocateCommandBuffer(CommandBufferLevel.Primary);

            BufferCopy* copies = stackalloc BufferCopy[1];

            BeginCommandBuffer(cbuffer, CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit);

            copies[0] = new BufferCopy(0, 0, (ulong)GetByteLength(positionData));
            VkApi.CmdCopyBuffer(cbuffer, hostBuffer1, this.VertexBuffer, 1, copies);

            copies[0] = new BufferCopy(0, 0, (ulong)GetByteLength(indexData));
            VkApi.CmdCopyBuffer(cbuffer, hostBuffer2, this.IndexBuffer, 1, copies);

            copies[0] = new BufferCopy(0, 0, (ulong)GetByteLength(instanceData));
            VkApi.CmdCopyBuffer(cbuffer, hostBuffer3, this.InstanceBuffer, 1, copies);

            EndCommandBuffer(cbuffer);

            var subInfo = new SubmitInfo(commandBufferCount: 1, pCommandBuffers: &cbuffer);

            var fence = CreateFence();

            var res = VkApi.QueueSubmit(GraphicsQueue, 1, &subInfo, fence);

            if (res != Result.Success)
                throw new Exception("Unable to submit to queue. " + res);

            var bufferTmp = cbuffer; //Allows the capture of this command buffer in a lambda

            BufferCopyPromise = this.scheduler.WaitForFenceAsync(fence);

            BufferCopyPromise.GetAwaiter().OnCompleted(() =>
            {
                VkApi.DestroyFence(Device, fence, null);

                FreeCommandBuffer(bufferTmp);

                VkApi.DestroyBuffer(Device, hostBuffer1, null);
                VkApi.DestroyBuffer(Device, hostBuffer2, null);
                VkApi.DestroyBuffer(Device, hostBuffer3, null);

                hostAlloc1.Dispose();
                hostAlloc2.Dispose();
                hostAlloc3.Dispose();
            });

            this.VertexCount    = (uint)positionData.Length;
            this.IndexCount     = (uint)indexData.Length;
            this.InstanceCount  = (uint)instanceData.Length;
        }

        static uint GetByteLength<T>(T[] arr) where T: unmanaged
        {
            return (uint)Unsafe.SizeOf<T>() * (uint)arr.Length;
        }

        private unsafe void CreateHostBufferWithContent<T>(ReadOnlySpan<T> span, out Buffer buffer, out Allocation alloc) where T : unmanaged
        {
            BufferCreateInfo bufferInfo = new(
                usage: BufferUsageFlags.BufferUsageTransferSrcBit,
                size: (uint)Unsafe.SizeOf<T>() * (uint)span.Length);

            AllocationCreateInfo allocInfo = new(AllocationCreateFlags.Mapped, usage: MemoryUsage.CPU_Only);

            buffer = Allocator.CreateBuffer(in bufferInfo, in allocInfo, out alloc);

            if (!alloc.TryGetSpan(out Span<T> bufferSpan))
            {
                throw new InvalidOperationException("Unable to get Span<T> to mapped allocation.");
            }

            span.CopyTo(bufferSpan);
        }

        private unsafe void CreateDeviceLocalBuffer(BufferUsageFlags usage, uint size, out Buffer buffer, out Allocation alloc)
        {
            BufferCreateInfo bufferInfo = new(
                usage: usage | BufferUsageFlags.BufferUsageTransferDstBit,
                size: size);

            AllocationCreateInfo allocInfo = new(usage: MemoryUsage.GPU_Only);

            buffer = Allocator.CreateBuffer(in bufferInfo, in allocInfo, out alloc);
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
            var allocInfo = new AllocationCreateInfo(
                usage: MemoryUsage.CPU_To_GPU,
                requiredFlags: MemoryPropertyFlags.MemoryPropertyHostVisibleBit);

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

            var allocInfo = new AllocationCreateInfo(usage: MemoryUsage.GPU_Only);

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

        protected struct DepthBufferObject
        {
            public Image Image;
            public Allocation Allocation;
            public ImageView View;
        }
    }
}
