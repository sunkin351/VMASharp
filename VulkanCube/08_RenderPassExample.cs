using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe abstract class RenderPassExample : DescriptorSetExample
    {
        protected readonly RenderPass RenderPass;

        public RenderPassExample() : base()
        {
            this.RenderPass = CreateRenderpass();
        }

        public override void Dispose()
        {
            VkApi.DestroyRenderPass(this.Device, this.RenderPass, null);

            base.Dispose();
        }

        private RenderPass CreateRenderpass()
        {
            AttachmentDescription* pAttachments = stackalloc AttachmentDescription[2]
            {
                //Color Attachment
                new AttachmentDescription
                {
                    Format = SwapchainImageFormat,
                    Samples = SampleCountFlags.SampleCount1Bit,
                    LoadOp = AttachmentLoadOp.Clear,
                    StoreOp = AttachmentStoreOp.Store,
                    StencilLoadOp = AttachmentLoadOp.DontCare,
                    StencilStoreOp = AttachmentStoreOp.DontCare,
                    InitialLayout = ImageLayout.Undefined,
                    FinalLayout = ImageLayout.PresentSrcKhr
                },

                //Depth Attachment
                new AttachmentDescription
                {
                    Format = DepthFormat,
                    Samples = SampleCountFlags.SampleCount1Bit,
                    LoadOp = AttachmentLoadOp.Clear,
                    StoreOp = AttachmentStoreOp.DontCare,
                    StencilLoadOp = AttachmentLoadOp.DontCare,
                    StencilStoreOp = AttachmentStoreOp.DontCare,
                    InitialLayout = ImageLayout.Undefined,
                    FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
                }
            };

            var colorAttachmentRef = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);

            var depthAttachmentRef = new AttachmentReference(1, ImageLayout.DepthStencilAttachmentOptimal);

            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef,
                PDepthStencilAttachment = &depthAttachmentRef
            };

            var dependency = new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
                DstAccessMask = AccessFlags.AccessColorAttachmentReadBit | AccessFlags.AccessColorAttachmentWriteBit
            };

            var renderPassInfo = new RenderPassCreateInfo
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 2,
                PAttachments = pAttachments,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            RenderPass rPass;
            var res = VkApi.CreateRenderPass(Device, &renderPassInfo, null, &rPass);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create RenderPass!", res);
            }

            return rPass;
        }
    }
}
