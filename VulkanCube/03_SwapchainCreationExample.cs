using System;
using System.Collections.Generic;
using System.Diagnostics;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe abstract class SwapchainCreationExample : DeviceCreationExample
    {
        protected readonly SwapchainKHR Swapchain;
        protected readonly Extent2D SwapchainExtent;
        protected readonly Format SwapchainImageFormat;
        protected readonly SwapchainImage[] SwapchainImages;

        protected int SwapchainImageCount => this.SwapchainImages.Length;

        protected SwapchainCreationExample() : base()
        {
            this.Swapchain = CreateSwapchain(out this.SwapchainExtent, out this.SwapchainImageFormat, out this.SwapchainImages);
        }

        public override void Dispose()
        {
            for (int i = 0; i < this.SwapchainImages.Length; ++i)
            {
                VkApi.DestroyImageView(this.Device, this.SwapchainImages[i].View, null);
            }

            this.VkSwapchain.DestroySwapchain(this.Device, this.Swapchain, null);

            base.Dispose();
        }

        private SwapchainKHR CreateSwapchain(out Extent2D extent, out Format swapImageFormat, out SwapchainImage[] swapImages)
        {
            QuerySwapchainSupport(this.PhysicalDevice, out var details);

            var surfaceFormat = ChooseSwapSurfaceFormat(details.Formats);
            var presentMode = ChooseSwapPresentMode(details.PresentModes);
            extent = ChooseSwapExtent(details.Capabilities);

            var imageCount = details.Capabilities.MinImageCount + 1;

            if (details.Capabilities.MaxImageCount > 0 && imageCount > details.Capabilities.MaxImageCount)
            {
                imageCount = details.Capabilities.MaxImageCount;
            }

            var createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = this.WindowSurface,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ImageUsageColorAttachmentBit
            };

            if (this.QueueIndices.GraphicsFamily != this.QueueIndices.PresentFamily)
            {
                createInfo.ImageSharingMode = SharingMode.Concurrent;
                createInfo.QueueFamilyIndexCount = 2;

                var indices = stackalloc uint[2] { this.QueueIndices.GraphicsFamily.Value, this.QueueIndices.PresentFamily.Value };

                createInfo.PQueueFamilyIndices = indices;
            }
            else
            {
                createInfo.ImageSharingMode = SharingMode.Exclusive;
            }

            createInfo.PreTransform = details.Capabilities.CurrentTransform;
            createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = true;

            createInfo.OldSwapchain = default;

            SwapchainKHR swapchain;

            var res = this.VkSwapchain.CreateSwapchain(Device, &createInfo, null, &swapchain);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to create swapchain!", res);
            }

            uint count = 0;

            res = VkSwapchain.GetSwapchainImages(this.Device, swapchain, &count, null);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Failed to retrieve swapchain images!", res);
            }

            Image[] images = new Image[count];

            fixed (Image* pImages = images)
            {
                res = VkSwapchain.GetSwapchainImages(this.Device, swapchain, &count, pImages);

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Failed to retrieve swapchain images!", res);
                }
            }

            var viewCreateInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                ViewType = ImageViewType.ImageViewType2D,
                Format = surfaceFormat.Format,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ImageAspectColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            var arr = new SwapchainImage[images.Length];

            for (int i = 0; i < arr.Length; ++i)
            {
                viewCreateInfo.Image = images[i];

                ImageView view = default;
                res = VkApi.CreateImageView(this.Device, &viewCreateInfo, null, &view);

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Swapchain image view creation failed!", res);
                }

                arr[i] = new SwapchainImage { Image = images[i], View = view };
            }

            swapImageFormat = surfaceFormat.Format;
            swapImages = arr;

            return swapchain;
        }

        private void QuerySwapchainSupport(PhysicalDevice device, out SwapChainSupportDetails details)
        {
            details = new SwapChainSupportDetails();

            var res = this.VkSurface.GetPhysicalDeviceSurfaceCapabilities(device, this.WindowSurface, out details.Capabilities);

            if (res != Result.Success)
                throw new VMASharp.VulkanResultException("Unable to get Surface Capabilities of this physical device!", res);

            uint count = 0;
            res = this.VkSurface.GetPhysicalDeviceSurfaceFormats(device, this.WindowSurface, &count, null);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Unable to get Surface Formats of this physical device!", res);
            }

            if (count != 0)
            {
                details.Formats = new SurfaceFormatKHR[count];

                fixed (SurfaceFormatKHR* pFormats = details.Formats)
                {
                    res = this.VkSurface.GetPhysicalDeviceSurfaceFormats(device, this.WindowSurface, &count, pFormats);
                }

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Unable to get Surface Formats of this physical device!", res);
                }

                count = 0; //Reset count because its now non-zero
            }
            else
            {
                details.Formats = Array.Empty<SurfaceFormatKHR>();
            }

            res = this.VkSurface.GetPhysicalDeviceSurfacePresentModes(device, this.WindowSurface, &count, null);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Unable to get Surface Present Modes of this physical device!", res);
            }

            if (count != 0)
            {
                details.PresentModes = new PresentModeKHR[count];

                fixed (PresentModeKHR* pPresentModes = details.PresentModes)
                {
                    res = this.VkSurface.GetPhysicalDeviceSurfacePresentModes(device, this.WindowSurface, &count, pPresentModes);
                }

                if (res != Result.Success)
                {
                    throw new VMASharp.VulkanResultException("Unable to get Surface Present Modes of this physical device!", res);
                }
            }
            else
            {
                details.PresentModes = Array.Empty<PresentModeKHR>();
            }
        }

        private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] formats)
        {
            Debug.Assert(formats.Length > 0);

            foreach (var format in formats)
            {
                if (format.Format == Format.B8G8R8A8Unorm)
                {
                    return format;
                }
            }

            return formats[0];
        }

        private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] presentModes)
        {
            foreach (var availablePresentMode in presentModes)
            {
                if (availablePresentMode == PresentModeKHR.PresentModeMailboxKhr)
                {
                    return availablePresentMode;
                }
            }

            return PresentModeKHR.PresentModeFifoKhr;
        }

        private Extent2D ChooseSwapExtent(in SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }

            var WinSize = this.DisplayWindow.Size;

            var width = Math.Clamp((uint)WinSize.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
            var height = Math.Clamp((uint)WinSize.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

            return new Extent2D(width, height);
        }

        protected struct SwapChainSupportDetails
        {
            public SurfaceCapabilitiesKHR Capabilities;
            public SurfaceFormatKHR[] Formats;
            public PresentModeKHR[] PresentModes;
        }

        protected struct SwapchainImage
        {
            public Image Image;
            public ImageView View;
        }
    }
}
