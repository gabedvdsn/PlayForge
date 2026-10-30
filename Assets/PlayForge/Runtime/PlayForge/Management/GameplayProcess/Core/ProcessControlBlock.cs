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
        public float UnscaledLifetime => Time.unscaledTime - _unscaledInitializeTime;
        public float Lifetime => Time.time - _initializeTime;
        public float UnscaledInitializeTime => _unscaledInitializeTime;
        public float InitializeTime => _initializeTime;
        public float TotalUpdateTime => _totalUpdateTime;

        public float UseSinceLastCheck
        {
            get
            {
                var _use = Time.time - _lastCheckEnd;
                _lastCheckEnd = Time.time;
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

        // A pause left this run going, for the process to hold
        private bool RunIsHeld => _cts != null && !_cts.IsCancellationRequested;

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

            _unscaledInitializeTime = Time.unscaledTime;
            _initializeTime = Time.time;
            _isInitialized = true;

            _lastCheckEnd = Time.time;
            _lastStepEnd = Time.unscaledTime;

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

            return ProcessControl.Instance.Unregister(this);
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

            // Synchronous processes have no async completion to flush queued state.
            // If a state change was queued during the step callback (e.g. relay.Terminate()
            // called inside WhenUpdate), process it now.
            if (Wrapper.Lifecycle == EProcessLifecycle.Synchronous && QueuedState != State)
            {
                SetQueuedState();
                return;
            }

            _totalUpdateTime += Time.unscaledTime - _lastStepEnd;
            _lastStepEnd = Time.unscaledTime;
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
