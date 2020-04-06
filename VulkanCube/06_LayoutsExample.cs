using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    /// <summary>
    /// 
    /// </summary>
    public unsafe abstract class LayoutsExample : AllocatorAndBuffersExample
    {
        protected readonly DescriptorSetLayout[] DescriptorSetLayouts;
        protected readonly PipelineLayout GraphicsPipelineLayout;

        protected LayoutsExample() : base()
        {
            DescriptorSetLayouts = CreateDescriptorSetLayouts();

            GraphicsPipelineLayout = CreatePipelineLayout();

            //VkApi.descriptors
        }

        public override void Dispose()
        {
            VkApi.DestroyPipelineLayout(this.Device, GraphicsPipelineLayout, null);

            foreach (var layout in DescriptorSetLayouts)
            {
                VkApi.DestroyDescriptorSetLayout(this.Device, layout, null);
            }

            base.Dispose();
        }

        private DescriptorSetLayout[] CreateDescriptorSetLayouts()
        {
            DescriptorSetLayoutBinding binding = new DescriptorSetLayoutBinding
            {
                Binding = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.ShaderStageVertexBit
            };

            DescriptorSetLayoutCreateInfo createInfo = new DescriptorSetLayoutCreateInfo
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = 1,
                PBindings = &binding
            };

            DescriptorSetLayout layout;
            var res = VkApi.CreateDescriptorSetLayout(this.Device, &createInfo, null, &layout);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create Descriptor Set Layout!", res);
            }

            return new[] { layout };
        }

        private PipelineLayout CreatePipelineLayout()
        {
            PipelineLayoutCreateInfo createInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo
            };

            fixed (DescriptorSetLayout* pLayouts = this.DescriptorSetLayouts)
            {
                createInfo.SetLayoutCount = (uint)this.DescriptorSetLayouts.Length;
                createInfo.PSetLayouts = pLayouts;

                PipelineLayout pipelineLayout;
                var res = VkApi.CreatePipelineLayout(this.Device, &createInfo, null, &pipelineLayout);

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Failed to create Pipeline Layout!", res);
                }

                return pipelineLayout;
            }
        }
    }
}
