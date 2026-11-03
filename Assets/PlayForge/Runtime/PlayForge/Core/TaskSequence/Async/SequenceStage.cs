using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════
    // SEQUENCE EVENT CONTEXT
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Context passed to sequence event callbacks.
    /// </summary>
    public readonly struct SequenceEventContext
    {
        /// <summary>The data packet.</summary>
        public readonly SequenceDataPacket Data;
        
        /// <summary>The runtime (null for definition-level events before runtime exists).</summary>
        public readonly TaskSequenceRuntime Runtime;
        
        /// <summary>The stage where the event occurred (null for sequence-level events).</summary>
        public readonly SequenceStage Stage;
        
        /// <summary>Elapsed time since sequence started.</summary>
        public readonly float ElapsedTime;
        
        /// <summary>Current stage index.</summary>
        public readonly int StageIndex;
        
        public SequenceEventContext(
            SequenceDataPacket data,
            TaskSequenceRuntime runtime,
            SequenceStage stage = null)
        {
            Data = data;
            Runtime = runtime;
            Stage = stage;
            ElapsedTime = runtime?.ElapsedTime ?? 0f;
            StageIndex = runtime?.StageIndex ?? -1;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // SEQUENCE STAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A stage within a TaskSequence containing tasks and/or sub-stages.
    /// </summary>
    public class SequenceStage
    {
        /// <summary>
        /// Tasks to execute in this stage.
        /// </summary>
        public List<ISequenceTask> Tasks { get; } = new();
        
        /// <summary>
        /// Sub-stages to execute in this stage (treated as task units for policy).
        /// </summary>
        public List<SequenceStage> SubStages { get; } = new();
        
        /// <summary>
        /// How this stage completes (WhenAll, WhenAny, etc.).
        /// </summary>
        public ISequenceStagePolicy Policy { get; set; } = WhenAllStagePolicy.Instance;

        /// <summary>
        /// Optional timeout for this stage. If exceeded, TimeoutInjection is applied.
        /// </summary>
        public List<SequenceTimeout> Timeouts { get; } = new();
        
        /// <summary>
        /// Optional metadata for debugging/logging.
        /// </summary>
        public SequenceStageMetadata Metadata { get; set; }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // REPEAT SUPPORT
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// If true, stage repeats after completion until broken or interrupted.
        /// Works similar to sequence-level repeat but for a single stage.
        /// </summary>
        public bool Repeat { get; set; }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE-LOCAL INJECTION SETTINGS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Stage-local conditions checked while this stage is active.
        /// These conditions respect stage-local AllowedInjections.
        /// </summary>
        public List<SequenceCondition> Conditions { get; } = new();
        
        /// <summary>
        /// Stage-local allowed injection types. If null, inherits from sequence.
        /// If empty set, no injections are allowed for this stage.
        /// </summary>
        public HashSet<Type> AllowedInjections { get; set; }
        
        /// <summary>
        /// Maximum duration for this stage. Null = no limit.
        /// </summary>
        public float? MaxDurationSeconds { get; set; }
        
        /// <summary>
        /// Injection to apply when max duration is exceeded. Defaults to InterruptSequenceInjection.
        /// </summary>
        public ISequenceInjection MaxDurationInjection { get; set; } = InterruptSequenceInjection.Instance;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EVENT CALLBACKS (all sync - called immediately when event occurs)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Called when this stage's timeout is triggered, before the injection is applied.
        /// </summary>
        public Action<SequenceEventContext, SequenceTimeout> OnTimeout { get; set; }
        
        /// <summary>
        /// Called when this stage's max duration is reached, before the injection is applied.
        /// </summary>
        public Action<SequenceEventContext> OnMaxDuration { get; set; }
        
        /// <summary>
        /// Called when an exception occurs in this stage.
        /// Return true to suppress the exception (stage will be skipped), false to propagate.
        /// </summary>
        public Func<SequenceEventContext, Exception, bool> OnException { get; set; }
        
        /// <summary>
        /// Called when a stage repeat iteration completes (before checking if should repeat again).
        /// </summary>
        public Action<SequenceEventContext> OnRepeat { get; set; }
        public Action<SequenceEventContext, bool> OnTerminate { get; set; }
        public Action<SequenceDataPacket> OnInit { get; set; }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns the total number of executable units (tasks + sub-stages).
        /// </summary>
        public int UnitCount => Tasks.Count + SubStages.Count;
        
        /// <summary>
        /// Returns true if this stage has any executable content.
        /// </summary>
        public bool HasContent => Tasks.Count > 0 || SubStages.Count > 0;
        
        /// <summary>
        /// Returns true if this stage has conditions that need checking.
        /// </summary>
        public bool HasConditions => Conditions != null && Conditions.Count > 0;
        
        /// <summary>
        /// Returns true if this stage has a max duration limit.
        /// </summary>
        public bool HasMaxDuration => MaxDurationSeconds.HasValue;
        public bool HasTimeouts => Timeouts.Count > 0;
        
        /// <summary>
        /// Returns true if this stage should repeat.
        /// </summary>
        public bool ShouldRepeat => Repeat || Policy is RepeatUntilSkippedPolicy;
        
        /// <summary>
        /// Checks if a given injection type is allowed at this stage level.
        /// Returns null if no stage-level restriction (inherit from sequence).
        /// </summary>
        public bool? IsInjectionAllowedLocal(ISequenceInjection injection)
        {
            if (AllowedInjections == null) return null; // Inherit from sequence
            return AllowedInjections.Contains(injection.GetType());
        }
        
        /// <summary>
        /// Creates all execution units (tasks wrapped, sub-stages as recursive executions).
        /// </summary>
        internal UniTask[] CreateExecutionUnits(
            TaskSequenceRuntime runtime,
            SequenceStage stage,
            SequenceDataPacket data,
            CancellationToken stageToken)
        {
            var units = new List<UniTask>();
            
            // Add task executions
            foreach (var task in stage.Tasks)
            {
                units.Add(ExecuteTaskUnit(task, data, stageToken));
            }
            
            // Add sub-stage executions. Each runs as a stage, with its own settings (TS-N)
            foreach (var subStage in stage.SubStages)
            {
                units.Add(runtime.ExecuteSubStage(subStage, stageToken));
            }
            
            return units.ToArray();
        }
        
        private async UniTask ExecuteTaskUnit(ISequenceTask task, SequenceDataPacket data, CancellationToken token)
        {
            try
            {
                // data.AppendPath(task.GetType().Name);
                await task.Execute(data, token);
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // STAGE METADATA
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Optional metadata for a stage.
    /// </summary>
    public class SequenceStageMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool EnableErrorLogging { get; set; } = true;
        public bool EnableInjectionLogging { get; set; } = false;

        public bool IsCritical { get; set; } = false;
        public HashSet<Type> CriticalAllowedInjections { get; set; }
    }

    public class SequenceTimeout
    {
        public float Seconds { get; set; }
        public ISequenceInjection Injection { get; set; } = SkipStageInjection.Instance;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // BRANCH CASE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A condition-stage pair for branching.
    /// </summary>
    public class BranchCase
    {
        public Func<SequenceDataPacket, bool> Condition { get; }
        public SequenceStage Stage { get; }
        
        public BranchCase(Func<SequenceDataPacket, bool> condition, SequenceStage stage)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // BRANCH STAGE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A stage that branches based on conditions.
    /// </summary>
    public class BranchStage : SequenceStage
    {
        /// <summary>
        /// Ordered list of condition-stage pairs. First matching condition wins.
        /// </summary>
        public List<BranchCase> Branches { get; } = new();
        
        /// <summary>
        /// Default stage if no conditions match. Can be null.
        /// </summary>
        public SequenceStage DefaultBranch { get; set; }
        
        /// <summary>
        /// Evaluates conditions and returns the matching stage.
        /// </summary>
        public SequenceStage Evaluate(ProcessDataPacket data)
        {
            var seqData = data as SequenceDataPacket ?? SequenceDataPacket.SceneRoot();
            
            foreach (var branch in Branches)
            {
                if (branch.Condition(seqData))
                {
                    return branch.Stage;
                }
            }
            
            return DefaultBranch;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // SEQUENCE DEFINITION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// The complete definition of a TaskSequence. Immutable after building.
    /// Can be reused across multiple runs.
    /// </summary>
    public class TaskSequenceDefinition
    {
        /// <summary>
        /// Top-level stages executed sequentially.
        /// </summary>
        public IReadOnlyList<SequenceStage> Stages { get; }
        
        /// <summary>
        /// Global conditions checked each update tick for auto-injection.
        /// </summary>
        public IReadOnlyList<SequenceCondition> Conditions { get; }
        
        /// <summary>
        /// Global allowed injection types. If null, all injections are allowed.
        /// </summary>
        public HashSet<Type> AllowedInjections { get; }
        
        /// <summary>
        /// Maximum duration for the entire sequence. Null = no limit.
        /// </summary>
        public float? MaxDurationSeconds { get; }
        
        /// <summary>
        /// Injection to apply when max duration is exceeded.
        /// </summary>
        public ISequenceInjection MaxDurationInjection { get; }
        
        /// <summary>
        /// Maximum duration for the entire sequence. Null = no limit.
        /// </summary>
        public IReadOnlyList<SequenceTimeout> Timeouts { get; }
        
        /// <summary>
        /// Optional metadata for the sequence.
        /// </summary>
        public SequenceMetadata Metadata { get; }
        
        /// <summary>
        /// Returns true if this sequence has global conditions that need update checking.
        /// </summary>
        public bool HasConditions => Conditions != null && Conditions.Count > 0;
        
        /// <summary>
        /// Returns true if any stage has conditions that need checking.
        /// </summary>
        public bool HasAnyStageConditions
        {
            get
            {
                // A loop, not Any(method group): this is read for every sequence run, and the delegate
                // and enumerator allocated each time (PF-007)
                if (Stages == null) return false;
                for (int i = 0; i < Stages.Count; i++)
                {
                    if (StageHasConditionsRecursive(Stages[i])) return true;
                }
                return false;
            }
        }
        
        /// <summary>
        /// Returns true if this sequence or any stage has conditions.
        /// </summary>
        public bool HasAnyConditions => HasConditions || HasAnyStageConditions;
        
        /// <summary>
        /// Returns true if this sequence has a max duration limit.
        /// </summary>
        public bool HasMaxDuration => MaxDurationSeconds.HasValue;

        public bool HasTimeout => Timeouts.Count > 0;
        
        /// <summary>
        /// Timing for condition checking (Update, FixedUpdate, LateUpdate, etc).
        /// </summary>
        public EProcessStepTiming ConditionStepTiming { get; }
        
        internal TaskSequenceDefinition(
            List<SequenceStage> stages,
            List<SequenceCondition> conditions,
            HashSet<Type> allowedInjections,
            float? maxDurationSeconds,
            ISequenceInjection maxDurationInjection,
            List<SequenceTimeout> timeouts,
            EProcessStepTiming conditionStepTiming,
            SequenceMetadata metadata)
        {
            Stages = stages?.AsReadOnly() ?? new List<SequenceStage>().AsReadOnly();
            Conditions = conditions?.AsReadOnly() ?? new List<SequenceCondition>().AsReadOnly();
            AllowedInjections = allowedInjections;
            MaxDurationSeconds = maxDurationSeconds;
            MaxDurationInjection = maxDurationInjection ?? InterruptSequenceInjection.Instance;
            Timeouts = timeouts;
            ConditionStepTiming = conditionStepTiming;
            Metadata = metadata;
        }

        internal void FixRepeatStages(List<SequenceStage> stages)
        {
            
        }
        
        /// <summary>
        /// Checks if a given injection type is allowed globally.
        /// </summary>
        public bool IsInjectionAllowed(ISequenceInjection injection)
        {
            if (AllowedInjections == null) return true;
            return AllowedInjections.Contains(injection.GetType());
        }
        
        /// <summary>
        /// Checks if injection is allowed considering both global and stage-local settings.
        /// Stage-local settings override global if present.
        /// </summary>
        public bool IsInjectionAllowed(ISequenceInjection injection, SequenceStage stage)
        {
            // Check stage-local first
            var stageAllowed = stage?.IsInjectionAllowedLocal(injection);
            if (stageAllowed.HasValue)
            {
                return stageAllowed.Value;
            }
            
            // Fall back to global
            return IsInjectionAllowed(injection);
        }
        
        /// <summary>
        /// Builds a SyncedTaskSequence from this definition for per-frame stepping. A runner steps tasks and has no
        /// runtime to inject into, so what needs one can't come across. Each such thing is named in a warning as the
        /// runner is built, where it used to be left out without a word (TS-Q).
        /// </summary>
        public SyncedTaskSequence BuildSyncRunner()
        {
            var name = Metadata?.Name ?? "SyncSequence";
            bool wholeSequenceIsCritical = Metadata?.IsCritical ?? false;

            if (HasConditions) WarnLeftOut(name, "its conditions");
            if (HasTimeout) WarnLeftOut(name, "its timeouts");
            if (HasMaxDuration) WarnLeftOut(name, "its max duration");
            if (Metadata?.OnException != null) WarnLeftOut(name, "its OnException handler");

            var syncStages = new List<SyncSequenceStage>();

            for (int i = 0; i < Stages.Count; i++)
            {
                var stage = Stages[i];
                string stageName = stage.Metadata?.Name ?? "stage " + i;
                syncStages.Add(BuildSyncStage(stage, name + ", " + stageName, wholeSequenceIsCritical));
            }

            var runner = new SyncedTaskSequence(syncStages, name, Metadata?.Repeat ?? false);

            var onComplete = Metadata?.OnComplete;
            if (onComplete != null) runner.OnComplete = (data, completed) => onComplete(new SequenceEventContext(data, null), completed);

            return runner;
        }

        private static SyncSequenceStage BuildSyncStage(SequenceStage stage, string owner, bool wholeSequenceIsCritical)
        {
            if (stage is BranchStage) WarnLeftOut(owner, "its branches");
            if (stage.SubStages.Count > 0) WarnLeftOut(owner, "its sub-stages");
            if (stage.HasConditions) WarnLeftOut(owner, "its conditions");
            if (stage.OnException != null) WarnLeftOut(owner, "its OnException handler");
            if (stage.OnTimeout != null || stage.OnMaxDuration != null) WarnLeftOut(owner, "its timeout handlers");
            if (stage.Tasks.Exists(task => task is DelegateSequenceTask))
            {
                ForgeLog.Warning($"{owner}: its async-only tasks do nothing in a synchronous sequence, and finish at once.");
            }

            var policy = ESyncStagePolicy.WhenAll;
            int requiredCount = 0;
            switch (stage.Policy)
            {
                case WhenAnyStagePolicy:
                    policy = ESyncStagePolicy.WhenAny;
                    break;
                case WhenNStagePolicy whenN:
                    policy = ESyncStagePolicy.WhenN;
                    requiredCount = whenN.RequiredCount;
                    break;
                case RepeatUntilSkippedPolicy repeatUntilSkipped:
                    if (repeatUntilSkipped.DelayBetweenRepeats > 0) WarnLeftOut(owner, "its delay between repeats");
                    break;
                case null:
                case WhenAllStagePolicy:
                    break;
                default:
                    ForgeLog.Warning($"{owner}: a synchronous sequence can't run its {stage.Policy.GetType().Name}, so it waits for all its tasks.");
                    break;
            }

            // A timer that skips its stage is a limit on the stage, which a sync stage has. One that does anything
            // else needs a runtime, and just ends the stage here
            float? limit = stage.MaxDurationSeconds;
            if (stage.HasMaxDuration && stage.MaxDurationInjection is not SkipStageInjection) WarnTimer(owner, stage.MaxDurationInjection);

            foreach (var timeout in stage.Timeouts)
            {
                limit = limit.HasValue ? Math.Min(limit.Value, timeout.Seconds) : timeout.Seconds;
                if (timeout.Injection is not SkipStageInjection) WarnTimer(owner, timeout.Injection);
            }

            return new SyncSequenceStage(
                new List<ISequenceTask>(stage.Tasks),
                stage.Metadata?.Name,
                policy,
                stage.ShouldRepeat,
                limit,
                (stage.Metadata?.IsCritical ?? false) || wholeSequenceIsCritical)
            {
                RequiredCount = requiredCount,
                OnInit = stage.OnInit,
                OnRepeat = stage.OnRepeat,
                OnTerminate = stage.OnTerminate
            };
        }

        private static void WarnLeftOut(string owner, string feature)
        {
            ForgeLog.Warning($"{owner}: left out {feature}, which a synchronous sequence can't run.");
        }

        private static void WarnTimer(string owner, ISequenceInjection injection)
        {
            ForgeLog.Warning($"{owner}: a timer that applies {injection?.GetType().Name ?? "nothing"} just ends its stage in a synchronous sequence.");
        }

        private static bool StageHasConditionsRecursive(SequenceStage stage)
        {
            if (stage.HasConditions) return true;
            
            foreach (var subStage in stage.SubStages)
            {
                if (StageHasConditionsRecursive(subStage)) return true;
            }
            
            if (stage is BranchStage branch)
            {
                foreach (var branchCase in branch.Branches)
                {
                    if (StageHasConditionsRecursive(branchCase.Stage)) return true;
                }
                if (branch.DefaultBranch != null && StageHasConditionsRecursive(branch.DefaultBranch)) return true;
            }
            
            return false;
        }
    }
    
    /// <summary>
    /// Declarative annotation of what a sequence DOES — the introspection channel for
    /// code-driven sequences. Sequence bodies are opaque async lambdas; annotations let
    /// the author state gameplay intent so <see cref="AbilityInspector"/> can decipher
    /// abilities built on RunSequenceTask (tooltips, the Forge analysis tab, debugging).
    ///
    /// <para>The load-and-apply pattern: an asset-loader task (e.g. SetEffectsTask) puts
    /// effects into the data packet under a tag, and the sequence applies them via
    /// <c>data.TryApplyEffects(target, tag)</c>. Annotating with that tag
    /// (<see cref="AppliedEffectsTag"/>) lets the inspector connect the loaded effect
    /// assets to this sequence's application — including how many times
    /// (<see cref="MaxApplications"/>, e.g. Chain Lightning's 4 jumps).</para>
    /// </summary>
    public class SequenceAnnotation
    {
        /// <summary>Human description of the behavior ("chains to up to 4 nearby enemies").</summary>
        public string Text;

        /// <summary>
        /// When valid: this sequence applies the packet effects loaded under this tag
        /// (the SetEffectsTask / TryApplyEffects data key).
        /// </summary>
        public Tag AppliedEffectsTag;

        /// <summary>Upper bound on how many times the effects are applied per activation.</summary>
        public int MaxApplications = 1;

        /// <summary>Who the applications land on, as authored intent.</summary>
        public EEffectTargetScope Scope = EEffectTargetScope.AcquiredTarget;

        public bool AppliesLoadedEffects => AppliedEffectsTag.IsValid;
    }

    /// <summary>
    /// Optional metadata for the sequence.
    /// </summary>
    public class SequenceMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool EnableErrorLogging { get; set; } = true;
        public bool EnableInjectionLogging { get; set; } = false;
        public bool Repeat { get; set; } = false;
        public bool IsCritical { get; set; } = false;
        public HashSet<Type> CriticalAllowedInjections { get; set; }

        /// <summary>
        /// Declared gameplay behavior for introspection (see <see cref="SequenceAnnotation"/>).
        /// Null when the sequence declares nothing.
        /// </summary>
        public List<SequenceAnnotation> Annotations { get; set; }

        /// <summary>
        /// Process lifecycle for this sequence. Determines whether it runs async or sync.
        /// Default is SelfTerminating (async).
        /// </summary>
        public EProcessLifecycle Lifecycle { get; set; } = EProcessLifecycle.SelfTerminating;  // Async by default

        /// <summary>
        /// Step timing for the process. Determines which Unity update loop the process attaches to.
        /// For synchronous sequences this must be set (e.g. Update). For async, defaults to None
        /// unless conditions are present.
        /// </summary>
        public EProcessStepTiming StepTiming { get; set; } = EProcessStepTiming.None;

        /// <summary>
        /// Per-step callbacks keyed by timing. Invoked each frame during sync execution.
        /// </summary>
        public Dictionary<EProcessStepTiming, Action<SequenceDataPacket, float>> OnStepCallbacks { get; set; }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EVENT CALLBACKS (all sync - called immediately when event occurs)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Called when the sequence's max duration is reached, before the injection is applied.
        /// </summary>
        public Action<SequenceEventContext> OnMaxDuration { get; set; }
        
        /// <summary>
        /// Called when the sequence becomes timed out, before the injection is applied.
        /// </summary>
        public Action<SequenceEventContext, SequenceTimeout> OnTimeout { get; set; }
        
        /// <summary>
        /// Called when an exception occurs anywhere in the sequence.
        /// Return true to suppress the exception (sequence fails gracefully), false to propagate.
        /// </summary>
        public Func<SequenceEventContext, Exception, bool> OnException { get; set; }
        
        /// <summary>
        /// Called when the sequence completes (success or failure), before repeating.
        /// </summary>
        public Action<SequenceEventContext, bool> OnComplete { get; set; }
        
        /// <summary>
        /// Called when the sequence terminates (success or failure).
        /// </summary>
        public Action<SequenceEventContext, bool> OnTerminate { get; set; }
        
    }
}