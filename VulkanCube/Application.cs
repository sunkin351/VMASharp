using System;
using System.Collections.Generic;
using System.Text;

namespace VulkanCube
{
    public class Application
    {
        private static void Main(string[] args)
        {
            var app = new DrawCubeExample();

            app.Run();

            app.Dispose();
        }

    }
}
