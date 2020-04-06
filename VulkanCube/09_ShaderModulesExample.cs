using System;
using System.Collections.Generic;
using System.IO;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe abstract class ShaderModulesExample : RenderPassExample
    {
        protected ShaderModule VertexShader, FragmentShader;

        public ShaderModulesExample() : base()
        {
            VertexShader = LoadShaderModule("../../../vert.spv");
            FragmentShader = LoadShaderModule("../../../frag.spv");
        }

        public override void Dispose()
        {
            VkApi.DestroyShaderModule(this.Device, VertexShader, null);
            VkApi.DestroyShaderModule(this.Device, FragmentShader, null);

            base.Dispose();
        }

        private ShaderModule LoadShaderModule(string filename)
        {
            byte[] data = File.ReadAllBytes(filename);

            fixed (byte* pData = data)
            {
                ShaderModuleCreateInfo createInfo = new ShaderModuleCreateInfo
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = new UIntPtr((uint)data.Length),
                    PCode = (uint*)pData
                };

                ShaderModule module;
                var res = VkApi.CreateShaderModule(this.Device, &createInfo, null, &module);

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Failed to create Shader Module!", res);
                }

                return module;
            }
        }
    }
}
