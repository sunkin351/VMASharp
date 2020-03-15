using System;
using System.Collections.Generic;
using System.Text;

using Silk.NET.Vulkan;

namespace VMASharp
{
    public class AllocationException : ApplicationException
    {
        public readonly Result? Result;

        public AllocationException(string message) : base (message)
        {
        }

        public AllocationException(Result res) : base("Vulkan returned an API error code")
        {
            Result = res;
        }

        public AllocationException(string message, Result res) : base (message)
        {
            Result = res;
        }
    }

    public class DefragmentationException : ApplicationException
    {
        public readonly Result? Result;

        public DefragmentationException(string message) : base(message)
        {
        }

        public DefragmentationException(Result res) : base("Vulkan returned an API error code")
        {
            Result = res;
        }

        public DefragmentationException(string message, Result res) : base(message)
        {
            Result = res;
        }
    }
}
