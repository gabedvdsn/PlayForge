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
        public readonly AbstractProcessWrapper Process;
        public readonly IGameplayProcessHandler Handler;
        public readonly int CacheIndex;

        public ProcessRelay Relay => Process?.Relay;
        
        public EProcessState State { get; private set; }
        public EProcessState QueuedState { get; private set; }

        // Timing
        public float UnscaledLifetime => Time.unscaledTime - _unscaledInitializeTime;
        public float Lifetime => Time.time - _initializeTime;
        public float UnscaledInitializeTime => _unscaledInitializeTime;
        public float InitializeTime => _initializeTime;
        public float TotalUpdateTime => _totalUpdateTime;

        private float _unscaledInitializeTime;
        private float _initializeTime;
        private float _totalUpdateTime;

        // Status flags
        public bool IsInitialized => _isInitialized;
        public bool HasRun => _hasRun;
        public bool MidRun => _midRun;
        public bool isMono;

        private bool _isInitialized;
        private bool _hasRun;
        private bool _midRun;
        
        private CancellationTokenSource _cts;

        #region Construction

        private ProcessControlBlock(int cacheIndex, AbstractProcessWrapper process, IGameplayProcessHandler handler)
        {
            CacheIndex = cacheIndex;
            Process = process;
            Handler = handler;
            
            Process.Relay = new ProcessRelay(this);
            State = EProcessState.Created;
            _totalUpdateTime = 0;
        }

        public static ProcessControlBlock Generate(int cacheIndex, AbstractProcessWrapper process, IGameplayProcessHandler handler)
        {
            return new ProcessControlBlock(cacheIndex, process, handler);
        }

        #endregion

        #region State Management

        public void ForceIntoState(EProcessState state)
        {
            if (state == State) return;

            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t[ {Process.ProcessName} ] Force {state}");
            
            if (State == EProcessState.Running && state != EProcessState.Running)
            {
                Interrupt();
            }
            
            QueuedState = state;
            SetQueuedState();
        }
        
        public void QueueNextState(EProcessState state)
        {
            if (state == State) return;
            
            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t[ {Process.ProcessName} ] Queue {state}");
            
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

        private void SetQueuedState()
        {
            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t\t[{Process.ProcessName}] Setting queued state: {State} -> {QueuedState}");
            
            State = QueuedState;
            
            switch (State)
            {
                case EProcessState.Created:
                    break;
                case EProcessState.Running:
                    ProcessControl.Instance.ProcessWillRun(this);
                    break;
                case EProcessState.Waiting:
                    ProcessControl.Instance.ProcessWillWait(this);
                    break;
                case EProcessState.Terminated:
                    ProcessControl.Instance.ProcessWillTerminate(this);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Lifecycle Methods

        public void Initialize()
        {
            if (_isInitialized) return;

            _unscaledInitializeTime = Time.unscaledTime;
            _initializeTime = Time.time;
            _isInitialized = true;
            
            Process.WhenInitialize(Process.Relay);
        }
        
        public bool TryRun()
        {
            if (State != EProcessState.Running) return false;

            RunProcessAsync().Forget();
            QueuedState = ProcessControl.Instance.GetDefaultTransitionState(this);
            
            return true;
        }

        public bool TryWait()
        {
            if (State != EProcessState.Waiting) return false;

            Process.WhenWait(Process.Relay);
            return true;
        }
        
        public bool TryTerminate()
        {
            if (State != EProcessState.Terminated) return false;
            
            Process.WhenTerminate(Process.Relay);
            ProcessControl.Instance.Unregister(this);
            
            return true;
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
            switch (timing)
            {
                case EProcessStepTiming.None:
                    break;
                case EProcessStepTiming.Update:
                    Process.WhenUpdate(Process.Relay);
                    break;
                case EProcessStepTiming.LateUpdate:
                    Process.WhenLateUpdate(Process.Relay);
                    break;
                case EProcessStepTiming.FixedUpdate:
                    Process.WhenFixedUpdate(Process.Relay);
                    break;
                case EProcessStepTiming.UpdateAndLate:
                    Process.WhenUpdate(Process.Relay);
                    Process.WhenLateUpdate(Process.Relay);
                    break;
                case EProcessStepTiming.UpdateAndFixed:
                    Process.WhenUpdate(Process.Relay);
                    Process.WhenFixedUpdate(Process.Relay);
                    break;
                case EProcessStepTiming.LateAndFixed:
                    Process.WhenLateUpdate(Process.Relay);
                    Process.WhenFixedUpdate(Process.Relay);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(timing), timing, null);
            }
            
            _totalUpdateTime += Time.deltaTime;
        }

        #endregion

        #region Private Helpers
        
        private async UniTask RunProcessAsync()
        {
            _cts = new CancellationTokenSource();
            _hasRun = true;
            
            if (!_midRun)
            {
                _totalUpdateTime = 0f;
            }
            _midRun = false;

            bool shouldTransition = true;
            try
            {
                await Process.RunProcess(Process.Relay, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                _midRun = true;
                shouldTransition = false;
            }

            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
            
            _cts?.Dispose();
            _cts = null;

            if (shouldTransition)
            {
                SetQueuedState();
            }
        }

        #endregion
    }

    /// <summary>
    /// Provides external access to ProcessControlBlock data without exposing internal state management.
    /// </summary>
    public class ProcessRelay
    {
        private readonly ProcessControlBlock _pcb;
        
        public bool Valid => _pcb != null;

        public ProcessRelay(ProcessControlBlock pcb)
        {
            _pcb = pcb;
        }

        public int CacheIndex => _pcb.CacheIndex;
        public AbstractProcessWrapper Wrapper => _pcb.Process;
        public IGameplayProcessHandler Handler => _pcb.Handler;
        public EProcessState State => _pcb.State;
        public EProcessState QueuedState => _pcb.QueuedState;
        public float UnscaledLifetime => _pcb.UnscaledLifetime;
        public float Lifetime => _pcb.Lifetime;
        public float UpdateTime => _pcb.TotalUpdateTime;

        public bool TryGetProcess<T>(out T process)
        {
            return _pcb.Process.TryGetProcess(out process);
        }
        
        /// <summary>
        /// Calculates remaining runtime based on elapsed update time.
        /// </summary>
        /// <param name="runtime">Total runtime in milliseconds</param>
        /// <param name="multiplier">Conversion multiplier (default 1000 for ms)</param>
        public int RemainingRuntime(int runtime, int multiplier = 1000) 
            => runtime - (int)UpdateTime * multiplier;

        /// <summary>
        /// Pauses this process and optionally its children.
        /// </summary>
        public bool Pause(bool cascade = true) 
            => ProcessControl.Instance.Pause(CacheIndex, cascade);

        /// <summary>
        /// Resumes this process and optionally its children.
        /// </summary>
        public bool Unpause(bool cascade = true) 
            => ProcessControl.Instance.Unpause(CacheIndex, cascade);

        /// <summary>
        /// Terminates this process and optionally its children.
        /// </summary>
        public bool Terminate(bool cascade = true) 
            => ProcessControl.Instance.Terminate(CacheIndex, cascade);
    }
    
    public enum EProcessState
    {
        Created,    // Created but not initialized
        Running,    // Actively running
        Waiting,    // Paused/waiting
        Terminated  // Terminated and cleaned up
    }
}