using System;
using System.Collections.Generic;
using System.Text;

namespace VulkanCube
{
    public class Application
    {
        private static void Main(string[] args)
        {
            try
            {
                var app = new DrawCubeExample();

                app.Run();

                app.Dispose();
            }
            catch (VMASharp.VulkanResultException e)
            {
                Console.WriteLine(e);
                Console.WriteLine("\nResult Code: " + e.Result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

    }
}
