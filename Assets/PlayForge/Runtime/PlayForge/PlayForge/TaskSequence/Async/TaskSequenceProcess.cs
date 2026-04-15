using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Wraps a TaskSequence or TaskSequenceChain for execution via ProcessControl.
    /// Supports both async (SelfTerminating/RunThenWait) and sync (Synchronous) lifecycles.
    ///
    /// Async mode: RunProcess executes the sequence via Execute path.
    /// Sync mode: RunProcess is skipped; WhenUpdate drives the SyncedTaskSequence runner via Step path.
    ///
    /// Uses SequenceDataPacket for direct sequence/stage control.
    /// </summary>
    public class TaskSequenceProcess : LazyRuntimeProcess
    {
        private readonly TaskSequence _sequence;
        private readonly TaskSequenceChain _chain;
        private readonly SyncedTaskSequence _syncRunner;
        private readonly bool _hasConditions;
        private readonly EProcessLifecycle _requestedLifecycle;
        private readonly EProcessStepTiming _requestedStepTiming;
        
        public SequenceDataPacket SeqData { get; private set; }

        /// <summary>The sequence being executed (null if using chain).</summary>
        public TaskSequence Sequence => _sequence;

        /// <summary>The chain being executed (null if using single sequence).</summary>
        public TaskSequenceChain Chain => _chain;

        /// <summary>True if running a chain, false if running a single sequence.</summary>
        public bool IsChain => _chain != null;

        /// <summary>True if this process runs synchronously (per-frame stepping only).</summary>
        public bool IsSynchronous => _requestedLifecycle == EProcessLifecycle.Synchronous;

        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a process for a single sequence.
        /// Lifecycle and step timing are read from the sequence's metadata.
        /// </summary>
        public TaskSequenceProcess(TaskSequence sequence)
            : base(
                sequence.Definition.Metadata?.Name ?? "TaskSequence",
                EProcessStepPriorityMethod.First,
                0,
                ResolveStepTiming(sequence),
                ResolveLifecycle(sequence))
        {
            _sequence = sequence;
            _hasConditions = sequence.HasConditions;
            _requestedLifecycle = ResolveLifecycle(sequence);
            _requestedStepTiming = ResolveStepTiming(sequence);

            // Build sync runner if synchronous lifecycle
            if (_requestedLifecycle == EProcessLifecycle.Synchronous)
            {
                _syncRunner = sequence.Definition.BuildSyncRunner();
                _syncRunner.OnCriticalSectionExited = sequence.OnCriticalSectionExited;
            }
        }

        /// <summary>
        /// Creates a process for a single sequence with explicit lifecycle and timing overrides.
        /// </summary>
        public TaskSequenceProcess(TaskSequence sequence, EProcessLifecycle lifecycle, EProcessStepTiming stepTiming)
            : base(
                sequence.Definition.Metadata?.Name ?? "TaskSequence",
                EProcessStepPriorityMethod.First,
                0,
                stepTiming,
                lifecycle)
        {
            _sequence = sequence;
            _hasConditions = sequence.HasConditions;
            _requestedLifecycle = lifecycle;
            _requestedStepTiming = stepTiming;

            if (lifecycle == EProcessLifecycle.Synchronous)
            {
                _syncRunner = sequence.Definition.BuildSyncRunner();
                _syncRunner.OnCriticalSectionExited = sequence.OnCriticalSectionExited;
            }
        }

        /// <summary>
        /// Creates a process for a SyncedTaskSequence directly.
        /// Always uses Synchronous lifecycle.
        /// </summary>
        public TaskSequenceProcess(SyncedTaskSequence syncRunner, string name = "SyncSequence",
            EProcessStepTiming stepTiming = EProcessStepTiming.Update)
            : base(
                name,
                EProcessStepPriorityMethod.First,
                0,
                stepTiming,
                EProcessLifecycle.Synchronous)
        {
            _syncRunner = syncRunner;
            _requestedLifecycle = EProcessLifecycle.Synchronous;
            _requestedStepTiming = stepTiming;
        }

        /// <summary>
        /// Creates a process for a sequence chain.
        /// </summary>
        public TaskSequenceProcess(TaskSequenceChain chain, string name = "TaskSequenceChain")
            : base(
                name,
                EProcessStepPriorityMethod.First,
                0,
                chain.HasConditions ? EProcessStepTiming.Update : EProcessStepTiming.None,
                EProcessLifecycle.SelfTerminating)
        {
            _chain = chain;
            _hasConditions = chain.HasConditions;
            _requestedLifecycle = EProcessLifecycle.SelfTerminating;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════════

        public override void WhenInitialize(ProcessRelay relay)
        {
            SeqData = regData as SequenceDataPacket;
        }

        /// <summary>
        /// For async: checks conditions. For sync: drives the sync runner.
        /// </summary>
        public override void WhenUpdate(ProcessRelay relay)
        {
            if (IsSynchronous && _syncRunner != null)
            {
                if (_syncRunner.Step(SeqData ?? SequenceDataPacket.SceneRoot(), Time.deltaTime))
                {
                    relay.TerminateImmediate();
                }
                return;
            }

            // Async condition checking
            if (!_hasConditions) return;

            if (IsChain)
            {
                _chain.CheckConditions();
            }
            else
            {
                _sequence.CheckConditions();
            }
        }

        /// <summary>
        /// Runs the sequence or chain asynchronously.
        /// For Synchronous lifecycle, this is never called (ProcessControl skips RunProcess).
        /// </summary>
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            if (IsSynchronous)
            {
                // Synchronous processes don't use RunProcess — they step in WhenUpdate.
                // Keep processActive true so the process stays alive.
                processActive = true;
                await UniTask.WaitWhile(() => processActive, cancellationToken: token);
                return;
            }

            processActive = true;

            try
            {
                if (IsChain)
                {
                    await _chain.Run(regData, token);
                }
                else
                {
                    await _sequence.Run(regData, token);
                }
            }
            finally
            {
                processActive = false;
            }
        }

        /// <summary>
        /// Fires the sequence-level OnTerminate callback for sync sequences.
        /// Async sequences handle this internally via TaskSequenceRuntime.
        /// </summary>
        public override void WhenTerminate(ProcessRelay relay)
        {
            if (IsSynchronous && _sequence?.Definition?.Metadata?.OnTerminate != null)
            {
                try
                {
                    var data = regData as SequenceDataPacket ?? SequenceDataPacket.SceneRoot();
                    var ctx = new SequenceEventContext(data, null);
                    bool completed = _syncRunner?.IsComplete ?? false;
                    _sequence.Definition.Metadata.OnTerminate.Invoke(ctx, completed);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TaskSequenceProcess] Exception in sync OnTerminate handler: {ex}");
                }
            }

            base.WhenTerminate(relay);
        }

        public override void WhenWait(ProcessRelay relay)
        {
            // No-op. Previously set Time.timeScale = 0f here, but that's destructive
            // as a default — it freezes the entire game whenever any sequence enters Waiting.
        }

        public override bool HandlePause(ProcessRelay relay)
        {
            return processActive;
        }

        public override bool HandleResume(ProcessRelay relay)
        {
            if (!processActive) return false;

            Time.timeScale = 1f;
            return true;
        }

        /// <summary>
        /// Returns the step timing based on lifecycle and conditions.
        /// </summary>
        public override EProcessStepTiming StepTiming => GetStepTiming();

        private EProcessStepTiming GetStepTiming()
        {
            // Synchronous always needs a step timing
            if (IsSynchronous)
            {
                return _requestedStepTiming != EProcessStepTiming.None
                    ? _requestedStepTiming
                    : EProcessStepTiming.Update;
            }

            if (!_hasConditions) return EProcessStepTiming.None;

            return IsChain ? _chain.ConditionStepTiming : _sequence.Definition.ConditionStepTiming;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // INJECTION PASSTHROUGH
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Injects into the running sequence/chain.
        /// </summary>
        public bool Inject(ISequenceInjection injection)
        {
            return IsChain ? _chain.Inject(injection) : _sequence.Inject(injection);
        }

        /// <summary>
        /// Interrupts the running sequence/chain.
        /// For chains, this interrupts the entire chain (not just the current sequence).
        /// </summary>
        public bool Interrupt()
        {
            return IsChain ? _chain.Interrupt() : _sequence.Interrupt();
        }

        /// <summary>
        /// Skips the current sequence in the chain and moves to the next.
        /// Only applicable for chains.
        /// </summary>
        public bool SkipCurrentSequence()
        {
            return IsChain && _chain.SkipCurrentSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════

        private static EProcessLifecycle ResolveLifecycle(TaskSequence sequence)
        {
            return sequence.Definition.Metadata?.Lifecycle ?? EProcessLifecycle.SelfTerminating;
        }

        private static EProcessStepTiming ResolveStepTiming(TaskSequence sequence)
        {
            var meta = sequence.Definition.Metadata;
            if (meta == null)
            {
                return sequence.HasConditions ? EProcessStepTiming.Update : EProcessStepTiming.None;
            }

            // Synchronous lifecycle requires a step timing
            if (meta.Lifecycle == EProcessLifecycle.Synchronous)
            {
                return meta.StepTiming != EProcessStepTiming.None
                    ? meta.StepTiming
                    : EProcessStepTiming.Update;
            }

            // For async, use explicit timing or derive from conditions
            if (meta.StepTiming != EProcessStepTiming.None)
                return meta.StepTiming;

            return sequence.HasConditions ? EProcessStepTiming.Update : EProcessStepTiming.None;
        }
    }
}
