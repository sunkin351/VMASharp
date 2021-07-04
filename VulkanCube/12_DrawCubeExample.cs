using System;
using System.Collections.Generic;

using Silk.NET.Windowing;
using Silk.NET.Vulkan;

namespace VulkanCube
{
    public sealed unsafe class DrawCubeExample : GraphicsPipelineExample
    {
        public const int MaxFramesInFlight = 2;

        private CommandBuffer[] SecondaryCommandBuffers;

        private readonly FrameCommandContext[] FrameContexts = new FrameCommandContext[MaxFramesInFlight];

        private int CurrentFrame = 0;

        public DrawCubeExample() : base()
        {
            RecordSecondaryCommandBuffers();

            InitializeFrameContexts();

            if (!this.BufferCopyPromise.IsCompleted)
                this.BufferCopyPromise.Wait();
        }

        public override void Run()
        {
            this.DisplayWindow.Render += this.DrawFrame;

            this.DisplayWindow.Run();

            VkApi.DeviceWaitIdle(this.Device);
        }

        public override void Dispose()
        {
            var primarys = stackalloc CommandBuffer[MaxFramesInFlight];

            for (int i = 0; i < MaxFramesInFlight; ++i)
            {
                ref var ctx = ref this.FrameContexts[i];

                primarys[i] = ctx.CmdBuffer;

                VkApi.DestroyFence(this.Device, ctx.Fence, null);

                VkApi.DestroySemaphore(this.Device, ctx.ImageAvailable, null);
                VkApi.DestroySemaphore(this.Device, ctx.RenderFinished, null);
            }

            VkApi.FreeCommandBuffers(this.Device, this.CommandPool, MaxFramesInFlight, primarys);

            fixed (CommandBuffer* cbuffers = SecondaryCommandBuffers)
            {
                VkApi.FreeCommandBuffers(this.Device, this.CommandPool, (uint)FrameContexts.Length, cbuffers);
            }

            base.Dispose();
        }

        private void DrawFrame(double dTime)
        {
            ref var ctx = ref this.FrameContexts[this.CurrentFrame];

            //Wait for a previous render operation to finish
            VkApi.WaitForFences(this.Device, 1, in ctx.Fence, true, ulong.MaxValue);

            //Acquire the next image index to render to, synchronize when its available
            uint nextImage = 0;
            var res = this.VkSwapchain.AcquireNextImage(this.Device, this.Swapchain, ulong.MaxValue, ctx.ImageAvailable, default, ref nextImage);

            if (res == Result.ErrorOutOfDateKhr)
            {
                this.DisplayWindow.Close(); //Window surface changed size, handling that is outside the scope of this example
                return;
            }
            else if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to acquire next swapchain image!", res);
            }

            //Push semaphores, command buffer, and Pipeline Stage Flags to the stack to allow "fixed-less" addressing

            var waitSemaphore = ctx.ImageAvailable;
            var signalSemaphore = ctx.RenderFinished; //This semaphore will be used to synchronize presentation of the rendered image.

            PipelineStageFlags waitStages = PipelineStageFlags.PipelineStageColorAttachmentOutputBit;

            var buffer = this.RecordPrimaryCommandBuffer(ctx.CmdBuffer, (int)nextImage); //Records primary command buffer on the fly

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
            VkApi.ResetFences(Device, 1, in ctx.Fence);

            //Submit to Graphics queue
            res = VkApi.QueueSubmit(GraphicsQueue, 1, &submitInfo, ctx.Fence);
            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to submit draw command buffer!", res);
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

            this.CurrentFrame = (this.CurrentFrame + 1) % MaxFramesInFlight;
            this.Allocator.CurrentFrameIndex = CurrentFrame;
        }

        private void RecordSecondaryCommandBuffers()
        {
            const uint secondaryCommandBufferCount = 1;

            this.SecondaryCommandBuffers = new CommandBuffer[secondaryCommandBufferCount];

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = CommandPool,
                Level = CommandBufferLevel.Secondary,
                CommandBufferCount = secondaryCommandBufferCount
            };

