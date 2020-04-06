using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Silk.NET.Windowing;
using Silk.NET.Windowing.Common;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
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

        protected readonly IVulkanWindow DisplayWindow;
        protected readonly Instance Instance;
        protected readonly KhrSurface VkSurface;
        protected readonly SurfaceKHR WindowSurface;

        protected InstanceCreationExample()
        {
            this.DisplayWindow = CreateWindow();
            this.Instance = CreateInstance();

            VkApi.CurrentInstance = this.Instance;

            if (!VkApi.TryGetExtension(out VkSurface))
            {
                throw new Exception("VK_KHR_Surface is missing or not specified");
            }

            this.WindowSurface = this.DisplayWindow.CreateSurface<AllocationCallbacks>(this.Instance.ToHandle(), null).ToSurface();
        }

        private static IVulkanWindow CreateWindow()
        {
            var options = WindowOptions.DefaultVulkan;

            options.Title = "Hello Cube";
            options.FramesPerSecond = 30;
            options.UseSingleThreadedWindow = true;

            if (!(Window.Create(options) is IVulkanWindow window) || !window.IsVulkanSupported)
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
            IntPtr appName, engineName;

            appName = Marshal.StringToHGlobalAnsi("Hello Cube");
            engineName = Marshal.StringToHGlobalAnsi("Custom Engine");

            appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)appName,
                ApplicationVersion = new Version32(0, 0, 1),
                PEngineName = (byte*)engineName,
                EngineVersion = new Version32(0, 0, 1),
                ApiVersion = Vk.Version11
            };
        }

        private static void SetupInstanceInfo(ApplicationInfo* appInfo, string[] Extensions, string[] layers, out InstanceCreateInfo createInfo)
        {
            IntPtr extPtr, layerPtr;

            extPtr = SilkMarshal.MarshalStringArrayToPtr(Extensions);
            layerPtr = SilkMarshal.MarshalStringArrayToPtr(layers);

            createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = appInfo,
                EnabledLayerCount = (uint)layers.Length,
                PpEnabledLayerNames = (byte**)layerPtr,
                EnabledExtensionCount = (uint)Extensions.Length,
                PpEnabledExtensionNames = (byte**)extPtr
            };
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
            byte** ptr = (byte**)this.DisplayWindow.GetRequiredExtensions(out uint count);

            string[] arr = new string[count];

            SilkMarshal.CopyPtrToStringArray((IntPtr)ptr, arr);

            return arr;
        }
    }
}
