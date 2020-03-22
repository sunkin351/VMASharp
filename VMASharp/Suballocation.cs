using System;
using System.Collections.Generic;
using System.Text;
using VMASharp;

namespace VMASharp
{
    internal class Suballocation
    {
        public long Offset, Size;
        public BlockAllocation Allocation;
        public SuballocationType Type;

        public delegate int Comparer(Suballocation alloc1, Suballocation alloc2);

        public static Comparer CompareOffsetLess = (alloc1, alloc2) =>
        {
            return alloc1.Offset.CompareTo(alloc2.Offset);
        };

        public static Comparer CompareOffsetGreater = (alloc1, alloc2) =>
        {
            return alloc2.Offset.CompareTo(alloc1.Offset);
        };
    }
}
