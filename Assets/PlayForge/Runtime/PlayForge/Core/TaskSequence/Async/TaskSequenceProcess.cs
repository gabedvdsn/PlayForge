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

        // This process's run, which isn't necessarily the only run of its sequence
        private TaskSequenceRuntime _runtime;
        
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

        public override void WhenInitialize()
        {
            base.WhenInitialize();

            // Made once, here. A sync sequence made a new one for every step, and a plain packet was swapped for an
            // empty one, so what it carried never reached the tasks (TS-P)
            SeqData = regData as SequenceDataPacket ?? new SequenceDataPacket(regData);
        }

        public override void WhenUpdate() => StepAt(EProcessStepTiming.Update);

        public override void WhenFixedUpdate() => StepAt(EProcessStepTiming.FixedUpdate);

        public override void WhenLateUpdate() => StepAt(EProcessStepTiming.LateUpdate);

        /// <summary>
        /// For async: checks conditions. For sync: drives the sync runner. Every timing the process steps at comes
        /// here: only Update used to, so a sequence set to step at any other never ran (TS-H).
        /// </summary>
        private void StepAt(EProcessStepTiming timing)
        {
            if (IsSynchronous && _syncRunner != null)
            {
                var onStep = _sequence?.Definition.Metadata?.OnStepCallbacks;
                if (onStep != null && onStep.TryGetValue(timing, out var callback)) callback?.Invoke(SeqData, Time.deltaTime);

                // A composite timing steps this more than once a frame, and a sequence that moved on at each would
                // run that many times too fast
                if (timing != AdvancingTiming(StepTiming)) return;

                if (_syncRunner.Step(SeqData, Time.deltaTime))
                {
                    Relay.TerminateImmediate();
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
                _runtime?.CheckConditions();
            }
        }

        /// <summary>
        /// Runs the sequence or chain asynchronously.
        /// For Synchronous lifecycle, this is never called (ProcessControl skips RunProcess).
        /// </summary>
        public override async UniTask RunProcess(CancellationToken token)
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
                    await _chain.Run(SeqData, token);
                }
                else
                {
                    _runtime = _sequence.CreateRuntime();
                    await _sequence.Run(_runtime, SeqData, token);
                }
            }
            finally
            {
                processActive = false;
                _runtime = null;
            }
        }

        /// <summary>
        /// Fires the sequence-level OnTerminate callback for sync sequences.
        /// Async sequences handle this internally via TaskSequenceRuntime.
        /// </summary>
        public override void WhenTerminate()
        {
            // Ended part way through, a sync sequence cleans the stage it's in, as an async one does (TS-P)
            if (IsSynchronous) _syncRunner?.Interrupt(SeqData);

            if (IsSynchronous && _sequence?.Definition?.Metadata?.OnTerminate != null)
            {
                try
                {
                    var ctx = new SequenceEventContext(SeqData, null);
                    bool completed = _syncRunner?.IsComplete ?? false;
                    _sequence.Definition.Metadata.OnTerminate.Invoke(ctx, completed);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[TaskSequenceProcess] Exception in sync OnTerminate handler: {ex}");
                }
            }

            base.WhenTerminate();
        }

        /// <summary>
        /// The run lasts as long as the sequence does, so a requested Terminate interrupts it. A sync sequence
        /// has no run, and applies the Terminate on its next step.
        /// </summary>
        public override void WhenTerminateQueued()
        {
            if (!IsSynchronous) Interrupt();
        }

        public override void WhenWait()
        {
            // No-op. Previously set Time.timeScale = 0f here, but that's destructive
            // as a default — it freezes the entire game whenever any sequence enters Waiting.
        }

        /// <summary>
        /// Holds the running sequence where it is, as <see cref="TaskSequenceRuntime.Pause"/> describes. With no
        /// run there's nothing to hold, which goes for a synchronous sequence too: it holds by not being stepped.
        /// </summary>
        public override bool TryHandlePause()
        {
            if (!processActive) return false;

            if (IsChain) _chain.Pause();
            else _runtime?.Pause();

            return true;
        }

        public override bool TryHandleResume()
        {
            if (!processActive) return false;

            // Time.timeScale is left alone for the same reason as WhenWait: it is global, so resuming one
            // sequence must not end a hit-stop or slow motion someone else started.
            if (IsChain) _chain.Resume();
            else _runtime?.Resume();

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
            return IsChain ? _chain.Inject(injection) : _runtime?.Inject(injection) ?? false;
        }

        /// <summary>
        /// Interrupts the running sequence/chain.
        /// For chains, this interrupts the entire chain (not just the current sequence).
        /// </summary>
        public bool Interrupt()
        {
            // A sync sequence has no run to inject into: its runner stops, and its next step ends the process (TS-P)
            if (IsSynchronous) return _syncRunner.Interrupt(SeqData);

            return IsChain ? _chain.Interrupt() : Inject(InterruptSequenceInjection.Instance);
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

        // The one timing a sync sequence moves on at: Update if it steps at Update, LateUpdate if not but it steps
        // then, and FixedUpdate only when that's all it steps at
        private static EProcessStepTiming AdvancingTiming(EProcessStepTiming timing) => timing switch
        {
            EProcessStepTiming.LateUpdate or EProcessStepTiming.LateAndFixed => EProcessStepTiming.LateUpdate,
            EProcessStepTiming.FixedUpdate => EProcessStepTiming.FixedUpdate,
            _ => EProcessStepTiming.Update
        };

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
