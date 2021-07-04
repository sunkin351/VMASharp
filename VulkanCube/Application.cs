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
            catch (Exception e)
            {
                Console.WriteLine(e);
                
                if (e is VMASharp.VulkanResultException ve)
                    Console.WriteLine("\nResult Code: " + ve.Result);
            }
        }

    }
}
