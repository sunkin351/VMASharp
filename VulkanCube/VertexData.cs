using System;
using System.Collections.Generic;
using System.Text;

using System.Numerics;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    public unsafe struct Vertex
    {
        public static VertexInputBindingDescription BindingDescription = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)sizeof(Vertex),
            InputRate = VertexInputRate.Vertex
        };

        public static VertexInputAttributeDescription[] AttributeDescriptions = new VertexInputAttributeDescription[]
        {
            new VertexInputAttributeDescription(0, 0, Format.R32G32B32Sfloat, 0),
            new VertexInputAttributeDescription(1, 0, Format.R32G32B32Sfloat, (uint)sizeof(Vector3))
        };

        public Vector3 Position;

        public Vector3 Color;
    }

    public static class VertexData
    {
        public static Vertex[] TriangleData = new Vertex[]
        {
            new Vertex
            {
                Position = new Vector3(0, -0.5f, 0),
                Color = new Vector3(1, 0, 0)
            },
            new Vertex
            {
                Position = new Vector3(0.5f, 0.5f, 0),
                Color = new Vector3(0, 1, 0)
            },
            new Vertex
            {
                Position = new Vector3(-0.5f, 0.5f, 0),
                Color = new Vector3(0, 0, 1)
            }
        };

    }
}
