using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// PlayForge's markers in Unity's Profiler, so what it runs shows there by its own name: the step loop at each timing,
    /// a process's step, under one of its type's too, a GAS's step and its end of frame, an effect's application, an
    /// ability's activation, a tag compile, an attribute's recompute and a stage's start. Runtime had none, so PlayForge's
    /// cost showed only as its callers' (FEAT-18, Decision 32). Each is a sample in the Scripts category, which a
    /// ProfilerRecorder can read by its name, as "PlayForge.ApplyEffect".
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

        /// <summary>The step loop at Update: each process that steps then, in step order.</summary>
        public static readonly ProfilerMarker StepUpdate = new(ProfilerCategory.Scripts, "PlayForge.Update");

        /// <summary>The step loop at LateUpdate, where a GAS's end of frame runs.</summary>
        public static readonly ProfilerMarker StepLateUpdate = new(ProfilerCategory.Scripts, "PlayForge.LateUpdate");

        /// <summary>The step loop at FixedUpdate, the physics step's.</summary>
        public static readonly ProfilerMarker StepFixedUpdate = new(ProfilerCategory.Scripts, "PlayForge.FixedUpdate");

        /// <summary>A GAS's tag compile: its tags rebuilt from their sources.</summary>
        public static readonly ProfilerMarker TagCompile = new(ProfilerCategory.Scripts, "PlayForge.TagCompile");

        /// <summary>An attribute's recompute, from its root through its modifiers to its bounds.</summary>
        public static readonly ProfilerMarker Recompute = new(ProfilerCategory.Scripts, "PlayForge.Recompute");

        /// <summary>A sequence stage's start, an ability's among them: its tasks, up to the first that waits.</summary>
        public static readonly ProfilerMarker Stage = new(ProfilerCategory.Scripts, "PlayForge.Stage");

        // The step loop's marker at a timing
        internal static ProfilerMarker StepLoop(EProcessStepTiming timing) => timing switch
        {
            EProcessStepTiming.LateUpdate => StepLateUpdate,
            EProcessStepTiming.FixedUpdate => StepFixedUpdate,
            _ => StepUpdate,
        };

        // Each process type's step marker, made the first time a process of the type steps, and kept
        private static readonly Dictionary<Type, ProfilerMarker<int>> steps = new();

        /// <summary>
        /// The step marker of a process's type, named "PlayForge.Step." and the type's name, within ProcessStep, with the
        /// process's ID as its metadata, so the Profiler tells which process cost what. Made the first time a process of
        /// the type steps, and kept.
        /// </summary>
        public static ProfilerMarker<int> StepOf(Type type)
        {
            if (!steps.TryGetValue(type, out var marker))
            {
                marker = new ProfilerMarker<int>(ProfilerCategory.Scripts, "PlayForge.Step." + type.Name, "Process");
                steps[type] = marker;
            }
            return marker;
        }
    }
}
