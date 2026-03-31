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
        public readonly IGameplayProcessHandler Handler;
        public readonly int CacheIndex;

        public ProcessRelay Relay => Wrapper?.Relay;
        
        public EProcessState State { get; private set; }
        public EProcessState QueuedState { get; private set; }

        // Timing
        public float UnscaledLifetime => Time.unscaledTime - _unscaledInitializeTime;
        public float Lifetime => Time.time - _initializeTime;
        public float UnscaledInitializeTime => _unscaledInitializeTime;
        public float InitializeTime => _initializeTime;
        //public float TotalUpdateTime => _totalUpdateTime;
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

        protected float _lastCheckEnd = 0f;
        protected float _lastStepEnd = 0f;

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

        private ProcessControlBlock(int cacheIndex, AbstractProcessWrapper wrapper, IGameplayProcessHandler handler)
        {
            CacheIndex = cacheIndex;
            Wrapper = wrapper;
            Handler = handler;
            
            Wrapper.Relay = new ProcessRelay(this);
            State = EProcessState.Created;
            _totalUpdateTime = 0;
        }

        public static ProcessControlBlock Generate(int cacheIndex, AbstractProcessWrapper process, IGameplayProcessHandler handler)
        {
            return new ProcessControlBlock(cacheIndex, process, handler);
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

        private void SetQueuedState()
        {
            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t\t[{Wrapper.ProcessName}] Setting queued state: {State} -> {QueuedState}");
            
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

        public void ForceIntoState(EProcessState state)
        {
            if (state == State) return;

            if (ProcessControl.Instance.OutputLogs && ProcessControl.Instance.DetailedLogs) Debug.Log($"\t[ {Wrapper.ProcessName} ] Force {state}");
            
            if (State == EProcessState.Running && state != EProcessState.Running)
            {
                if (state == EProcessState.Terminated || !Wrapper.HandlePause(Relay)) Interrupt();
            }
            
            QueuedState = state;
            SetQueuedState();
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
            _lastStepEnd = _lastCheckEnd;
            
            Wrapper.WhenInitialize(Wrapper.Relay);
        }
        
        public bool TryRun()
        {
            if (State != EProcessState.Running) return false;

            QueuedState = ProcessControl.Instance.GetDefaultTransitionState(this);
            if (_hasRun && Wrapper.HandleResume(Relay)) return true;
            
            if (Wrapper.Lifecycle == EProcessLifecycle.StepOnly)
            {
                _hasRun = true;
                return true;
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
            
            Wrapper.WhenTerminate(Wrapper.Relay);
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

            _totalUpdateTime += Time.unscaledTime - (Time.time - _lastStepEnd) - Time.time;
            _lastStepEnd = Time.time;
            
            // _totalUpdateTime += Time.deltaTime;
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

            bool shouldTransition = true;
            try
            {
                await Wrapper.RunProcess(Wrapper.Relay, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                //shouldTransition = true;
                _midRun = true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
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

    public enum EProcessState
    {
        Created,    // Created but not initialized
        Running,    // Actively running
        Waiting,    // Paused/waiting
        Terminated  // Terminated and cleaned up
    }

    public enum EProcessStatus
    {
        Async,  // waiting for an async process
        Inline,
        Unknown
    }
}