            fixed (CommandBuffer* commandBuffers = this.SecondaryCommandBuffers)
            {
                var res = VkApi.AllocateCommandBuffers(Device, &allocInfo, commandBuffers);

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Failed to allocate command buffers!", res);
                }
            }

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

            var inherit = new CommandBufferInheritanceInfo
            {
                SType = StructureType.CommandBufferInheritanceInfo,

                RenderPass = this.RenderPass,
                Subpass = 0
            };

            const CommandBufferUsageFlags usageFlags = CommandBufferUsageFlags.CommandBufferUsageRenderPassContinueBit | CommandBufferUsageFlags.CommandBufferUsageSimultaneousUseBit;

            CommandBuffer DrawCommandBuffer = SecondaryCommandBuffers[0];

            BeginCommandBuffer(DrawCommandBuffer, usageFlags, &inherit);

            VkApi.CmdBindPipeline(DrawCommandBuffer, PipelineBindPoint.Graphics, this.GraphicsPipeline);

            VkApi.CmdSetViewport(DrawCommandBuffer, 0, 1, &viewport);
            VkApi.CmdSetScissor(DrawCommandBuffer, 0, 1, &scissor);

            fixed (DescriptorSet* pDescriptorSets = this.DescriptorSets)
            {
                uint setCount = (uint)this.DescriptorSets.Length;
                
                VkApi.CmdBindDescriptorSets(DrawCommandBuffer, PipelineBindPoint.Graphics, this.GraphicsPipelineLayout, 0, setCount, pDescriptorSets, 0, null);
            }

            var vertexBuffer = this.VertexBuffer;
            ulong offset = 0;

            VkApi.CmdBindVertexBuffers(DrawCommandBuffer, 0, 1, &vertexBuffer, &offset);

            vertexBuffer = this.InstanceBuffer;

            VkApi.CmdBindVertexBuffers(DrawCommandBuffer, 1, 1, &vertexBuffer, &offset);

            VkApi.CmdBindIndexBuffer(DrawCommandBuffer, this.IndexBuffer, 0, IndexType.Uint16);

            VkApi.CmdDrawIndexed(DrawCommandBuffer, this.IndexCount, this.InstanceCount, 0, 0, 0);
            EndCommandBuffer(DrawCommandBuffer);
        }

        private void InitializeFrameContexts()
        {
            var buffers = stackalloc CommandBuffer[MaxFramesInFlight];

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = CommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = MaxFramesInFlight
            };

            var res = VkApi.AllocateCommandBuffers(this.Device, &allocInfo, buffers);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to allocate command buffers!", res);
            }

            for (int i = 0; i < MaxFramesInFlight; ++i)
            {
                ref var ctx = ref this.FrameContexts[i];

                ctx.CmdBuffer = buffers[i];

                ctx.Fence = this.CreateFence(true);

                ctx.ImageAvailable = this.CreateSemaphore();

                ctx.RenderFinished = this.CreateSemaphore();
            }
        }

        private CommandBuffer RecordPrimaryCommandBuffer(CommandBuffer primary, int framebufferIndex)
        {
            var res = VkApi.ResetCommandBuffer(primary, 0);

            if (res != Result.Success)
            {

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
                new ClearValue(depthStencil: new ClearDepthStencilValue(1.0f))
            };

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = this.RenderPass,
                Framebuffer = this.FrameBuffers[framebufferIndex],
                RenderArea = new Rect2D(new Offset2D(0, 0), this.SwapchainExtent),
                ClearValueCount = 2,
                PClearValues = clearValues
            };

            BeginCommandBuffer(primary, CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit);

            VkApi.CmdBeginRenderPass(primary, &renderPassInfo, SubpassContents.SecondaryCommandBuffers);

            fixed (CommandBuffer* cmds = this.SecondaryCommandBuffers)
            {
                VkApi.CmdExecuteCommands(primary, (uint)this.SecondaryCommandBuffers.Length, cmds);
            }

            VkApi.CmdEndRenderPass(primary);

            EndCommandBuffer(primary);

            return primary;
        }

        private Semaphore CreateSemaphore()
        {
            var semInfo = new SemaphoreCreateInfo
            {
                SType = StructureType.SemaphoreCreateInfo
            };

            Semaphore sem;
            var res = VkApi.CreateSemaphore(this.Device, &semInfo, null, &sem);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create Semaphore!", res);
            }

            return sem;
        }

        private struct FrameCommandContext
        {
            public CommandBuffer CmdBuffer;
            public Fence Fence;
            public Semaphore ImageAvailable, RenderFinished;
        }
    }
}
