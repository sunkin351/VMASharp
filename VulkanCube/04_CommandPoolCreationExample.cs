using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe abstract class CommandPoolCreationExample : SwapchainCreationExample
    {
        protected readonly CommandPool CommandPool;

        protected CommandPoolCreationExample() : base()
        {
            this.CommandPool = CreateCommandPool();
        }

        public override void Dispose()
        {
            VkApi.DestroyCommandPool(this.Device, this.CommandPool, null);

            base.Dispose();
        }

        private CommandPool CreateCommandPool()
        {
            CommandPoolCreateInfo poolCreateInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.CommandPoolCreateResetCommandBufferBit,
                QueueFamilyIndex = this.QueueIndices.GraphicsFamily.Value
            };

            CommandPool pool;
            var res = VkApi.CreateCommandPool(this.Device, &poolCreateInfo, null, &pool);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Command Pool Creation Failed!", res);
            }

            return pool;
        }

        //Helper methods for other examples
        protected static void BeginCommandBuffer(CommandBuffer buffer, CommandBufferUsageFlags flags = default)
        {
            CommandBufferBeginInfo info = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = flags
            };

            var res = VkApi.BeginCommandBuffer(buffer, &info);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to begin Command Buffer recording!", res);
            }
        }

        protected static void EndCommandBuffer(CommandBuffer buffer)
        {
            var res = VkApi.EndCommandBuffer(buffer);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to begin Command Buffer recording!", res);
            }
        }
    }
}
