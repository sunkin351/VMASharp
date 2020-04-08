using System;
using System.Collections.Generic;
using System.Linq;

using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace VulkanCube
{
    public unsafe abstract class DeviceCreationExample : InstanceCreationExample
    {
        private static readonly string[] RequiredDeviceExtensions = new[] { "VK_KHR_swapchain" };

        protected readonly PhysicalDevice PhysicalDevice;
        protected readonly QueueFamilyIndices QueueIndices;
        protected readonly Device Device;
        protected readonly Queue GraphicsQueue, PresentQueue;
        protected readonly KhrSwapchain VkSwapchain;

        protected DeviceCreationExample() : base()
        {
            this.PhysicalDevice = SelectPhysicalDevice(out this.QueueIndices);
            this.Device = CreateLogicalDevice(out this.GraphicsQueue, out this.PresentQueue);

            VkApi.CurrentDevice = this.Device;

            if (!VkApi.TryGetExtension(out this.VkSwapchain))
            {
                throw new Exception("VK_KHR_Swapchain is missing or not specified");
            }
        }

        public override void Dispose()
        {
            VkSwapchain.Dispose();

            VkApi.DestroyDevice(this.Device, null);

            base.Dispose();
        }

        private PhysicalDevice SelectPhysicalDevice(out QueueFamilyIndices indices)
        {
            uint count = 0;
            var res = VkApi.EnumeratePhysicalDevices(this.Instance, &count, null);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Unable to enumerate physical devices", res);
            }

            if (count == 0)
            {
                throw new Exception("No physical devices found!");
            }

            PhysicalDevice* deviceList = stackalloc PhysicalDevice[(int)count];

            res = VkApi.EnumeratePhysicalDevices(this.Instance, &count, deviceList);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Unable to enumerate physical devices", res);
            }

            for (uint i = 0; i < count; ++i)
            {
                var device = deviceList[i];

                if (IsDeviceSuitable(device, out indices))
                {
                    return device;
                }
            }

            throw new Exception("No suitable device found!");
        }

        private Device CreateLogicalDevice(out Queue GraphicsQueue, out Queue PresentQueue)
        {
            var queueInfos = stackalloc DeviceQueueCreateInfo[2];
            uint infoCount = 1;
            float queuePriority = 1f;

            queueInfos[0] = new DeviceQueueCreateInfo(queueFamilyIndex: (uint)this.QueueIndices.GraphicsFamily, queueCount: 1, pQueuePriorities: &queuePriority);

            if (this.QueueIndices.GraphicsFamily != this.QueueIndices.PresentFamily)
            {
                infoCount = 2;

                queueInfos[1] = new DeviceQueueCreateInfo(queueFamilyIndex: (uint)this.QueueIndices.PresentFamily, queueCount: 1, pQueuePriorities: &queuePriority);
            }

            PhysicalDeviceFeatures features = default;

            var extensionNames = SilkMarshal.MarshalStringArrayToPtr(RequiredDeviceExtensions);

            PhysicalDeviceSeparateDepthStencilLayoutsFeatures depthStencilFeature = new PhysicalDeviceSeparateDepthStencilLayoutsFeatures
            {
                SType = StructureType.PhysicalDeviceSeparateDepthStencilLayoutsFeatures,
                SeparateDepthStencilLayouts = true
            };

            DeviceCreateInfo createInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                PNext = &depthStencilFeature,
                QueueCreateInfoCount = infoCount,
                PQueueCreateInfos = queueInfos,
                EnabledExtensionCount = (uint)RequiredDeviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)extensionNames,
                PEnabledFeatures = &features
            };

            Device device;
            var res = VkApi.CreateDevice(this.PhysicalDevice, &createInfo, null, &device);

            SilkMarshal.FreeStringArrayPtr(extensionNames, RequiredDeviceExtensions.Length);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Logical Device Creation Failed!", res);
            }

            Queue queue = default;
            VkApi.GetDeviceQueue(device, (uint)this.QueueIndices.GraphicsFamily, 0, &queue);

            GraphicsQueue = queue;

            if (this.QueueIndices.GraphicsFamily != this.QueueIndices.PresentFamily)
            {
                queue = default;
                VkApi.GetDeviceQueue(device, (uint)this.QueueIndices.PresentFamily, 0, &queue);
            }

            PresentQueue = queue;

            return device;
        }

        private bool IsDeviceSuitable(PhysicalDevice device, out QueueFamilyIndices indices)
        {
            FindQueueFamilies(device, out indices);

            return indices.IsComplete() && HasAllRequiredExtensions(device) && IsSwapchainSupportAdequate(device);
        }

        private void FindQueueFamilies(PhysicalDevice device, out QueueFamilyIndices indices)
        {
            indices = new QueueFamilyIndices();

            var families = QuerryQueueFamilyProperties(device);

            for (int i = 0; i < families.Length; ++i)
            {
                ref QueueFamilyProperties queueFamily = ref families[i];

                const QueueFlags GraphicsQueueBits = QueueFlags.QueueGraphicsBit | QueueFlags.QueueTransferBit;

                if ((queueFamily.QueueFlags & GraphicsQueueBits) == GraphicsQueueBits)
                {
                    indices.GraphicsFamily = (uint)i;
                }

                var res = VkSurface.GetPhysicalDeviceSurfaceSupport(device, (uint)i, this.WindowSurface, out var presentSupport);

                if (res == Result.Success && presentSupport)
                {
                    indices.PresentFamily = (uint)i;
                }

                if (indices.IsComplete())
                {
                    break;
                }
            }
        }

        private static QueueFamilyProperties[] QuerryQueueFamilyProperties(PhysicalDevice device)
        {
            uint count = 0;
            VkApi.GetPhysicalDeviceQueueFamilyProperties(device, &count, null);

            if (count == 0)
            {
                return Array.Empty<QueueFamilyProperties>();
            }

            var arr = new QueueFamilyProperties[count];

            fixed (QueueFamilyProperties* pProperties = arr)
            {
                VkApi.GetPhysicalDeviceQueueFamilyProperties(device, &count, pProperties);
            }

            return arr;
        }

        private static bool HasAllRequiredExtensions(PhysicalDevice device)
        {
            uint count = 0;
            var res = VkApi.EnumerateDeviceExtensionProperties(device, (byte*)null, &count, null);

            if (res != Result.Success || count == 0)
            {
                return false;
            }

            var pExtensions = stackalloc ExtensionProperties[(int)count];

            res = VkApi.EnumerateDeviceExtensionProperties(device, (byte*)null, &count, pExtensions);

            if (res != Result.Success)
            {
                return false;
            }

            HashSet<string> extensions = new HashSet<string>((int)count, StringComparer.OrdinalIgnoreCase);

            for (uint i = 0; i < count; ++i)
            {
                string name = SilkMarshal.MarshalPtrToString((IntPtr)pExtensions[i].ExtensionName);

                extensions.Add(name);
            }

            foreach (var ext in RequiredDeviceExtensions)
            {
                if (!extensions.Contains(ext))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsSwapchainSupportAdequate(PhysicalDevice device) //If there are either no surface formats or no present modes supported, then this method returns false.
        {
            uint count = 0;

            VkSurface.GetPhysicalDeviceSurfaceFormats(device, this.WindowSurface, &count, null);

            if (count == 0)
            {
                return false;
            }

            count = 0;
            VkSurface.GetPhysicalDeviceSurfacePresentModes(device, this.WindowSurface, &count, null);

            return count != 0;
        }

        protected struct QueueFamilyIndices
        {
            public uint? GraphicsFamily;
            public uint? PresentFamily;

            public bool IsComplete()
            {
                return GraphicsFamily.HasValue && PresentFamily.HasValue;
            }
        }
    }
}
