using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Silk.NET.Windowing;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Core.Native;

namespace VulkanCube
{
    public unsafe abstract class InstanceCreationExample : ExampleBase
    {
        protected static readonly Vk VkApi;

        static InstanceCreationExample()
        {
            VkApi = Vk.GetApi();
        }

        protected readonly IWindow DisplayWindow;
        protected readonly Instance Instance;
        protected readonly KhrSurface VkSurface;

        protected readonly SurfaceKHR WindowSurface;

        protected InstanceCreationExample()
        {
            this.DisplayWindow = CreateWindow();
            this.Instance = CreateInstance();

            if (!VkApi.TryGetInstanceExtension(this.Instance, out VkSurface))
            {
                throw new Exception("VK_KHR_Surface is missing or not specified");
            }

            this.WindowSurface = this.DisplayWindow.VkSurface.Create<AllocationCallbacks>(this.Instance.ToHandle(), null).ToSurface();
        }

        private static IWindow CreateWindow()
        {
            var options = WindowOptions.DefaultVulkan;

            options.Title = "Hello Cube";
            options.FramesPerSecond = 60;

            Window.PrioritizeGlfw();

            var window = Window.Create(options);

            if (window.VkSurface == null)
                throw new NotSupportedException("Vulkan is not supported.");

            window.Initialize();

            return window;
        }

        private Instance CreateInstance()
        {
            using var appName = SilkMarshal.StringToMemory("Hello Cube");
            using var engineName = SilkMarshal.StringToMemory("Custom Engine");

            var appInfo = new ApplicationInfo
            (
                pApplicationName: (byte*)appName,
                applicationVersion: new Version32(0, 0, 1),
                pEngineName: (byte*)engineName,
                engineVersion: new Version32(0, 0, 1),
                apiVersion: Vk.Version11
            );

            List<string> extensions = new List<string>(GetWindowExtensions())
            {
                Debugging.DebugExtensionString
            };

            string[] layers = new string[] { "VK_LAYER_KHRONOS_validation" };

            using var extList = SilkMarshal.StringArrayToMemory(extensions);
            using var layerList = SilkMarshal.StringArrayToMemory(layers);

            var instInfo = new InstanceCreateInfo(pApplicationInfo: &appInfo,
                                                enabledLayerCount: (uint)layers.Length,
                                                ppEnabledLayerNames: (byte**)layerList,
                                                enabledExtensionCount: (uint)extensions.Count,
                                                ppEnabledExtensionNames: (byte**)extList);

            Instance inst;
            var res = VkApi.CreateInstance(&instInfo, null, &inst);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Instance Creation Failed", res);
            }

            return inst;
        }

        public override void Dispose()
        {
            this.VkSurface.DestroySurface(this.Instance, this.WindowSurface, null);
            
            VkApi.DestroyInstance(this.Instance, null);
            
            this.DisplayWindow.Reset();
        }

        private string[] GetWindowExtensions()
        {
            var ptr = (IntPtr)this.DisplayWindow.VkSurface.GetRequiredExtensions(out uint count);

            string[] arr = new string[count];

            SilkMarshal.CopyPtrToStringArray(ptr, arr);

            return arr;
        }
    }
}
