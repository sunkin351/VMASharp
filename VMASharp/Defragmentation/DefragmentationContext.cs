using System;
using System.Collections.Generic;
using System.Text;
using Silk.NET.Vulkan;
using VMASharp;

#nullable enable

namespace VMASharp.Defragmentation
{
    public class DefragmentationContext : IDisposable
    {
        private readonly VulkanMemoryAllocator Allocator;
        private readonly uint currentFrame;
        private readonly uint Flags;
        private DefragmentationStats Stats;

        private ulong MaxCPUBytesToMove, MaxGPUBytesToMove;
        private int MaxCPUAllocationsToMove, MaxGPUAllocationsToMove;

        private readonly BlockListDefragmentationContext[] DefaultPoolContexts = new BlockListDefragmentationContext[Vk.MaxMemoryTypes];
        private readonly List<BlockListDefragmentationContext> CustomPoolContexts = new List<BlockListDefragmentationContext>();


        internal DefragmentationContext(VulkanMemoryAllocator allocator, uint currentFrame, uint flags, DefragmentationStats stats)
        {

        }

        internal void Dispose()
        {

        }

        internal void AddPools(params VulkanMemoryPool[] Pools)
        {

        }

        internal void AddAllocations(Allocation[] allocations, out bool[] allocationsChanged)
        {

        }

        internal Result Defragment(ulong maxCPUBytesToMove, int maxCPUAllocationsToMove, ulong maxGPUBytesToMove,
            int maxGPUAllocationsToMove, CommandBuffer cbuffer, DefragmentationStats stats,
            DefragmentationFlags flags)
        {

        }

        internal Result DefragmentationPassBegin(ref DefragmentationPassMoveInfo[] Info)
        {

        }

        internal Result DefragmentationPassEnd()
        {

        }
    }
}
