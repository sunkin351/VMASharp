using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace VMASharp
{
    public struct StatInfo
    {
        public int BlockCount, AllocationCount, UnusedRangeCount;
        public long UsedBytes, UnusedBytes;
        public long AllocationSizeMin, AllocationSizeAvg, AllocationSizeMax;
        public long UnusedRangeSizeMin, UnusedRangeSizeAvg, UnusedRangeSizeMax;

        internal static void Init(out StatInfo info)
        {
            info = default;
            info.AllocationSizeMin = long.MaxValue;
            info.UnusedRangeSizeMin = long.MaxValue;
        }

        internal static void Add(ref StatInfo info, in StatInfo other)
        {
            info.BlockCount += other.BlockCount;
            info.AllocationCount += other.AllocationCount;
            info.UnusedRangeCount += other.UnusedRangeCount;
            info.UsedBytes += other.UsedBytes;
            info.UnusedBytes += other.UnusedBytes;

            if (info.AllocationSizeMin > other.AllocationSizeMin)
            {
                info.AllocationSizeMin = other.AllocationSizeMin;
            }

            if (info.AllocationSizeMax < other.AllocationSizeMax)
            {
                info.AllocationSizeMax = other.AllocationSizeMax;
            }

            if (info.UnusedRangeSizeMin > other.UnusedRangeSizeMin)
            {
                info.UnusedRangeSizeMin = other.UnusedRangeSizeMin;
            }

            if (info.UnusedRangeSizeMax < other.UnusedRangeSizeMax)
            {
                info.UnusedRangeSizeMax = other.UnusedRangeSizeMax;
            }
        }

        internal static void PostProcessCalcStatInfo(ref StatInfo info)
        {
            info.AllocationSizeAvg = info.UsedBytes / info.AllocationCount;
            info.UnusedRangeSizeAvg = info.UnusedBytes / info.UnusedRangeCount;
        }
    }

    public class Stats
    {
        public readonly StatInfo[] MemoryType;
        public readonly StatInfo[] MemoryHeap;
        public StatInfo Total;

        public Stats(StatInfo[] memoryTypes, StatInfo[] memoryHeaps, in StatInfo total)
        {
            MemoryType = memoryTypes;
            MemoryHeap = memoryHeaps;
            Total = total;
        }
    }

    public struct PoolStats
    {
        public long Size;

        public long UnusedSize;

        public int AllocationCount;

        public int UnusedRangeCount;

        public long UnusedRangeSizeMax;

        public int BlockCount;
    }
}
