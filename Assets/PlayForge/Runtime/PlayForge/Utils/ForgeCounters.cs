using Unity.Profiling;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// PlayForge's counters in Unity's Profiler, in its PlayForge category, which the PlayForge Processes and PlayForge GAS
    /// modules chart (FEAT-18, Decision 32). A frame's counts start from 0 each frame, and count every world's work, a
    /// headless run's within the frame among them. A level holds until it changes: the scene's ProcessControl gives the
    /// process levels as its LateUpdate loop ends, and the others follow what starts and ends. Unity compiles them out of
    /// release builds, so they count in the editor and development builds. A ProfilerRecorder reads one by its name in the
    /// category, as "Effects Applied". The markers time PlayForge's work; these say how much of it there is.
    /// </summary>
    public static class ForgeCounters
    {
        /// <summary>The name of the Profiler category PlayForge's counters are in.</summary>
        public const string CategoryName = "PlayForge";

        /// <summary>The Profiler category PlayForge's counters are in.</summary>
        public static readonly ProfilerCategory Category = new(CategoryName);

        private const ProfilerCounterOptions Level = ProfilerCounterOptions.FlushOnEndOfFrame;
        private const ProfilerCounterOptions PerFrame = ProfilerCounterOptions.FlushOnEndOfFrame | ProfilerCounterOptions.ResetToZeroOnFlush;

        // ═══════════════════════════════════════════════════════════════════════════
        // ProcessControl
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The scene's processes running, as its ProcessControl last stepped at LateUpdate.</summary>
        public static readonly ProfilerCounterValue<int> ProcessesRunning = new(Category, "Processes Running", ProfilerMarkerDataUnit.Count, Level);

        /// <summary>The scene's processes waiting.</summary>
        public static readonly ProfilerCounterValue<int> ProcessesWaiting = new(Category, "Processes Waiting", ProfilerMarkerDataUnit.Count, Level);

        /// <summary>The processes the scene's ProcessControl has taken.</summary>
        public static readonly ProfilerCounterValue<int> ProcessesCreated = new(Category, "Processes Created", ProfilerMarkerDataUnit.Count, Level);

        /// <summary>A frame's registrations.</summary>
        public static readonly ProfilerCounterValue<int> Registrations = new(Category, "Registrations", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's terminations: the processes unregistered as they ended.</summary>
        public static readonly ProfilerCounterValue<int> Terminations = new(Category, "Terminations", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's process steps, one for each process at each timing it steps in.</summary>
        public static readonly ProfilerCounterValue<int> Steps = new(Category, "Steps", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's process state changes.</summary>
        public static readonly ProfilerCounterValue<int> StateChanges = new(Category, "State Changes", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's time in the step loops, every timing's.</summary>
        public static readonly ProfilerCounterValue<long> StepTime = new(Category, "Step Time", ProfilerMarkerDataUnit.TimeNanoseconds, PerFrame);

        // ═══════════════════════════════════════════════════════════════════════════
        // GAS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The MonoBehaviour entities running, GameRoot left out.</summary>
        public static readonly ProfilerCounterValue<int> MonoEntities = new(Category, "Mono Entities", ProfilerMarkerDataUnit.Count, Level);

        /// <summary>The runtime entities running, in every world.</summary>
        public static readonly ProfilerCounterValue<int> RuntimeEntities = new(Category, "Runtime Entities", ProfilerMarkerDataUnit.Count, Level);

        /// <summary>The effects on the entities' shelves, as each last stepped.</summary>
        public static readonly ProfilerCounterValue<int> EffectsActive = new(Category, "Effects Active", ProfilerMarkerDataUnit.Count, Level);

        /// <summary>A frame's effect applications, a contained effect's among them.</summary>
        public static readonly ProfilerCounterValue<int> EffectsApplied = new(Category, "Effects Applied", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's effects taken off a shelf.</summary>
        public static readonly ProfilerCounterValue<int> EffectsRemoved = new(Category, "Effects Removed", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's impacts, as each entity received them.</summary>
        public static readonly ProfilerCounterValue<int> Impacts = new(Category, "Impacts", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's ability activations.</summary>
        public static readonly ProfilerCounterValue<int> Activations = new(Category, "Activations", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's refused activations, as each was told.</summary>
        public static readonly ProfilerCounterValue<int> RefusedActivations = new(Category, "Refused Activations", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's tag compiles.</summary>
        public static readonly ProfilerCounterValue<int> TagCompiles = new(Category, "Tag Compiles", ProfilerMarkerDataUnit.Count, PerFrame);

        /// <summary>A frame's attribute recomputes.</summary>
        public static readonly ProfilerCounterValue<int> Recomputes = new(Category, "Recomputes", ProfilerMarkerDataUnit.Count, PerFrame);

        // ═══════════════════════════════════════════════════════════════════════════
        // TaskSequence
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The sequences running, an ability's among them.</summary>
        public static readonly ProfilerCounterValue<int> SequencesRunning = new(Category, "Sequences Running", ProfilerMarkerDataUnit.Count, Level);

        /// <summary>A frame's stages started.</summary>
        public static readonly ProfilerCounterValue<int> StagesStarted = new(Category, "Stages Started", ProfilerMarkerDataUnit.Count, PerFrame);

        // The levels that follow what starts and ends start from 0 each session: statics outlive play mode when domain
        // reload is off (PC-26)
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLevels()
        {
            MonoEntities.Value = 0;
            RuntimeEntities.Value = 0;
            EffectsActive.Value = 0;
            SequencesRunning.Value = 0;
        }

        // The scene's process levels, as its ProcessControl's LateUpdate loop ends
        internal static void SceneLevels(int running, int waiting, int created)
        {
            ProcessesRunning.Value = running;
            ProcessesWaiting.Value = waiting;
            ProcessesCreated.Value = created;
        }

        // A step loop's time, given in Stopwatch ticks
        internal static void AddStepTime(long ticks) =>
            StepTime.Value += ticks * 1_000_000_000L / System.Diagnostics.Stopwatch.Frequency;
    }
}
