using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Represents a managed process instance with state tracking and lifecycle management.
    /// </summary>
    public class ProcessControlBlock
    {
        public readonly AbstractProcessWrapper Wrapper;
        public readonly int CacheIndex;
        public IGameplayProcessHandler Handler => Wrapper.Handler;

        public ProcessRelay Relay => Wrapper?.Relay;

        public EProcessState LastState { get; private set; }

        private EProcessState _state;
        public EProcessState State
        {
            get => _state;
            private set
            {
                LastState = _state;
                _state = value;
            }
        }

        public EProcessState QueuedState { get; private set; }

        // Timing
        public float UnscaledLifetime => ForgeTime.UnscaledTime - _unscaledInitializeTime;
        public float Lifetime => ForgeTime.Time - _initializeTime;
        public float UnscaledInitializeTime => _unscaledInitializeTime;
        public float InitializeTime => _initializeTime;

        /// <summary>
        /// The wall-clock time between the block's steps, added up over its run. It says nothing of what a step costs:
        /// <see cref="StepSeconds"/> does.
        /// </summary>
        public float TotalUpdateTime => _totalUpdateTime;

        /// <summary>
        /// Whether each block counts its steps and times them, as the Debugger asks while it reads the game. Off,
        /// as it is by default, a step costs one check of it. Process Control showed the time between a process's steps as
        /// its cost, much the same for every process whatever its work (ED-5).
        /// </summary>
        public static bool MeasureSteps { get; set; }

        /// <summary>The steps the block has taken while <see cref="MeasureSteps"/> was on.</summary>
        public int Steps => _steps;

        /// <summary>The time those steps took, in seconds.</summary>
        public double StepSeconds => _stepTicks / (double)System.Diagnostics.Stopwatch.Frequency;

        /// <summary>The longest of those steps, in seconds.</summary>
        public double PeakStepSeconds => _peakStepTicks / (double)System.Diagnostics.Stopwatch.Frequency;

        private int _steps;
        private long _stepTicks;
        private long _peakStepTicks;

        public float UseSinceLastCheck
        {
            get
            {
                var _use = ForgeTime.Time - _lastCheckEnd;
                _lastCheckEnd = ForgeTime.Time;
                return _use;
            }
        }

        private float _lastCheckEnd;
        private float _lastStepEnd;

        private float _unscaledInitializeTime;
        private float _initializeTime;
        private float _totalUpdateTime;

        // Status flags
        public bool IsInitialized => _isInitialized;
        public bool HasRun => _hasRun;
        public bool MidRun => _midRun;
        public bool isMono;

        /// <summary>True while this process is Waiting because ProcessControl is, and goes back when it isn't.</summary>
        public bool HeldByControl => _heldByControl;

        /// <summary>True once something has asked this process to terminate. It outranks a pause.</summary>
        public bool TerminateRequested => _terminateRequested;

        private bool _isInitialized;
        private bool _hasRun;
        private bool _midRun;
        private bool _heldByControl;
        private bool _terminateRequested;
        private bool _runEndedWhilePaused;

        private CancellationTokenSource _cts;

        /// <summary>
        /// Whether the process's run is going, or held by a pause: a paused process that holds its run picks it up on
        /// resume, and one that doesn't had it cancelled, so a resume starts a new one (Decision 5). The Debugger shows
        /// which (ED-5).
        /// </summary>
        public bool RunIsHeld => _cts != null && !_cts.IsCancellationRequested;

        // Done once the process has terminated and is unregistered, for whatever waits on it: made when something does.
        // Waiting meant polling the state once a frame, which found the end a frame later, and each relay waited on
        // added a frame (TS-Z)
        private UniTaskCompletionSource _terminated;
        private bool _terminationDone;

        /// <summary>Completes once the process has terminated and is unregistered, or at once if it has.</summary>
        internal UniTask WhenTerminated()
        {
            if (_terminationDone) return UniTask.CompletedTask;

            _terminated ??= new UniTaskCompletionSource();
            return _terminated.Task;
        }

        private ProcessControlBlock(int cacheIndex, AbstractProcessWrapper wrapper)
        {
            CacheIndex = cacheIndex;
            Wrapper = wrapper;

            LastState = EProcessState.Created;
            _state = EProcessState.Created;
            QueuedState = EProcessState.Terminated;

            _lastCheckEnd = 0f;
            _lastStepEnd = 0f;

            _unscaledInitializeTime = 0f;
            _initializeTime = 0f;
            _totalUpdateTime = 0;

            isMono = false;
            _isInitialized = false;
            _hasRun = false;
            _midRun = false;

            _cts = null;

            Wrapper.Relay = new ProcessRelay(this);
        }

        #region Construction

        public static ProcessControlBlock Generate(int cacheIndex, AbstractProcessWrapper process)
        {
            return new ProcessControlBlock(cacheIndex, process);
        }

        #endregion

        #region State Management

        public void QueueNextState(EProcessState state)
        {
            if (state == State) return;

            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t[ {Wrapper.ProcessName} ] Queue {state}");

            switch (state)
            {
                case EProcessState.Created:
                    break;
                case EProcessState.Running:
                case EProcessState.Waiting:
                case EProcessState.Terminated:
                    QueuedState = state;
                    if (State != EProcessState.Running)
                    {
                        SetQueuedState();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        /// <summary>
        /// Queues Terminated at someone's request. A running process only moves on when its run ends, and a run
        /// may be waiting for exactly this, so the process is told (PC-5). A default transition queues the same
        /// state without asking anything to stop, so it doesn't come through here.
        /// </summary>
        public void QueueTerminate()
        {
            _terminateRequested = true;
            QueueNextState(EProcessState.Terminated);
            if (State != EProcessState.Running) return;

            // With no run to end and no step to apply it on, nothing else ever would (PC-17)
            if (Wrapper.Lifecycle == EProcessLifecycle.Synchronous && Wrapper.StepTiming == EProcessStepTiming.None)
            {
                SetQueuedState();
                return;
            }

            try
            {
                Wrapper.WhenTerminateQueued(Relay);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void SetQueuedState()
        {
            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t\t[{Wrapper.ProcessName}] Setting queued state: {State} -> {QueuedState}");

            State = QueuedState;

            ProcessControl.Instance.ProcessWillChangeState(this, State);
        }

        public void ForceIntoState(EProcessState state)
        {
            if (state == State) return;

            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t[ {Wrapper.ProcessName} ] Force {state}");

            if (State == EProcessState.Running && state != EProcessState.Running)
            {
                if (state == EProcessState.Terminated || !TryHoldRun()) Interrupt();
            }

            QueuedState = state;
            SetQueuedState();
        }

        /// <summary>
        /// Stops a running process for ProcessControl's own pause, marking it to go back when that ends.
        /// Nothing else goes back then: what was already Waiting was Waiting for a reason of its own.
        /// </summary>
        public void HoldForControl()
        {
            if (State != EProcessState.Running) return;

            _heldByControl = true;
            ForceIntoState(EProcessState.Waiting);
        }

        /// <summary>
        /// Marks a process that came in during ProcessControl's pause, to start when that ends.
        /// </summary>
        public void WaitForControl()
        {
            _heldByControl = true;
        }

        /// <summary>
        /// Makes ProcessControl's pause this process's own, so it stays paused when that ends.
        /// </summary>
        public void KeepPaused()
        {
            _heldByControl = false;
        }

        /// <summary>
        /// Sends back what ProcessControl's pause stopped.
        /// </summary>
        public void ReleaseFromControl()
        {
            if (!_heldByControl) return;

            _heldByControl = false;
            ForceIntoState(EProcessState.Running);
        }

        #endregion

        #region Lifecycle Methods

        public void Initialize()
        {
            if (_isInitialized) return;

            _unscaledInitializeTime = ForgeTime.UnscaledTime;
            _initializeTime = ForgeTime.Time;
            _isInitialized = true;

            _lastCheckEnd = ForgeTime.Time;
            _lastStepEnd = ForgeTime.UnscaledTime;

            Wrapper.WhenInitialize(Wrapper.Relay);
        }

        public bool TryRun()
        {
            if (State != EProcessState.Running) return false;

            // For async processes, set the default transition state so RunProcessAsync
            // can flush it on completion. Synchronous processes have no async completion,
            // so setting this would cause the Step() flush to eagerly transition them
            // out of Running (e.g. into Waiting) on the very first frame.
            if (Wrapper.Lifecycle != EProcessLifecycle.Synchronous)
            {
                QueuedState = ProcessControl.Instance.GetDefaultTransitionState(this);
            }

            if (_terminateRequested) QueuedState = EProcessState.Terminated;

            // Nothing to go back to: the run finished during a pause, or a Terminate is due and the pause
            // cancelled the run it was waiting for
            if (_runEndedWhilePaused || (_terminateRequested && !RunIsHeld))
            {
                _runEndedWhilePaused = false;
                SetQueuedState();
                return true;
            }

            if (Wrapper.Lifecycle == EProcessLifecycle.Synchronous)
            {
                if (_hasRun) TryResumeRun();

                _hasRun = true;
                return true;
            }

            // A run the pause left going picks up where it stopped, or is cancelled like any other
            if (RunIsHeld)
            {
                if (TryResumeRun()) return true;

                _cts.Cancel();
            }

            RunProcessAsync().Forget();

            return true;
        }

        public bool TryWait()
        {
            if (State != EProcessState.Waiting) return false;

            Wrapper.WhenWait(Wrapper.Relay);
            return true;
        }

        public bool TryTerminate()
        {
            if (State != EProcessState.Terminated) return false;

            // A run that a pause left going has nothing else to end it
            _cts?.Cancel();

            // The process's own code, and the unregister has to happen whatever it does. A throw that skipped
            // it left a block that was Terminated, still stepped, and that nothing could end (PC-8)
            try
            {
                Wrapper.WhenTerminate(Wrapper.Relay);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            try
            {
                return ProcessControl.Instance.Unregister(this);
            }
            finally
            {
                // Whatever waits on the process goes on now, in the frame it ended (TS-Z)
                _terminationDone = true;
                _terminated?.TrySetResult();
            }
        }

        public void Interrupt()
        {
            if (State != EProcessState.Running) return;
            _cts?.Cancel();
        }

        #endregion

        #region Update/Step

        public void Step(EProcessStepTiming timing)
        {
            bool timed = MeasureSteps;
            long started = timed ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;

            // A step that throws is reported and goes no further, as a MonoBehaviour's Update would: the
            // rest of the frame's processes still step, and a state queued for this one still applies
            // below. Escaping, it ended the whole frame's stepping and skipped the flush, so a process
            // terminated while its step kept throwing never left Running (PF-011).
            try
            {
                switch (timing)
                {
                    case EProcessStepTiming.None:
                        break;
                    case EProcessStepTiming.Update:
                        Wrapper.WhenUpdate(Wrapper.Relay);
                        break;
                    case EProcessStepTiming.LateUpdate:
                        Wrapper.WhenLateUpdate(Wrapper.Relay);
                        break;
                    case EProcessStepTiming.FixedUpdate:
                        Wrapper.WhenFixedUpdate(Wrapper.Relay);
                        break;
                    case EProcessStepTiming.UpdateAndLate:
                    case EProcessStepTiming.UpdateAndFixed:
                    case EProcessStepTiming.LateAndFixed:
                        break;
                    case EProcessStepTiming.UpdateFixedAndLate:
                    default:
                        throw new ArgumentOutOfRangeException(nameof(timing), timing, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (timed)
            {
                long took = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                _stepTicks += took;
                if (took > _peakStepTicks) _peakStepTicks = took;
                _steps++;
            }

            // Synchronous processes have no async completion to flush queued state.
            // If a state change was queued during the step callback (e.g. relay.Terminate()
            // called inside WhenUpdate), process it now.
            if (Wrapper.Lifecycle == EProcessLifecycle.Synchronous && QueuedState != State)
            {
                SetQueuedState();
                return;
            }

            _totalUpdateTime += ForgeTime.UnscaledTime - _lastStepEnd;
            _lastStepEnd = ForgeTime.UnscaledTime;
        }

        #endregion

        #region Private Helpers

        // The process's own code, so a throw is reported and counts as a no
        private bool TryHoldRun()
        {
            try
            {
                return Wrapper.TryHandlePause(Relay);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        private bool TryResumeRun()
        {
            try
            {
                return Wrapper.TryHandleResume(Relay);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        private async UniTask RunProcessAsync()
        {
            // A token source per run: a pause and resume in one frame starts a new run while the old one is
            // still unwinding, and sharing one let the old run cancel the new one and end the process (PC-12)
            var cts = new CancellationTokenSource();
            _cts = cts;
            _hasRun = true;

            if (!_midRun)
            {
                _totalUpdateTime = 0f;
            }

            bool cancelled = false;
            try
            {
                await Wrapper.RunProcess(Wrapper.Relay, cts.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                // A run that throws is over like any other, so the state queued for its end still applies.
                // Skipping it left the process Running for good, with anything waiting on it hanging (PC-2)
                Debug.LogException(ex);
            }

            bool replaced = _cts != cts;
            cancelled |= cts.IsCancellationRequested;

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }

            cts.Dispose();

            if (replaced) return;

            _cts = null;
            _midRun = cancelled;

            // A forced state change didn't wait for this run, so there is nothing left to apply. Applying it
            // again ran WhenTerminate or WhenWait a second time (PC-4)
            if (State == EProcessState.Running) SetQueuedState();

            // Left going by a pause, it finished anyway, and is what a resume moves on from
            else if (State == EProcessState.Waiting && !cancelled) _runEndedWhilePaused = true;
        }

        #endregion
    }

    public enum EProcessState
    {
        Created,    // Created but not initialized
        Running,    // Actively running
        Waiting,    // Paused/waiting
        Terminated  // Terminated and cleaned up
    }

    public enum EProcessStatus
    {
        Asynchronous,  // waiting for an async process
        Synchronous,
        Unknown
    }
}
