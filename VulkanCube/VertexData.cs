using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Numerics;

using Silk.NET.Vulkan;

namespace VulkanCube
{
    [DebuggerDisplay("Position: {Position}, Color: {Color}")]
    public unsafe struct PositionColorVertex
    {
        public static VertexInputBindingDescription BindingDescription = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)sizeof(PositionColorVertex),
            InputRate = VertexInputRate.Vertex
        };

        public static VertexInputAttributeDescription[] AttributeDescriptions = new VertexInputAttributeDescription[]
        {
            new VertexInputAttributeDescription(0, 0, Format.R32G32B32Sfloat, 0),
            new VertexInputAttributeDescription(1, 0, Format.R32G32B32Sfloat, (uint)sizeof(Vector3))
        };

        public Vector3 Position;
        public Vector3 Color;

        public PositionColorVertex(Vector3 position, Vector3 color)
        {
            this.Position = position;
            this.Color = color;
        }

        public override bool Equals(object obj)
        {
            return obj is PositionColorVertex vert && this == vert;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Position, Color);
        }

        public static bool operator ==(PositionColorVertex left, PositionColorVertex right)
        {
            return left.Position == right.Position && left.Color == right.Color;
        }

        public static bool operator !=(PositionColorVertex left, PositionColorVertex right)
        {
            return !(left == right);
        }
    }

    public struct InstanceData
    {
        public Vector3 InstancePosition;

        public InstanceData(Vector3 position)
        {
            InstancePosition = position;
        }
    }

    public static class VertexData
    {
        public static readonly PositionColorVertex[] TriangleData = new PositionColorVertex[]
        {
            new PositionColorVertex
            {
                Position = new Vector3(0, -0.5f, 0),
                Color = new Vector3(1, 0, 0)
            },
            new PositionColorVertex
            {
                Position = new Vector3(0.5f, 0.5f, 0),
                Color = new Vector3(0, 1, 0)
            },
            new PositionColorVertex
            {
                Position = new Vector3(-0.5f, 0.5f, 0),
                Color = new Vector3(0, 0, 1)
            }
        };

        public static readonly PositionColorVertex[] CubeData = new PositionColorVertex[]
        {
            //Face 1
            new PositionColorVertex(new Vector3(-1, -1, -1), new Vector3(0, 0, 0)),
            new PositionColorVertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new PositionColorVertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),

            new PositionColorVertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),
            new PositionColorVertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new PositionColorVertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),

            //Face 2
            new PositionColorVertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new PositionColorVertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new PositionColorVertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),

            new PositionColorVertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),
            new PositionColorVertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new PositionColorVertex(new Vector3(1, 1, 1), new Vector3(1, 1, 1)),

            //Face 3
            new PositionColorVertex(new Vector3(1, 1, 1), new Vector3(1, 1, 1)),
            new PositionColorVertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),
            new PositionColorVertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),

            new PositionColorVertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),
            new PositionColorVertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),
            new PositionColorVertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),

            //Face 4
            new PositionColorVertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new PositionColorVertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new PositionColorVertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),

            new PositionColorVertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),
            new PositionColorVertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new PositionColorVertex(new Vector3(-1, -1, -1), new Vector3(0, 0, 0)),

            //Face 5
            new PositionColorVertex(new Vector3(1, 1, 1), new Vector3(1, 1, 1)),
            new PositionColorVertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new PositionColorVertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),

            new PositionColorVertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),
            new PositionColorVertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
            new PositionColorVertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),

            //Face 6
            new PositionColorVertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),
            new PositionColorVertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new PositionColorVertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),

            new PositionColorVertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
            new PositionColorVertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
            new PositionColorVertex(new Vector3(-1, -1, -1), new Vector3(0, 0, 0))
        };

        public static readonly PositionColorVertex[] IndexedCubeData;
        //= new Vertex[]
        //{
        //    new Vertex(new Vector3(-1, -1, -1), new Vector3(0, 0, 0)),
        //    new Vertex(new Vector3(1, -1, -1), new Vector3(1, 0, 0)),
        //    new Vertex(new Vector3(-1, 1, -1), new Vector3(0, 1, 0)),
        //    new Vertex(new Vector3(1, 1, -1), new Vector3(1, 1, 0)),
        //    new Vertex(new Vector3(-1, -1, 1), new Vector3(0, 0, 1)),
        //    new Vertex(new Vector3(-1, 1, 1), new Vector3(0, 1, 1)),
        //    new Vertex(new Vector3(1, -1, 1), new Vector3(1, 0, 1)),
        //    new Vertex(new Vector3(1, 1, 1), new Vector3(1, 1, 1))
        //};

        public static readonly ushort[] CubeIndexData;
        //= new ushort[]
        //{
        //    0, 1, 2,
        //    2, 1, 3,
        //    4, 5, 6,
        //    6, 5, 7,
        //    7, 3, 6,
        //    6, 3, 1,
        //    5, 4, 2,
        //    2, 4, 0,
        //    7, 5, 3,
        //    3, 5, 2,
        //    6, 1, 4,
        //    4, 1, 0
        //};

        static VertexData()
        {
            (IndexedCubeData, CubeIndexData) = ConvertToIndexedData(CubeData);
        }

        delegate bool VertexCompareEqual(ref PositionColorVertex v0, ref PositionColorVertex v1);
        static readonly VertexCompareEqual _defaultCompareEqual = (ref PositionColorVertex v0, ref PositionColorVertex v1) => v0.Position == v1.Position;

        private static (PositionColorVertex[], ushort[]) ConvertToIndexedData(PositionColorVertex[] data, VertexCompareEqual? compare = null)
        {
            compare ??= _defaultCompareEqual;

            PositionColorVertex[] indexedData = new PositionColorVertex[data.Length];
            ushort[] indexData = new ushort[data.Length];

            indexedData[0] = data[0];

            int vertexDataLength = 1;
            bool found;

            for (int i = 1; i < data.Length; ++i)
            {
                ref var vtx = ref data[i];
                found = false;

                for (int j = 0; j < vertexDataLength; ++j)
                {
                    if (compare(ref vtx, ref indexedData[j]))
                    {
                        indexData[i] = (ushort)j;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    indexData[i] = (ushort)vertexDataLength;
                    indexedData[vertexDataLength++] = vtx;
                }
            }

            return (indexedData.AsSpan(0, vertexDataLength).ToArray(), indexData);
        }
    }
}
