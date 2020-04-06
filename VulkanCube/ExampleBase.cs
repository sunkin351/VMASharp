#pragma warning disable CA1063
using System;

namespace VulkanCube
{
    public abstract class ExampleBase : IDisposable
    {
        public abstract void Run();

        public abstract void Dispose();
    }
}
