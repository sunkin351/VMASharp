using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe abstract class GraphicsPipelineExample : FrameBuffersExample
    {
        protected readonly Pipeline GraphicsPipeline;

        protected GraphicsPipelineExample() : base()
        {
            GraphicsPipeline = CreateGraphicsPipeline();
        }

        public override void Dispose()
        {
            VkApi.DestroyPipeline(this.Device, GraphicsPipeline, null);

            base.Dispose();
        }

        private Pipeline CreateGraphicsPipeline()
        {
            using var pName = SilkMarshal.StringToMemory("main");

            var shaderStages = stackalloc PipelineShaderStageCreateInfo[2]
            {
                new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ShaderStageVertexBit,
                    Module = this.VertexShader,
                    PName = (byte*)pName
                },

                new PipelineShaderStageCreateInfo
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.ShaderStageFragmentBit,
                    Module = this.FragmentShader,
                    PName = (byte*)pName
                }
            };

            var dynamicStates = stackalloc DynamicState[2] { DynamicState.Viewport, DynamicState.Scissor };

            PipelineDynamicStateCreateInfo dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates
            };

            var vertexBindings = stackalloc VertexInputBindingDescription[2]
            {
                new VertexInputBindingDescription(0, (uint)sizeof(PositionColorVertex), VertexInputRate.Vertex),
                new VertexInputBindingDescription(1, (uint)sizeof(InstanceData), VertexInputRate.Instance)
            };

            var vertexAttributes = stackalloc VertexInputAttributeDescription[3]
            {
                new VertexInputAttributeDescription(0, 0, Format.R32G32B32Sfloat, 0),
                new VertexInputAttributeDescription(1, 0, Format.R32G32B32Sfloat, (uint)sizeof(System.Numerics.Vector3)),
                new VertexInputAttributeDescription(2, 1, Format.R32G32B32Sfloat, 0)
            };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 2,
                PVertexBindingDescriptions = vertexBindings,
                VertexAttributeDescriptionCount = 3,
                PVertexAttributeDescriptions = vertexAttributes
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false
            };

            //Marked as dynamic state, will be specified in the command buffer
            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = Vk.False,
                RasterizerDiscardEnable = Vk.False,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.CullModeNone,
                FrontFace = FrontFace.Clockwise,
                DepthBiasEnable = Vk.False
            };

            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.SampleCount1Bit
            };

            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask = ColorComponentFlags.ColorComponentRBit |
                                 ColorComponentFlags.ColorComponentGBit |
                                 ColorComponentFlags.ColorComponentBBit |
                                 ColorComponentFlags.ColorComponentABit,
                BlendEnable = Vk.False
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = Vk.False,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            colorBlending.BlendConstants[0] = 0.0f;
            colorBlending.BlendConstants[1] = 0.0f;
            colorBlending.BlendConstants[2] = 0.0f;
            colorBlending.BlendConstants[3] = 0.0f;

            var depthState = new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.LessOrEqual,
                DepthBoundsTestEnable = false,
                MinDepthBounds = 0,
                MaxDepthBounds = 0,
                StencilTestEnable = false,
                Back =
                {
                    FailOp = StencilOp.Keep,
                    PassOp = StencilOp.Keep,
                    CompareOp = CompareOp.Always,
                    CompareMask = 0,
                    Reference = 0,
                    DepthFailOp = StencilOp.Keep,
                    WriteMask = 0
                }
            };

            depthState.Front = depthState.Back;

            var pipelineInfo = new GraphicsPipelineCreateInfo
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PDynamicState = &dynamicState,
                PDepthStencilState = &depthState,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                Layout = this.GraphicsPipelineLayout,
                RenderPass = RenderPass,
                Subpass = 0,
                BasePipelineHandle = default
            };

            Pipeline pipeline;
            var res = VkApi.CreateGraphicsPipelines(Device, default, 1, &pipelineInfo, null, &pipeline);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create Graphics Pipeline!", res);
            }

            return pipeline;
        }
    }
}
