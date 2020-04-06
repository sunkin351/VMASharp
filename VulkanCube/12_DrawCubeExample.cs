using System;
using System.Collections.Generic;

using Silk.NET.Windowing.Common;
using Silk.NET.Vulkan;

namespace VulkanCube
{
    public sealed unsafe class DrawCubeExample : GraphicsPipelineExample
    {
        private CommandBuffer[] DrawCommandBuffers;

        private Semaphore ImageAvailableSemaphore;
        private Semaphore RenderFinishedSemaphore;
        private Fence RenderFinishedFence;

        private uint CurrentFrame = 0;

        public DrawCubeExample() : base()
        {
            CreateSyncObjects();

            RecordCommandBuffers();
        }

        public override void Run()
        {
            this.DisplayWindow.Render += this.DrawFrame;

            this.DisplayWindow.Run();

            VkApi.DeviceWaitIdle(this.Device);
        }

        public override void Dispose()
        {
            fixed (CommandBuffer* cbuffers = DrawCommandBuffers)
            {
                VkApi.FreeCommandBuffers(this.Device, this.CommandPool, (uint)DrawCommandBuffers.Length, cbuffers);
            }

            VkApi.DestroySemaphore(this.Device, ImageAvailableSemaphore, null);
            VkApi.DestroySemaphore(this.Device, RenderFinishedSemaphore, null);
            VkApi.DestroyFence(this.Device, RenderFinishedFence, null);

            base.Dispose();
        }

        private void DrawFrame(double dTime)
        {
            //Wait for the previous render operation to finish
            VkApi.WaitForFences(this.Device, 1, ref RenderFinishedFence, true, ulong.MaxValue);

            //Acquire the next image index to render to, synchronize when its available
            uint nextImage = 0;
            var res = this.VkSwapchain.AcquireNextImage(this.Device, this.Swapchain, ulong.MaxValue, this.ImageAvailableSemaphore, default, ref nextImage);

            //Push semaphores, command buffer, and Pipeline Stage Flags to the stack to allow "fixed-less" addressing

            Semaphore waitSemaphore = this.ImageAvailableSemaphore;

            PipelineStageFlags waitStages = PipelineStageFlags.PipelineStageColorAttachmentOutputBit;

            var signalSemaphore = this.RenderFinishedSemaphore; //This semaphore will be used to synchronize presentation of the rendered image.

            var buffer = this.DrawCommandBuffers[nextImage];

            //Fill out queue submit info
            SubmitInfo submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &waitSemaphore,
                PWaitDstStageMask = &waitStages,
                CommandBufferCount = 1,
                PCommandBuffers = &buffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = &signalSemaphore 
            };

            //Reset Fence to unsignaled
            VkApi.ResetFences(Device, 1, ref RenderFinishedFence);

            //Submit to Graphics queue
            if (VkApi.QueueSubmit(GraphicsQueue, 1, &submitInfo, RenderFinishedFence) != Result.Success)
            {
                throw new Exception("failed to submit draw command buffer!");
            }

            //
            fixed (SwapchainKHR* swapchain = &this.Swapchain)
            {
                PresentInfoKHR presentInfo = new PresentInfoKHR
                {
                    SType = StructureType.PresentInfoKhr,
                    WaitSemaphoreCount = 1,
                    PWaitSemaphores = &signalSemaphore,
                    SwapchainCount = 1,
                    PSwapchains = swapchain,
                    PImageIndices = &nextImage
                };

                VkSwapchain.QueuePresent(PresentQueue, &presentInfo);
            }

            //this.CurrentFrame = (this.CurrentFrame + 1) % (uint)this.SwapchainImageCount;
        }

        private void CreateSyncObjects()
        {
            SemaphoreCreateInfo semaphoreInfo = new SemaphoreCreateInfo
            {
                SType = StructureType.SemaphoreCreateInfo
            };

            FenceCreateInfo fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.FenceCreateSignaledBit
            };

            Semaphore sem1, sem2;
            Fence fen;

            var res = VkApi.CreateSemaphore(this.Device, &semaphoreInfo, null, &sem1);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create Semaphore!", res);
            }

            res = VkApi.CreateSemaphore(this.Device, &semaphoreInfo, null, &sem2);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create Semaphore!", res);
            }

            res = VkApi.CreateFence(this.Device, &fenceInfo, null, &fen);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create Fence!", res);
            }

            this.ImageAvailableSemaphore = sem1;
            this.RenderFinishedSemaphore = sem2;
            this.RenderFinishedFence = fen;
        }

        private void RecordCommandBuffers()
        {
            var count = this.SwapchainImageCount;

            this.DrawCommandBuffers = new CommandBuffer[count];

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = CommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = (uint)count
            };

            fixed (CommandBuffer* commandBuffers = this.DrawCommandBuffers)
            {
                var res = VkApi.AllocateCommandBuffers(Device, &allocInfo, commandBuffers);

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Failed to allocate command buffers!", res);
                }
            }

            var clearValues = stackalloc ClearValue[2]
            {
                new ClearValue(new ClearColorValue
                {
                    Float32_0 = 0,
                    Float32_1 = 0,
                    Float32_2 = 0,
                    Float32_3 = 1
                }),
                new ClearValue(depthStencil: new ClearDepthStencilValue())
            };

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = this.RenderPass,
                RenderArea = { Offset = new Offset2D { X = 0, Y = 0 }, Extent = this.SwapchainExtent },
                ClearValueCount = 2,
                PClearValues = clearValues
            };

            var viewport = new Viewport
            {
                X = 0.0f,
                Y = 0.0f,
                Width = SwapchainExtent.Width,
                Height = SwapchainExtent.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };

            var scissor = new Rect2D(default, SwapchainExtent);

            var beginInfo = new CommandBufferBeginInfo(flags: default);
            var vertexBuffer = this.VertexBuffer;
            ulong offset = 0;

            for (var i = 0; i < count; ++i)
            {
                var cbuffer = this.DrawCommandBuffers[i];

                BeginCommandBuffer(cbuffer);

                renderPassInfo.Framebuffer = this.FrameBuffers[i];

                VkApi.CmdBeginRenderPass(cbuffer, &renderPassInfo, SubpassContents.Inline);

                VkApi.CmdBindPipeline(cbuffer, PipelineBindPoint.Graphics, this.GraphicsPipeline);

                VkApi.CmdBindVertexBuffers(cbuffer, 0, 1, &vertexBuffer, &offset);

                VkApi.CmdSetViewport(cbuffer, 0, 1, &viewport);

                VkApi.CmdSetScissor(cbuffer, 0, 1, &scissor);

                VkApi.CmdDraw(cbuffer, 3, 1, 0, 0);

                VkApi.CmdEndRenderPass(cbuffer);

                EndCommandBuffer(cbuffer);
            }
        }
    }
}
