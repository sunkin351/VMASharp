using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe abstract class FrameBuffersExample : ShaderModulesExample
    {
        protected readonly Framebuffer[] FrameBuffers;

        protected FrameBuffersExample() : base()
        {
            FrameBuffers = CreateFrameBuffers();
        }

        public override void Dispose()
        {
            foreach (var fb in this.FrameBuffers)
            {
                VkApi.DestroyFramebuffer(this.Device, fb, null);
            }

            base.Dispose();
        }

        private Framebuffer[] CreateFrameBuffers()
        {
            var attachments = stackalloc ImageView[2] { default, DepthBuffer.View };

            FramebufferCreateInfo createInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = this.RenderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = this.SwapchainExtent.Width,
                Height = this.SwapchainExtent.Height,
                Layers = 1
            };

            Framebuffer[] arr = new Framebuffer[this.SwapchainImages.Length];

            for (int i = 0; i < arr.Length; ++i)
            {
                attachments[0] = this.SwapchainImages[i].View;

                Framebuffer tmp;

                var res = VkApi.CreateFramebuffer(this.Device, &createInfo, null, &tmp);

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Failed to create Shader Module!", res);
                }

                arr[i] = tmp;
            }

            return arr;
        }
    }
}
