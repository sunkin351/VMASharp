using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe abstract class DescriptorSetExample : LayoutsExample
    {
        protected readonly DescriptorPool DescriptorPool;
        protected readonly DescriptorSet[] DescriptorSets;

        protected DescriptorSetExample() : base()
        {
            this.DescriptorPool = CreateDescriptorPool();

            this.DescriptorSets = AllocateDescriptorSets();

            DescriptorBufferInfo info = new DescriptorBufferInfo
            {
                Buffer = UniformBuffer,
                Offset = 0,
                Range = this.UniformBufferSize
            };

            WriteDescriptorSet write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = this.DescriptorSets[0],
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &info,
                DstArrayElement = 0,
                DstBinding = 0
            };

            VkApi.UpdateDescriptorSets(this.Device, 1, &write, 0, null);
        }

        public override void Dispose()
        {
            VkApi.FreeDescriptorSets(this.Device, this.DescriptorPool, (uint)this.DescriptorSets.Length, ref DescriptorSets[0]);

            VkApi.DestroyDescriptorPool(this.Device, this.DescriptorPool, null);

            base.Dispose();
        }

        private DescriptorPool CreateDescriptorPool()
        {
            DescriptorPoolSize typeCount = new DescriptorPoolSize
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = 1
            };

            DescriptorPoolCreateInfo createInfo = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                MaxSets = 1,
                PoolSizeCount = 1,
                PPoolSizes = &typeCount,
                Flags = DescriptorPoolCreateFlags.DescriptorPoolCreateFreeDescriptorSetBit
            };

            DescriptorPool pool;

            var res = VkApi.CreateDescriptorPool(this.Device, &createInfo, null, &pool);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create Descriptor Pool!", res);
            }

            return pool;
        }

        private DescriptorSet[] AllocateDescriptorSets()
        {
            fixed (DescriptorSetLayout* pLayouts = this.DescriptorSetLayouts)
            {
                DescriptorSetAllocateInfo allocInfo = new DescriptorSetAllocateInfo
                {
                    SType = StructureType.DescriptorSetAllocateInfo,
                    DescriptorPool = this.DescriptorPool,
                    DescriptorSetCount = (uint)this.DescriptorSetLayouts.Length,
                    PSetLayouts = pLayouts
                };

                var arr = new DescriptorSet[this.DescriptorSetLayouts.Length];

                fixed (DescriptorSet* pSets = arr)
                {
                    var res = VkApi.AllocateDescriptorSets(this.Device, &allocInfo, pSets);

                    if (res != Result.Success)
                    {
                        throw new VMASharp.VulkanResultException("Failed to allocate Descriptor Sets!", res);
                    }

                    return arr;
                }
            }
        }
    }
}
