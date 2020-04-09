using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

using VMASharp;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace VulkanCube
{
    public unsafe abstract class AllocatorAndBuffersExample : CommandPoolCreationExample
    {
        protected const Format DepthFormat = Format.D16Unorm;


        protected readonly VulkanMemoryAllocator Allocator;

        protected Buffer VertexBuffer;
        protected Allocation VertexAllocation;
        protected uint VertexCount;

        protected CameraUniform Camera = new CameraUniform();

        protected Buffer UniformBuffer;
        protected Allocation UniformAllocation;

        protected DepthBufferObject DepthBuffer;

        protected AllocatorAndBuffersExample() : base()
        {
            this.Allocator = CreateAllocator();

            CreateVertexBuffer();
            CreateUniformBuffer();
            CreateDepthBuffer();
        }

        public override void Dispose()
        {
            VkApi.DestroyImageView(this.Device, DepthBuffer.View, null);
            VkApi.DestroyImage(this.Device, DepthBuffer.Image, null);
            DepthBuffer.Allocation.Dispose();

            VkApi.DestroyBuffer(this.Device, UniformBuffer, null);
            UniformAllocation.Dispose();

            VkApi.DestroyBuffer(this.Device, VertexBuffer, null);
            VertexAllocation.Dispose();

            Allocator.Dispose();

            base.Dispose();
        }

        private VulkanMemoryAllocator CreateAllocator()
        {
            uint version;
            var res = VkApi.EnumerateInstanceVersion(&version);

            if (res != Result.Success)
            {
                throw new VulkanResultException("Unable to retrieve instance version", res);
            }

            VulkanMemoryAllocatorCreateInfo createInfo = new VulkanMemoryAllocatorCreateInfo
            {
                Instance = this.Instance,
                PhysicalDevice = this.PhysicalDevice,
                LogicalDevice = this.Device,
                PreferredLargeHeapBlockSize = 64L * 1024 * 1024,
                VulkanAPIVersion = (Version32)version
            };

            VulkanMemoryAllocator.SetAPI(VkApi);

            return new VulkanMemoryAllocator(createInfo);
        }

        private void CreateVertexBuffer()
        {
            Vertex[] data = VertexData.CubeData;
            
            uint graphicsFamily = this.QueueIndices.GraphicsFamily.Value;

            BufferCreateInfo bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Usage = BufferUsageFlags.BufferUsageVertexBufferBit | BufferUsageFlags.BufferUsageTransferDstBit,
                Size = (uint)sizeof(Vertex) * (uint)data.Length,
                SharingMode = SharingMode.Exclusive,
                QueueFamilyIndexCount = 1,
                PQueueFamilyIndices = &graphicsFamily
            };

            AllocationCreateInfo allocInfo = new AllocationCreateInfo
            {
                Usage = MemoryUsage.GPU_Only
            };

            var buffer = this.Allocator.CreateBuffer(bufferInfo, allocInfo, out Allocation allocation);

            bufferInfo.Usage = BufferUsageFlags.BufferUsageTransferSrcBit;
            allocInfo.Usage = MemoryUsage.CPU_Only;
            allocInfo.Flags = AllocationCreateFlags.Mapped;

            var hostBuffer = this.Allocator.CreateBuffer(bufferInfo, allocInfo, out Allocation hostAllocation);

            Span<Vertex> span = new Span<Vertex>(hostAllocation.MappedData.ToPointer(), data.Length);

            data.CopyTo(span);

            TransferBufferData(hostBuffer, buffer, new BufferCopy(0, 0, bufferInfo.Size));

            hostAllocation.Dispose();
            VkApi.DestroyBuffer(this.Device, hostBuffer, null);

            this.VertexBuffer = buffer;
            this.VertexAllocation = allocation;
            this.VertexCount = (uint)data.Length;
        }

        protected uint UniformBufferSize = (uint)sizeof(Matrix4x4) * 2;

        private void CreateUniformBuffer() //Simpler setup from the Vertex buffer because there is no staging or device copying
        {
            BufferCreateInfo bufferInfo = new BufferCreateInfo
            {
                SType = StructureType.BufferCreateInfo,
                Size = this.UniformBufferSize,
                Usage = BufferUsageFlags.BufferUsageUniformBufferBit,
                SharingMode = SharingMode.Exclusive
            };

            //Allow this to be updated every frame
            AllocationCreateInfo allocInfo = new AllocationCreateInfo
            {
                Usage = MemoryUsage.CPU_To_GPU,
                RequiredFlags = MemoryPropertyFlags.MemoryPropertyHostVisibleBit
            };

            //Binds buffer to allocation for you
            var buffer = this.Allocator.CreateBuffer(bufferInfo, allocInfo, out var allocation);

            var lookAt = Matrix4x4.CreateLookAt(new Vector3(0.5f, 0.5f, 0.75f), new Vector3(0, 0, 0), new Vector3(0, -1, 0));

            var scale = Matrix4x4.CreateScale(0.25f);

            var translation = Matrix4x4.CreateTranslation(0, 0, 0.75f);

            //var Projection = Matrix4x4.CreatePerspectiveFieldOfView(1.0f, (float)this.SwapchainExtent.Width / this.SwapchainExtent.Height, 0.5f, 100f);

            //Camera.SetClip(new Matrix4x4(1.0f, 0.0f, 0.0f, 0.0f,
            //                             0.0f, -1.0f, 0.0f, 0.0f,
            //                             0.0f, 0.0f, 0.5f, 0.0f,
            //                             0.0f, 0.0f, 0.5f, 1.0f));

            allocation.Map();

            Matrix4x4* ptr = (Matrix4x4*)allocation.MappedData;

            ptr[0] = lookAt * scale * translation; //View Matrix
            ptr[1] = Matrix4x4.Identity; //Projection Matrix

            allocation.Unmap();

            this.UniformBuffer = buffer;
            this.UniformAllocation = allocation;
        }

        private void CreateDepthBuffer()
        {
            ImageCreateInfo depthInfo = new ImageCreateInfo
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

            ImageViewCreateInfo depthViewInfo = new ImageViewCreateInfo
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

        protected Fence CreateFence(bool initialState = false)
        {
            FenceCreateInfo info = new FenceCreateInfo(flags: initialState ? FenceCreateFlags.FenceCreateSignaledBit : 0);

            Fence fence;
            var res = VkApi.CreateFence(this.Device, &info, null, &fence);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Unable to create Fence!", res);
            }

            return fence;
        }

        protected void TransferBufferData(Buffer source, Buffer destination, BufferCopy copyRegion)
        {
            CommandBuffer cBuffer;

            CommandBufferAllocateInfo cbufferAlloc = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = this.CommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            if (VkApi.AllocateCommandBuffers(this.Device, &cbufferAlloc, &cBuffer) != Result.Success)
            {
                throw new Exception("Unable to allocate command buffer");
            }

            BeginCommandBuffer(cBuffer, CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit);

            VkApi.CmdCopyBuffer(cBuffer, source, destination, 1, &copyRegion);

            EndCommandBuffer(cBuffer);

            SubmitInfo subInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &cBuffer
            };

            Fence fence = this.CreateFence();

            if (VkApi.QueueSubmit(this.GraphicsQueue, 1, &subInfo, fence) != Result.Success)
            {
                throw new Exception("Queue submission failed!");
            }

            VkApi.WaitForFences(this.Device, 1, &fence, true, ulong.MaxValue); //Warning, this returns a Result Value

            VkApi.DestroyFence(this.Device, fence, null);
            VkApi.FreeCommandBuffers(this.Device, this.CommandPool, 1, ref cBuffer);
        }

        protected struct DepthBufferObject
        {
            public Image Image;
            public Allocation Allocation;
            public ImageView View;
        }
    }
}
