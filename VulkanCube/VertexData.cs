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

        public Vertex(Vector3 position, Vector3 color)
        {
            this.Position = position;
            this.Color = color;
        }
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

        public static Vertex[] CubeData = new Vertex[]
        {
            //Face 1
            new Vertex(new Vector3(-1, -1, -1), new Vector3(0, 0, 0)),
            new Vertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new Vertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),

            new Vertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),
            new Vertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new Vertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),

            //Face 2
            new Vertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new Vertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new Vertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),

            new Vertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),
            new Vertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new Vertex(new Vector3(1, 1, 1), new Vector3(1, 1, 1)),

            //Face 3
            new Vertex(new Vector3(1, 1, 1), new Vector3(1, 1, 1)),
            new Vertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),
            new Vertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),

            new Vertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),
            new Vertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),
            new Vertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),

            //Face 4
            new Vertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new Vertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new Vertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),

            new Vertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),
            new Vertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new Vertex(new Vector3(-1, -1, -1), new Vector3(0, 0, 0)),

            //Face 5
            new Vertex(new Vector3(1, 1, 1), new Vector3(1, 1, 1)),
            new Vertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new Vertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),

            new Vertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),
            new Vertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new Vertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),

            //Face 6
            new Vertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),
            new Vertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new Vertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),

            new Vertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new Vertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new Vertex(new Vector3(-1, -1, -1), new Vector3(0, 0, 0))
        };
    }
}
