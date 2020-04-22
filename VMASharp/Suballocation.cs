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
    }
}
