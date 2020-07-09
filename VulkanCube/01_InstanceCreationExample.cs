using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Silk.NET.Windowing;
using Silk.NET.Windowing.Common;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Core.Native;

namespace VulkanCube
{
    public unsafe abstract class InstanceCreationExample : ExampleBase
    {
        protected static Vk VkApi;

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
            options.UseSingleThreadedWindow = true;

            var window = Window.Create(options);

            if (window.VkSurface == null)
                throw new NotSupportedException("Vulkan is not supported.");

            window.Initialize();

            return window;
        }

        private Instance CreateInstance()
        {
            SetupAppInfo(out var appInfo);

            string[] extensions = GetWindowExtensions();
            string[] layers = new string[] { "VK_LAYER_KHRONOS_validation" };

            SetupInstanceInfo(&appInfo, extensions, layers, out var instInfo);

            Instance inst;
            var res = VkApi.CreateInstance(&instInfo, null, &inst);

            CleanupInstanceInfo(instInfo);
            CleanupAppInfo(appInfo);

            if (res != Result.Success)
            {
                throw new VMASharp.VulkanResultException("Instance Creation Failed", res);
            }

            return inst;
        }

        private static void SetupAppInfo(out ApplicationInfo appInfo)
        {
            IntPtr appName = Marshal.StringToHGlobalAnsi("Hello Cube");
            IntPtr engineName = Marshal.StringToHGlobalAnsi("Custom Engine");

            appInfo = new ApplicationInfo
            (
                pApplicationName: (byte*)appName,
                applicationVersion: new Version32(0, 0, 1),
                pEngineName: (byte*)engineName,
                engineVersion: new Version32(0, 0, 1),
                apiVersion: Vk.Version11
            );
        }

        private static void SetupInstanceInfo(ApplicationInfo* appInfo, IReadOnlyList<string> extensions, string[] layers, out InstanceCreateInfo createInfo)
        {
            IntPtr extPtr = default, layerPtr = default;
            uint extCount = 0, layerCount = 0;

            if (extensions != null && extensions.Count > 0)
            {
                extPtr = SilkMarshal.MarshalStringArrayToPtr(extensions);
                extCount = (uint)extensions.Count;
            }
            
            if (layers != null && layers.Length > 0)
            {
                layerPtr = SilkMarshal.MarshalStringArrayToPtr(layers);
                layerCount = (uint)layers.Length;
            }

            createInfo = new InstanceCreateInfo(pApplicationInfo: appInfo,
                                                enabledLayerCount: layerCount,
                                                ppEnabledLayerNames: (byte**)layerPtr,
                                                enabledExtensionCount: extCount,
                                                ppEnabledExtensionNames: (byte**)extPtr);
        }

        private static void CleanupAppInfo(in ApplicationInfo appInfo)
        {
            Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
            Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        }

        private static void CleanupInstanceInfo(in InstanceCreateInfo createInfo)
        {
            SilkMarshal.FreeStringArrayPtr((IntPtr)createInfo.PpEnabledLayerNames, (int)createInfo.EnabledLayerCount);
            SilkMarshal.FreeStringArrayPtr((IntPtr)createInfo.PpEnabledExtensionNames, (int)createInfo.EnabledExtensionCount);
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
