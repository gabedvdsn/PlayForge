using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor.VersionControl;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ProcessControl : MonoBehaviour, IGameplayProcessHandler, IManagerial
    {
        public static ProcessControl Instance;

        [Header("Process Control")]
        [SerializeField] private EProcessControlState StartState = EProcessControlState.Ready;
        [SerializeField] private new bool DontDestroyOnLoad = true;
        [SerializeField] private bool CollectOnAwake = true;
        
        [Header("Hierarchy Settings")]
        [Tooltip("When true, pausing/terminating a parent will affect all children")]
        [SerializeField] private bool CascadeToChildren = true;
        [Tooltip("When true, processes on the same GameObject are treated as a group")]
        [SerializeField] private bool CascadeToSiblings = true;
        
        [Space]
        public bool OutputLogs;
        public bool DetailedLogs = true;
        
        public EProcessControlState State { get; private set; }

        // Core process storage
        private Dictionary<int, ProcessControlBlock> _active = new();
        private HashSet<int> _waiting = new();
        
        // Optimized stepping - flat lists per timing type for cache-friendly iteration
        private Dictionary<EProcessStepTiming, StepGroup> _stepping;
        
        // Hierarchy management for MonoProcesses
        private ProcessHierarchy _hierarchy = new();

        private int _cacheCounter;
        private int NextCacheIndex => _cacheCounter++; 

        public int Created => _cacheCounter;
        public int Active => _active.Count;
        public int Running => _active.Count - _waiting.Count;

        #region Stepping Optimization
        
        /// <summary>
        /// Optimized container for processes that need to be stepped.
        /// Uses flat array iteration instead of nested dictionary + LINQ.
        /// </summary>
        private class StepGroup
        {
            // Flat list of process indices, grouped by priority
            private readonly List<int> _indices = new();
            private readonly Dictionary<int, int> _priorityStarts = new(); // priority -> start index
            private readonly Dictionary<int, int> _priorityCounts = new(); // priority -> count
            private readonly List<int> _sortedPriorities = new();
            
            private bool _dirty;

            public void Add(int cacheIndex, int priority)
            {
                if (!_priorityCounts.ContainsKey(priority))
                {
                    _priorityCounts[priority] = 0;
                    _sortedPriorities.Add(priority);
                    _sortedPriorities.Sort();
                    _dirty = true;
                }
                
                _indices.Add(cacheIndex);
                _priorityCounts[priority]++;
                _dirty = true;
            }

            public bool Remove(int cacheIndex)
            {
                int idx = _indices.IndexOf(cacheIndex);
                if (idx < 0) return false;
                
                _indices.RemoveAt(idx);
                _dirty = true;
                return true;
            }

            public void RebuildIfDirty()
            {
                if (!_dirty) return;
                
                // Recalculate priority starts
                _priorityStarts.Clear();
                int currentStart = 0;
                foreach (int priority in _sortedPriorities)
                {
                    if (_priorityCounts.TryGetValue(priority, out int count) && count > 0)
                    {
                        _priorityStarts[priority] = currentStart;
                        currentStart += count;
                    }
                }
                
                _dirty = false;
            }

            public List<int> GetIndices()
            {
                RebuildIfDirty();
                return _indices;
            }
            
            public int Count => _indices.Count;
        }
        
        #endregion

        #region Unity Events

        public void Bootstrap()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (DontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            
            ResetProcessControl(StartState);
        }

        public void DeferredInit()
        {
            if (!CollectOnAwake) return;
            CollectSceneProcesses();
        }
        
        private void Update()
        {
            
            StepProcesses(EProcessStepTiming.Update);
        }

        private void LateUpdate()
        {
            StepProcesses(EProcessStepTiming.LateUpdate);
        }

        private void FixedUpdate()
        {
            StepProcesses(EProcessStepTiming.FixedUpdate);
        }

        private void StepProcesses(EProcessStepTiming timing)
        {
            if (State is EProcessControlState.Waiting or EProcessControlState.TerminatedImmediately) 
                return;

            if (!_stepping.TryGetValue(timing, out var group)) 
                return;

            // Cache-friendly iteration - no LINQ, no allocations
            var indices = group.GetIndices();
            int count = indices.Count;
            for (int i = 0; i < count; i++)
            {
                int cacheIndex = indices[i];
                if (_active.TryGetValue(cacheIndex, out var pcb))
                {
                    pcb.Step(timing);
                }
            }
        }
        
        private async void OnDestroy()
        {
            TerminateAllImmediately();
        }
        
        #endregion

        #region Scene Collection

        /// <summary>
        /// Collects all MonoProcesses in the scene and registers them with proper hierarchy.
        /// </summary>
        public void CollectSceneProcesses()
        {
            var processes = FindObjectsByType<AbstractMonoProcess>(
                FindObjectsInactive.Exclude, 
                FindObjectsSortMode.None
            );

            if (OutputLogs)
            {
                Debug.Log($"[ProcessControl] Found {processes.Length} processes in scene");
                if (DetailedLogs)
                {
                    for (int i = 0; i < processes.Length; i++) Debug.Log($"\t\t[{i+1}] {processes[i]}");
                }
            }

            // Sort by hierarchy depth (parents first) to ensure proper registration order
            Array.Sort(processes, (a, b) => 
                GetTransformDepth(a.transform).CompareTo(GetTransformDepth(b.transform)));

            foreach (var process in processes)
            {
                if (process.IsInitialized) continue;
                
                var data = ProcessDataPacket.LocalDefault(process, this);
                Register(process, data, out _);
            }
            
            if (OutputLogs) Debug.Log($"[ProcessControl] {_hierarchy.GetDebugInfo()}");
        }

        private int GetTransformDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        #endregion

        #region Core Registration

        private void ResetProcessControl(EProcessControlState nextState)
        {
            TerminateAllImmediately();

            _active = new Dictionary<int, ProcessControlBlock>();
            _waiting = new HashSet<int>();
            _hierarchy.Clear();
            
            // Initialize stepping groups for each timing type
            _stepping = new Dictionary<EProcessStepTiming, StepGroup>
            {
                [EProcessStepTiming.None] = new StepGroup(),
                [EProcessStepTiming.Update] = new StepGroup(),
                [EProcessStepTiming.LateUpdate] = new StepGroup(),
                [EProcessStepTiming.FixedUpdate] = new StepGroup()
            };
            
            State = nextState;
        }

        public bool Register(AbstractMonoProcess process, out ProcessRelay relay)
        {
            return Register(process, ProcessDataPacket.RootDefault(), out relay);
        }

        public bool Register(AbstractMonoProcess process, ProcessDataPacket data, out ProcessRelay relay)
        {
            relay = default;
            
            if (process == null || process.IsInitialized) 
                return false;
            
            if (!CanAcceptNewProcesses()) 
                return false;
            
            var wrapper = new MonoWrapperProcess(process, data);
            var pcb = ProcessControlBlock.Generate(NextCacheIndex, wrapper, data.Handler);
            pcb.isMono = true;
            
            SetProcess(pcb);
            
            // Register in hierarchy
            _hierarchy.Register(process, pcb.CacheIndex);
            
            relay = pcb.Relay;
            data.Handler?.HandlerSubscribeProcess(relay);
            
            return true;
        }

        public bool Register(AbstractRuntimeProcess process, out ProcessRelay relay)
        {
            return Register(process, ProcessDataPacket.RootDefault(), out relay);
        }

        public bool Register(AbstractRuntimeProcess process, IGameplayProcessHandler handler, out ProcessRelay relay)
        {
            return Register(process, ProcessDataPacket.RootDefault(handler), out relay);
        }

        public bool Register(AbstractRuntimeProcess process, ProcessDataPacket data, out ProcessRelay relay)
        {
            relay = default;

            if (process == null || process.IsInitialized) 
                return false;
            
            if (!CanAcceptNewProcesses()) 
                return false;

            var wrapper = new RuntimeWrapperProcess(process, data);
            var pcb = ProcessControlBlock.Generate(NextCacheIndex, wrapper, data.Handler);
            
            SetProcess(pcb);
            
            relay = pcb.Relay;
            data.Handler?.HandlerSubscribeProcess(relay);

            return true;
        }
        
        public bool Unregister(ProcessControlBlock pcb)
        {
            if (_waiting.Contains(pcb.CacheIndex)) _waiting.Remove(pcb.CacheIndex);
            else RemoveFromStepping(pcb);

            pcb.Handler?.HandlerVoidProcess(pcb.CacheIndex);
            
            // Remove from hierarchy
            _hierarchy.Unregister(pcb.CacheIndex);

            if (OutputLogs)
            {
                Debug.Log($"[Process Control] Unregister process \"{pcb.Process.ProcessName}\" ({pcb.CacheIndex})");
            }
            
            return _active.Remove(pcb.CacheIndex);
        }
        
        public Dictionary<int, ProcessControlBlock> FetchActiveProcesses() => _active;
        
        private bool CanAcceptNewProcesses()
        {
            return State is not (EProcessControlState.Closed 
                or EProcessControlState.ClosedWaiting 
                or EProcessControlState.Terminated 
                or EProcessControlState.TerminatedImmediately);
        }
        
        #endregion

        #region Process State Control

        public void SetControlState(EProcessControlState state)
        {
            if (state == State) return;
            
            State = state;
            SetAllProcessesUponStateChange();
        }

        /// <summary>
        /// Runs the process. If cascading is enabled, also runs children.
        /// </summary>
        public bool Run(int cacheIndex, bool cascade = false)
        {
            if (!_active.TryGetValue(cacheIndex, out var pcb)) 
                return false;
            if (!ValidateStateTransition(pcb, EProcessState.Running)) 
                return false;

            pcb.QueueNextState(EProcessState.Running);
            
            if (cascade && CascadeToChildren)
            {
                CascadeStateToChildren(cacheIndex, EProcessState.Running);
            }
            
            return true;
        }
        
        /// <summary>
        /// Pauses/waits the process. If cascading is enabled, also pauses children.
        /// </summary>
        public bool Wait(int cacheIndex, bool cascade = true)
        {
            if (!_active.TryGetValue(cacheIndex, out var pcb)) 
                return false;
            if (!ValidateStateTransition(pcb, EProcessState.Waiting)) 
                return false;
            
            pcb.QueueNextState(EProcessState.Waiting);
            
            if (cascade && CascadeToChildren)
            {
                CascadeStateToChildren(cacheIndex, EProcessState.Waiting);
            }
            
            return true;
        }
        
        /// <summary>
        /// Alias for Wait - pauses the process.
        /// </summary>
        public bool Pause(int cacheIndex, bool cascade = true) => Wait(cacheIndex, cascade);
        
        /// <summary>
        /// Alias for Run - unpauses/resumes the process.
        /// </summary>
        public bool Unpause(int cacheIndex, bool cascade = true) => Run(cacheIndex, cascade);
        
        /// <summary>
        /// Terminates the process. Always cascades to children.
        /// </summary>
        public bool Terminate(int cacheIndex, bool cascade = true)
        {
            if (!_active.ContainsKey(cacheIndex)) 
                return false;

            // Always terminate children when terminating parent
            if (cascade)
            {
                CascadeStateToChildren(cacheIndex, EProcessState.Terminated);
            }
            
            _active[cacheIndex].QueueNextState(EProcessState.Terminated);
            return true;
        }
        
        public void TerminateImmediate(int cacheIndex, bool cascade = true)
        {
            if (!_active.ContainsKey(cacheIndex))
                return;

            // Always terminate children when terminating parent
            if (cascade)
            {
                var children = _hierarchy.GetCascadeTargets(cacheIndex, CascadeToSiblings, includeSelf: false, reverseList: true);
                foreach (int childIndex in children)
                {
                    if (_active.TryGetValue(childIndex, out var childPcb))
                    {
                        childPcb.ForceIntoState(EProcessState.Terminated);
                    }
                }
            }
            
            _active[cacheIndex].ForceIntoState(EProcessState.Terminated);
        }
        
        public void TerminateAll()
        {
            // Create copy to avoid modification during iteration
            var indices = new List<int>(_active.Keys);
            foreach (int cacheIndex in indices) Terminate(cacheIndex, false); // Don't cascade since we're terminating all anyway
        }

        public void TerminateAllImmediately()
        {
            if (_active == null || _active.Count == 0)
                return;
            
            try
            {
                var pcbs = _active.Values.ToArray();
                foreach (var task in pcbs)
                {
                    TerminateImmediate(task.CacheIndex, false);
                }
            }
            catch (InvalidOperationException)
            {
                
            }
        }

        private void CascadeStateToChildren(int sourceIndex, EProcessState state)
        {
            var targets = _hierarchy.GetCascadeTargets(sourceIndex, CascadeToSiblings, includeSelf: false);
            
            foreach (int targetIndex in targets)
            {
                if (!_active.TryGetValue(targetIndex, out var targetPcb))
                    continue;
                
                // Skip if already in target state
                if (targetPcb.State == state)
                    continue;
                
                switch (state)
                {
                    case EProcessState.Running:
                        if (ValidateStateTransition(targetPcb, EProcessState.Running))
                            targetPcb.QueueNextState(EProcessState.Running);
                        break;
                    case EProcessState.Waiting:
                        if (ValidateStateTransition(targetPcb, EProcessState.Waiting))
                            targetPcb.QueueNextState(EProcessState.Waiting);
                        break;
                    case EProcessState.Terminated:
                        targetPcb.QueueNextState(EProcessState.Terminated);
                        break;
                }
            }
        }
        
        #endregion

        #region Process Setup

        private void SetProcess(ProcessControlBlock pcb)
        {
            if (pcb.State == EProcessState.Created)
            {
                PrepareCreatedProcess(pcb);
            }
            
            var state = GetDefaultTransitionState(pcb);
            pcb.QueueNextState(state);
        }

        private void PrepareCreatedProcess(ProcessControlBlock pcb)
        {
            pcb.Process.InitializeWrapper();
            
            _waiting.Add(pcb.CacheIndex);
            _active[pcb.CacheIndex] = pcb;
            
            if (OutputLogs)
            {
                string msg = DetailedLogs 
                    ? $"[Process Control] Registered Process \"{pcb.Process.ProcessName}\" ({pcb.Handler})"
                    : $"[Process Control] Registered Process ({pcb.CacheIndex})";
                Debug.Log(msg);
            }
            
            pcb.Initialize();
        }
        
        private void SetAllProcessesUponStateChange()
        {
            if (State == EProcessControlState.TerminatedImmediately)
            {
                TerminateAllImmediately();
            }
            else if (State == EProcessControlState.Terminated)
            {
                TerminateAll();
            }
            // If ready, closed, waiting, closedwaiting
            else
            {
                var pcbs = _active.Values.ToArray();
                foreach (var pcb in pcbs)
                {
                    if (pcb.State == EProcessState.Created)
                    {
                        SetProcess(pcb);
                    }
                    else
                    {
                        var targetState = GetDefaultStateWhenControlChanged(pcb);
                        //targetState = GetDefaultTransitionState(pcb);
                        Debug.Log($"[{pcb.Process.ProcessName}] from {pcb.State} -> {targetState} --> {(DefaultTransitions.TryGetValue((State, pcb.Process.Lifecycle, pcb.State), out var _qState) ? _qState : $"{pcb.State}/")} ({State}) ({(pcb.State != targetState ? "Will Set" : "No Set")})");
                        
                        if (State is EProcessControlState.Ready or EProcessControlState.Waiting or EProcessControlState.ClosedWaiting)
                            pcb.ForceIntoState(targetState);
                        else
                            pcb.QueueNextState(targetState);

                        Debug.Log($"[{pcb.Process.ProcessName}] Queues Default State Next {pcb.State} -> {GetDefaultTransitionState(pcb)}");
                        pcb.QueueNextState(GetDefaultTransitionState(pcb));
                    }
                }
            }
        }
        
        #endregion

        #region Stepping Management

        private void MoveToStepping(ProcessControlBlock pcb)
        {
            var timing = pcb.Process.StepTiming;
            int priority = GetEffectivePriority(pcb);

            AddToStepGroup(pcb.CacheIndex, priority, timing);
        }

        private void AddToStepGroup(int cacheIndex, int priority, EProcessStepTiming timing)
        {
            switch (timing)
            {
                case EProcessStepTiming.None:
                    break;
                case EProcessStepTiming.Update:
                    _stepping[EProcessStepTiming.Update].Add(cacheIndex, priority);
                    break;
                case EProcessStepTiming.LateUpdate:
                    _stepping[EProcessStepTiming.LateUpdate].Add(cacheIndex, priority);
                    break;
                case EProcessStepTiming.FixedUpdate:
                    _stepping[EProcessStepTiming.FixedUpdate].Add(cacheIndex, priority);
                    break;
                case EProcessStepTiming.UpdateAndLate:
                    _stepping[EProcessStepTiming.Update].Add(cacheIndex, priority);
                    _stepping[EProcessStepTiming.LateUpdate].Add(cacheIndex, priority);
                    break;
                case EProcessStepTiming.UpdateAndFixed:
                    _stepping[EProcessStepTiming.Update].Add(cacheIndex, priority);
                    _stepping[EProcessStepTiming.FixedUpdate].Add(cacheIndex, priority);
                    break;
                case EProcessStepTiming.LateAndFixed:
                    _stepping[EProcessStepTiming.LateUpdate].Add(cacheIndex, priority);
                    _stepping[EProcessStepTiming.FixedUpdate].Add(cacheIndex, priority);
                    break;
                case EProcessStepTiming.UpdateFixedAndLate:
                    _stepping[EProcessStepTiming.Update].Add(cacheIndex, priority);
                    _stepping[EProcessStepTiming.LateUpdate].Add(cacheIndex, priority);
                    _stepping[EProcessStepTiming.FixedUpdate].Add(cacheIndex, priority);
                    break;
            }
        }
        
        private void RemoveFromStepping(ProcessControlBlock pcb)
        {
            var timing = pcb.Process.StepTiming;

            switch (timing)
            {
                case EProcessStepTiming.None:
                    break;
                case EProcessStepTiming.Update:
                    _stepping[EProcessStepTiming.Update].Remove(pcb.CacheIndex);
                    break;
                case EProcessStepTiming.LateUpdate:
                    _stepping[EProcessStepTiming.LateUpdate].Remove(pcb.CacheIndex);
                    break;
                case EProcessStepTiming.FixedUpdate:
                    _stepping[EProcessStepTiming.FixedUpdate].Remove(pcb.CacheIndex);
                    break;
                case EProcessStepTiming.UpdateAndLate:
                    _stepping[EProcessStepTiming.Update].Remove(pcb.CacheIndex);
                    _stepping[EProcessStepTiming.LateUpdate].Remove(pcb.CacheIndex);
                    break;
                case EProcessStepTiming.UpdateAndFixed:
                    _stepping[EProcessStepTiming.Update].Remove(pcb.CacheIndex);
                    _stepping[EProcessStepTiming.FixedUpdate].Remove(pcb.CacheIndex);
                    break;
                case EProcessStepTiming.LateAndFixed:
                    _stepping[EProcessStepTiming.LateUpdate].Remove(pcb.CacheIndex);
                    _stepping[EProcessStepTiming.FixedUpdate].Remove(pcb.CacheIndex);
                    break;
                case EProcessStepTiming.UpdateFixedAndLate:
                    _stepping[EProcessStepTiming.Update].Remove(pcb.CacheIndex);
                    _stepping[EProcessStepTiming.LateUpdate].Remove(pcb.CacheIndex);
                    _stepping[EProcessStepTiming.FixedUpdate].Remove(pcb.CacheIndex);
                    break;
            }
        }

        private int GetEffectivePriority(ProcessControlBlock pcb)
        {
            return pcb.Process.PriorityMethod switch
            {
                EProcessStepPriorityMethod.Manual => pcb.Process.StepPriority,
                EProcessStepPriorityMethod.First => 0,
                EProcessStepPriorityMethod.Last => int.MaxValue,
                _ => pcb.Process.StepPriority
            };
        }

        private void MoveToWaiting(ProcessControlBlock pcb)
        {
            _waiting.Add(pcb.CacheIndex);
        }

        private void RemoveFromWaiting(ProcessControlBlock pcb)
        {
            _waiting.Remove(pcb.CacheIndex);
        }
        
        #endregion

        #region Internal Process Communication

        public void ProcessWillRun(ProcessControlBlock pcb)
        {
            RemoveFromWaiting(pcb);
            MoveToStepping(pcb);
            pcb.TryRun();
        }

        public void ProcessWillWait(ProcessControlBlock pcb)
        {
            RemoveFromStepping(pcb);
            MoveToWaiting(pcb);
            pcb.TryWait();
        }

        public void ProcessWillTerminate(ProcessControlBlock pcb)
        {
            pcb.TryTerminate();
        }
        
        #endregion

        #region State Transition Logic

        // Simplified state transition tables using tuples for cleaner code
        private static readonly Dictionary<(EProcessControlState control, EProcessLifecycle lifecycle, EProcessState from), EProcessState> 
            DefaultTransitions = new()
        {
            // Ready state
            { (EProcessControlState.Ready, EProcessLifecycle.SelfTerminating, EProcessState.Created), EProcessState.Running },
            { (EProcessControlState.Ready, EProcessLifecycle.SelfTerminating, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.Ready, EProcessLifecycle.SelfTerminating, EProcessState.Waiting), EProcessState.Running },
            { (EProcessControlState.Ready, EProcessLifecycle.SelfTerminating, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Ready, EProcessLifecycle.RunThenWait, EProcessState.Created), EProcessState.Running },
            { (EProcessControlState.Ready, EProcessLifecycle.RunThenWait, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Ready, EProcessLifecycle.RunThenWait, EProcessState.Waiting), EProcessState.Running },
            { (EProcessControlState.Ready, EProcessLifecycle.RunThenWait, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Ready, EProcessLifecycle.RequiresControl, EProcessState.Created), EProcessState.Waiting },
            { (EProcessControlState.Ready, EProcessLifecycle.RequiresControl, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Ready, EProcessLifecycle.RequiresControl, EProcessState.Waiting), EProcessState.Running },
            { (EProcessControlState.Ready, EProcessLifecycle.RequiresControl, EProcessState.Terminated), EProcessState.Terminated },
            
            // Waiting state - everything goes to Waiting except Terminated
            { (EProcessControlState.Waiting, EProcessLifecycle.SelfTerminating, EProcessState.Created), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.SelfTerminating, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.SelfTerminating, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.SelfTerminating, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Waiting, EProcessLifecycle.RunThenWait, EProcessState.Created), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.RunThenWait, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.RunThenWait, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.RunThenWait, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Waiting, EProcessLifecycle.RequiresControl, EProcessState.Created), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.RequiresControl, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.RequiresControl, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.RequiresControl, EProcessState.Terminated), EProcessState.Terminated },
            
            // Terminated states - everything goes to Terminated
            { (EProcessControlState.Terminated, EProcessLifecycle.SelfTerminating, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.SelfTerminating, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.SelfTerminating, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.SelfTerminating, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Terminated, EProcessLifecycle.RunThenWait, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.RunThenWait, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.RunThenWait, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.RunThenWait, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Terminated, EProcessLifecycle.RequiresControl, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.RequiresControl, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.RequiresControl, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.RequiresControl, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.SelfTerminating, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.SelfTerminating, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.SelfTerminating, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.SelfTerminating, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RunThenWait, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RunThenWait, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RunThenWait, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RunThenWait, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RequiresControl, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RequiresControl, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RequiresControl, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.RequiresControl, EProcessState.Terminated), EProcessState.Terminated },
            
            // Closed - let existing processes run/wait naturally
            { (EProcessControlState.Closed, EProcessLifecycle.SelfTerminating, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.Closed, EProcessLifecycle.SelfTerminating, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.Closed, EProcessLifecycle.SelfTerminating, EProcessState.Waiting), EProcessState.Running },
            { (EProcessControlState.Closed, EProcessLifecycle.SelfTerminating, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Closed, EProcessLifecycle.RunThenWait, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.Closed, EProcessLifecycle.RunThenWait, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Closed, EProcessLifecycle.RunThenWait, EProcessState.Waiting), EProcessState.Running },
            { (EProcessControlState.Closed, EProcessLifecycle.RunThenWait, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.Closed, EProcessLifecycle.RequiresControl, EProcessState.Created), EProcessState.Waiting },
            { (EProcessControlState.Closed, EProcessLifecycle.RequiresControl, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Closed, EProcessLifecycle.RequiresControl, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.Closed, EProcessLifecycle.RequiresControl, EProcessState.Terminated), EProcessState.Terminated },
            
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.SelfTerminating, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.SelfTerminating, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.SelfTerminating, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.SelfTerminating, EProcessState.Terminated), EProcessState.Terminated },

            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RunThenWait, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RunThenWait, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RunThenWait, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RunThenWait, EProcessState.Terminated), EProcessState.Terminated },

            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RequiresControl, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RequiresControl, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RequiresControl, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.RequiresControl, EProcessState.Terminated), EProcessState.Terminated },
        };

        public EProcessState GetDefaultTransitionState(ProcessControlBlock pcb)
        {
            var key = (State, pcb.Process.Lifecycle, pcb.State);
            
            if (DefaultTransitions.TryGetValue(key, out var result))
                return result;
            
            // Fallback for unhandled cases
            if (OutputLogs)
                Debug.LogWarning($"[ProcessControl] Unhandled state transition: {key}");
            
            return pcb.State; // Stay in current state
        }

        private EProcessState GetDefaultStateWhenControlChanged(ProcessControlBlock pcb)
        {
            // When control state changes, determine what state processes should be in
            return State switch
            {
                EProcessControlState.Ready => pcb.Process.Lifecycle switch
                {
                    EProcessLifecycle.SelfTerminating => pcb.State == EProcessState.Terminated 
                        ? EProcessState.Terminated 
                        : EProcessState.Running,
                    EProcessLifecycle.RunThenWait => (pcb.State == EProcessState.Waiting && (!pcb.HasRun || pcb.MidRun))
                        ? EProcessState.Running 
                        : pcb.State,
                    EProcessLifecycle.RequiresControl => (pcb.State == EProcessState.Waiting && pcb.MidRun)
                        ? EProcessState.Running 
                        : pcb.State,
                    _ => pcb.State
                },
                EProcessControlState.Waiting or EProcessControlState.ClosedWaiting => 
                    pcb.State == EProcessState.Terminated ? EProcessState.Terminated : EProcessState.Waiting,
                EProcessControlState.Closed => pcb.Process.Lifecycle switch
                {

                    EProcessLifecycle.SelfTerminating => pcb.State == EProcessState.Terminated 
                        ? EProcessState.Terminated 
                        : EProcessState.Running,
                    EProcessLifecycle.RunThenWait => (pcb.State == EProcessState.Waiting && (!pcb.HasRun || pcb.MidRun))
                        ? EProcessState.Running 
                        : pcb.State,
                    EProcessLifecycle.RequiresControl => (pcb.State == EProcessState.Waiting && pcb.MidRun)
                        ? EProcessState.Running 
                        : pcb.State,
                    _ => pcb.State
                },
                EProcessControlState.Terminated or EProcessControlState.TerminatedImmediately => EProcessState.Terminated,
                _ => pcb.State
            };
        }

        private bool ValidateStateTransition(ProcessControlBlock pcb, EProcessState to)
        {
            var from = pcb.State;
            var lifecycle = pcb.Process.Lifecycle;
            
            // Can't transition to Created
            if (to == EProcessState.Created) return false;
            
            // Already in target state
            if (from == to) return false;
            
            // Common transitions
            return (from, to, lifecycle) switch
            {
                // Can always terminate from any state (except Created which shouldn't be in active)
                (_, EProcessState.Terminated, _) => true,
                
                // Running transitions
                (EProcessState.Created, EProcessState.Running, EProcessLifecycle.SelfTerminating) => true,
                (EProcessState.Created, EProcessState.Running, EProcessLifecycle.RunThenWait) => true,
                (EProcessState.Waiting, EProcessState.Running, _) => true,
                
                // Waiting transitions
                (EProcessState.Running, EProcessState.Waiting, _) => true,
                (EProcessState.Created, EProcessState.Waiting, EProcessLifecycle.RequiresControl) => true,
                
                _ => false
            };
        }
        
        #endregion

        #region Public API

        /// <summary>
        /// Gets all child process IDs for a given process
        /// </summary>
        public List<int> GetChildProcessIds(int cacheIndex)
        {
            return _hierarchy.GetCascadeTargets(cacheIndex, includeSiblings: false, includeSelf: false);
        }

        /// <summary>
        /// Gets all sibling process IDs (same GameObject) for a given process
        /// </summary>
        public List<int> GetSiblingProcessIds(int cacheIndex)
        {
            return _hierarchy.GetSiblingProcessIds(cacheIndex);
        }

        /// <summary>
        /// Checks if a process exists and is active
        /// </summary>
        public bool IsActive(int cacheIndex) => _active.ContainsKey(cacheIndex);

        /// <summary>
        /// Checks if a process is currently running (not waiting)
        /// </summary>
        public bool IsRunning(int cacheIndex)
        {
            return _active.TryGetValue(cacheIndex, out var pcb) && pcb.State == EProcessState.Running;
        }

        /// <summary>
        /// Checks if a process is currently waiting/paused
        /// </summary>
        public bool IsWaiting(int cacheIndex) => _waiting.Contains(cacheIndex);

        /// <summary>
        /// Gets the ProcessControlBlock for direct access (use cautiously)
        /// </summary>
        public bool TryGetPCB(int cacheIndex, out ProcessControlBlock pcb)
        {
            return _active.TryGetValue(cacheIndex, out pcb);
        }
        
        #endregion
        
        #region Handler Interface

        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (ProcessControl)handler == this;
        }

        public bool HandlerProcessIsSubscribed(ProcessRelay relay)
        {
            return _active.ContainsKey(relay.CacheIndex);
        }

        public void HandlerSubscribeProcess(ProcessRelay relay)
        {
            // No additional action needed
        }
        
        public bool HandlerVoidProcess(int processIndex)
        {
            return true;
        }
        
        #endregion
    }

    public enum EProcessControlState
    {
        Ready,              // Normal operation
        Waiting,            // Accept register/unregister but don't run processes
        Closed,             // Don't accept new processes but run active ones
        ClosedWaiting,      // Don't accept new processes and don't run active ones
        Terminated,         // Terminate all processes gracefully
        TerminatedImmediately // Terminate all processes immediately
    }
}