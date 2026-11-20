using System;
using Unity.Profiling.Editor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// The PlayForge GAS module in Unity's Profiler: the GAS's counters, charted: its entities by
    /// hub, its effects, impacts, activations and refusals, and its tag compiles and recomputes.
    /// </summary>
    [Serializable]
    [ProfilerModuleMetadata("PlayForge GAS")]
    public class PlayForgeGASProfilerModule : ProfilerModule
    {
        /// <summary>The counters it charts, ten at most.</summary>
        public static readonly ProfilerCounterDescriptor[] Counters =
        {
            new("Mono Entities", ForgeCounters.CategoryName),
            new("Runtime Entities", ForgeCounters.CategoryName),
            new("Effects Active", ForgeCounters.CategoryName),
            new("Effects Applied", ForgeCounters.CategoryName),
            new("Effects Removed", ForgeCounters.CategoryName),
            new("Impacts", ForgeCounters.CategoryName),
            new("Activations", ForgeCounters.CategoryName),
            new("Refused Activations", ForgeCounters.CategoryName),
            new("Tag Compiles", ForgeCounters.CategoryName),
            new("Recomputes", ForgeCounters.CategoryName),
        };

        public PlayForgeGASProfilerModule() : base(Counters) { }
    }
}
