using Unity.Profiling;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// PlayForge's markers in Unity's Profiler, so what it runs shows there by its own name: a process's step, a GAS's step
    /// and its end of frame, an effect's application and an ability's activation. Runtime had none, so PlayForge's cost
    /// showed only as its callers' (FEAT-18). Each is a sample in the Scripts category, which a ProfilerRecorder can read by
    /// its name, as "PlayForge.ApplyEffect".
    /// </summary>
    public static class ForgeProfiler
    {
        /// <summary>One process's step, in the timing ProcessControl steps it in.</summary>
        public static readonly ProfilerMarker ProcessStep = new(ProfilerCategory.Scripts, "PlayForge.ProcessStep");

        /// <summary>A GAS's step: its clock, its effects' ticks, its While active checks and its input buffer.</summary>
        public static readonly ProfilerMarker GASStep = new(ProfilerCategory.Scripts, "PlayForge.GASStep");

        /// <summary>A GAS's end of frame: its queued actions, its tag and analysis workers, and its frame's summary.</summary>
        public static readonly ProfilerMarker EndOfFrame = new(ProfilerCategory.Scripts, "PlayForge.EndOfFrame");

        /// <summary>An effect's application to its target, its contained effects' within it.</summary>
        public static readonly ProfilerMarker ApplyEffect = new(ProfilerCategory.Scripts, "PlayForge.ApplyEffect");

        /// <summary>An ability's activation, up to the first of its tasks that waits.</summary>
        public static readonly ProfilerMarker ActivateAbility = new(ProfilerCategory.Scripts, "PlayForge.ActivateAbility");
    }
}
