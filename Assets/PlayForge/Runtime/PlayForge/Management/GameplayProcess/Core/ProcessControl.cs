using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
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

        public AbstractMonoProcessInstantiator CustomInstantiator;
        
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

        private Dictionary<int, ProcessWatcher> _watchers = new();

        // Group process registry — maps user-facing Tag to the GroupProcess instance
        private Dictionary<Tag, GroupProcess> _groups = new();

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

            private void RebuildIfDirty()
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
        
        #region Process Watching
        
        private struct ProcessWatcher
        {
            public struct WatcherActions
            {
                public Action<EProcessState, EProcessState, ProcessDataPacket> onStateChange;
                public Action<ProcessDataPacket> onUnregister;
            }

            private Dictionary<object, WatcherActions> actions;

            public int ActionCount => actions.Count;

            public readonly ProcessRelay watching;

            public ProcessWatcher(ProcessRelay toWatch, object caller, Action<EProcessState, EProcessState, ProcessDataPacket> onStateChange = null, Action<ProcessDataPacket> onUnregister = null)
            {
                actions = new Dictionary<object, WatcherActions>();
                actions[caller] = new WatcherActions()
                {
                    onStateChange = onStateChange,
                    onUnregister = onUnregister
                };

                watching = toWatch;
            }

            public void AddActions(object caller, Action<EProcessState, EProcessState, ProcessDataPacket> onStateChange, Action<ProcessDataPacket> onUnregister)
            {
                if (!actions.TryGetValue(caller, out var _actions))
                {
                    actions[caller] = new WatcherActions()
                    {
                        onStateChange = onStateChange,
                        onUnregister = onUnregister
                    };
                    return;
                }
                
                // Update onStateChange
                if (_actions.onStateChange is not null)
                {
                    _actions.onStateChange = (oldState, newState, data) =>
                    {
                        _actions.onStateChange.Invoke(oldState, newState, data);
                        onStateChange?.Invoke(oldState, newState, data);
                    };
                }
                else _actions.onStateChange = onStateChange;
                    
                // Update onUnregister
                if (_actions.onUnregister is not null)
                {
                    _actions.onUnregister = (data) =>
                    {
                        _actions.onUnregister.Invoke(data);
                        onUnregister?.Invoke(data);
                    };
                }
                else _actions.onUnregister = onUnregister;
            }

            public WatcherActions GetActions(object caller)
            {
                return actions.TryGetValue(caller, out var _actions) ? _actions : default;
            }

            public bool RemoveActions(object caller)
            {
                return actions.Remove(caller);
            }

            public void RunOnStateChange(EProcessState oldState, EProcessState newState, ProcessDataPacket data)
            {
                foreach (var _actions in actions.Values) _actions.onStateChange?.Invoke(oldState, newState, data);
            }
            
            public void RunOnUnregister(ProcessDataPacket data)
            {
                foreach (var _actions in actions.Values) _actions.onUnregister?.Invoke(data);
            }
        }

        public void WatchProcess(ProcessRelay toWatch, object caller, Action<ProcessDataPacket> onStartWatching = null, Action<EProcessState, EProcessState, ProcessDataPacket> onStateChange = null,
            Action<ProcessDataPacket> onUnregister = null)
        {
            if (!Instance._watchers.TryGetValue(toWatch.CacheIndex, out var watcher))
            {
                Instance._watchers[toWatch.CacheIndex] = new ProcessWatcher(toWatch, caller, onStateChange, onUnregister);
                return;
            }

            watcher.AddActions(caller, onStateChange, onUnregister);
            
            onStartWatching?.Invoke(toWatch.Wrapper.Data);
        }

        public void StopWatchingProcess(ProcessRelay toWatch)
        {
            if (!_watchers.ContainsKey(toWatch.CacheIndex)) return;
            _watchers.Remove(toWatch.CacheIndex);
        }
        
        public void StopWatchingProcess(ProcessRelay toWatch, object caller)
        {
            if (!_watchers.ContainsKey(toWatch.CacheIndex)) return;
            _watchers[toWatch.CacheIndex].RemoveActions(caller);
            if (_watchers[toWatch.CacheIndex].ActionCount <= 0) _watchers.Remove(toWatch.CacheIndex);
        }

        private void CheckWatcherOnStateChange(ProcessControlBlock pcb)
        {
            if (!_watchers.ContainsKey(pcb.CacheIndex)) return;
            _watchers[pcb.CacheIndex].RunOnStateChange(pcb.LastState, pcb.State, pcb.Wrapper.Data);
        }

        private void CheckWatcherOnUnregister(ProcessControlBlock pcb)
        {
            if (!_watchers.ContainsKey(pcb.CacheIndex)) return;
            _watchers[pcb.CacheIndex].RunOnUnregister(pcb.Wrapper.Data);
            
            StopWatchingProcess(pcb.Relay);
        }
        
        #endregion
        
        #region Readable Definition
        
        public string GetName()
        {
            return "Process Control";
        }
        public string GetDescription()
        {
            return "Process Control manages active processes.";
        }
        public Texture2D GetDefaultIcon()
        {
            return null;
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

        // Reusable snapshot buffer to avoid allocation during stepping.
        // A step callback (e.g. a Synchronous process self-terminating) can mutate the
        // live indices list, so we iterate a snapshot instead.
        private readonly List<int> _stepSnapshot = new();

        private void StepProcesses(EProcessStepTiming timing)
        {
            if (State is EProcessControlState.Waiting or EProcessControlState.TerminatedImmediately)
                return;

            if (!_stepping.TryGetValue(timing, out var group))
                return;

            // Snapshot the indices before iterating.
            // A Synchronous process may self-terminate during Step(), which modifies the
            // live list via RemoveFromStepping. Iterating a snapshot keeps the loop safe.
            _stepSnapshot.Clear();
            _stepSnapshot.AddRange(group.GetIndices());

            int count = _stepSnapshot.Count;
            for (int i = 0; i < count; i++)
            {
                int cacheIndex = _stepSnapshot[i];
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

            // Sort by effective priority first, then by hierarchy depth (parents before children)
            Array.Sort(processes, (a, b) =>
            {
                int priorityA = GetEffectivePriority(a);
                int priorityB = GetEffectivePriority(b);
                int cmp = priorityA.CompareTo(priorityB);
                return cmp != 0 ? cmp : GetTransformDepth(a.transform).CompareTo(GetTransformDepth(b.transform));
            });

            foreach (var process in processes)
            {
                if (process.IsInitialized)
                {
                    continue;
                }
        
                var data = ProcessDataPacket.SceneRoot();
                Register(process, data, out _);
            }
    
            if (OutputLogs) Debug.Log($"[ProcessControl] {_hierarchy.GetDebugInfo()}");
        }

        private static int GetEffectivePriority(AbstractMonoProcess process)
        {
            return process.PriorityMethod switch
            {
                EProcessStepPriorityMethod.First => 0,
                EProcessStepPriorityMethod.Last => int.MaxValue,
                EProcessStepPriorityMethod.Manual => process.ProcessStepPriority,
                _ => 0
            };
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
            _groups = new Dictionary<Tag, GroupProcess>();

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
        
        #region Mono Process
        public static bool Register(AbstractMonoProcess process, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterMonoProcess(process, GameRoot.Instance, ProcessDataPacket.SceneRoot(), out relay);
        }
        public static bool Register(AbstractMonoProcess process, AbilityDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterMonoProcess(process, data.EffectOrigin.GetOwner(), data, out relay);
        }
        public static bool Register(AbstractMonoProcess process, ProcessDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterMonoProcess(process, GameRoot.Instance, data, out relay);
        }
        public static bool Register(AbstractMonoProcess process, IGameplayProcessHandler handler, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterMonoProcess(process, handler, ProcessDataPacket.SceneRoot(), out relay);
        }
        public static bool Register(AbstractMonoProcess process, IGameplayProcessHandler handler, ProcessDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterMonoProcess(process, handler, data, out relay);
        }

        private bool Internal_RegisterMonoProcess(AbstractMonoProcess process, IGameplayProcessHandler handler, ProcessDataPacket data, out ProcessRelay relay)
        {
            relay = default;
            
            if (process == null || process.IsInitialized) 
                return false;
            
            if (!CanAcceptNewProcesses()) 
                return false;
            
            var wrapper = new MonoWrapperProcess(process, data, handler);
            var pcb = ProcessControlBlock.Generate(NextCacheIndex, wrapper);
            pcb.isMono = true;
            
            // Register in hierarchy
            _hierarchy.Register(process, pcb.CacheIndex);
            
            relay = pcb.Relay;
            handler?.HandlerSubscribeProcess(relay);
            data.HandlerSubscribeProcess(relay);
            
            SetProcess(pcb);
            
            return true;
        }
        
        #endregion
        
        #region Runtime Process
        public static bool Register(AbstractRuntimeProcess process, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterRuntimeProcess(process, GameRoot.Instance, ProcessDataPacket.Default(), out relay);
        }
        public static bool Register(AbstractRuntimeProcess process, AbilityDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterRuntimeProcess(process, data.EffectOrigin.GetOwner(), data, out relay);
        }
        public static bool Register(AbstractRuntimeProcess process, IGameplayProcessHandler handler, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterRuntimeProcess(process, handler, ProcessDataPacket.Default(), out relay);
        }
        public static bool Register(AbstractRuntimeProcess process, ProcessDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterRuntimeProcess(process, GameRoot.Instance, data, out relay);
        }
        public static bool Register(AbstractRuntimeProcess process, IGameplayProcessHandler handler, ProcessDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterRuntimeProcess(process, handler, data, out relay);
        }
        private bool Internal_RegisterRuntimeProcess(AbstractRuntimeProcess process, IGameplayProcessHandler handler, ProcessDataPacket data, out ProcessRelay relay)
        {
            relay = default;

            if (process == null || process.IsInitialized) 
                return false;
            
            if (!CanAcceptNewProcesses()) 
                return false;

            var wrapper = new RuntimeWrapperProcess(process, data, handler);
            var pcb = ProcessControlBlock.Generate(NextCacheIndex, wrapper);
            
            relay = pcb.Relay;
            handler?.HandlerSubscribeProcess(relay);
            data.HandlerSubscribeProcess(relay);
            
            SetProcess(pcb);

            return true;
        }
        #endregion
        
        #region Task Sequences (Async)

        public static bool Register(TaskSequence sequence, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(sequence), GameRoot.Instance, SequenceDataPacket.SceneRoot(), out relay);
        }
        public static bool Register(TaskSequence sequence, AbilityDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(sequence), data.EffectOrigin.GetOwner(), data, out relay);
        }
        public static bool Register(TaskSequence sequence, IGameplayProcessHandler handler, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(sequence), handler, SequenceDataPacket.SceneRoot(), out relay);
        }
        public static bool Register(TaskSequence sequence, SequenceDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(sequence), GameRoot.Instance, data, out relay);
        }
        public static bool Register(TaskSequence sequence, IGameplayProcessHandler handler, SequenceDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(sequence), handler, data, out relay);
        }
        
        public static bool Register(TaskSequenceChain chain, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(chain), GameRoot.Instance, SequenceDataPacket.SceneRoot(), out relay);
        }
        public static bool Register(TaskSequenceChain chain, AbilityDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(chain), data.EffectOrigin.GetOwner(), data, out relay);
        }
        public static bool Register(TaskSequenceChain chain, IGameplayProcessHandler handler, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(chain), handler, SequenceDataPacket.SceneRoot(), out relay);
        }
        public static bool Register(TaskSequenceChain chain, SequenceDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(chain), GameRoot.Instance, data, out relay);
        }
        public static bool Register(TaskSequenceChain chain, IGameplayProcessHandler handler, SequenceDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterAsyncSequence(new TaskSequenceProcess(chain), handler, data, out relay);
        }
        private bool Internal_RegisterAsyncSequence(TaskSequenceProcess process, IGameplayProcessHandler handler, SequenceDataPacket data, out ProcessRelay relay)
        {
            return Register(process, handler, data, out relay);
        }

        #endregion

        #region Group Processes

        /// <summary>
        /// Registers a pre-configured GroupProcess under the given tag.
        /// Use this when you need to set MaxMembers, OverflowPolicy, callbacks, etc.
        /// before any members are added.
        /// </summary>
        public static bool RegisterGroup(Tag groupTag, GroupProcess group, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterGroup(groupTag, group, ProcessDataPacket.Default(), out relay);
        }

        public static bool RegisterGroup(Tag groupTag, GroupProcess group, ProcessDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterGroup(groupTag, group, data, out relay);
        }

        private bool Internal_RegisterGroup(Tag groupTag, GroupProcess group, ProcessDataPacket data, out ProcessRelay relay)
        {
            relay = default;
            if (group == null || _groups.ContainsKey(groupTag)) return false;

            if (!Register(group, data, out relay)) return false;

            _groups[groupTag] = group;

            // When the group terminates, remove it from the registry
            WatchProcess(relay, group, onUnregister: _ => _groups.Remove(groupTag));

            return true;
        }

        /// <summary>
        /// Registers a runtime process as a member of the group identified by groupTag.
        /// If the group doesn't exist yet, it is auto-created with default settings.
        /// The member uses the group's shared data packet by default.
        /// </summary>
        public static bool RegisterWithGroup(Tag groupTag, AbstractRuntimeProcess process, out ProcessRelay relay, bool useSharedData = true)
        {
            return Instance.Internal_RegisterWithGroup(groupTag, process, null, useSharedData, out relay);
        }

        /// <summary>
        /// Registers a runtime process as a member with an explicit standalone data packet.
        /// </summary>
        public static bool RegisterWithGroup(Tag groupTag, AbstractRuntimeProcess process, ProcessDataPacket data, out ProcessRelay relay)
        {
            return Instance.Internal_RegisterWithGroup(groupTag, process, data, false, out relay);
        }

        private bool Internal_RegisterWithGroup(Tag groupTag, AbstractRuntimeProcess process, ProcessDataPacket explicitData, bool useSharedData, out ProcessRelay relay)
        {
            relay = default;
            if (process == null) return false;

            // Auto-create the group if it doesn't exist
            if (!_groups.TryGetValue(groupTag, out var group))
            {
                group = new GroupProcess(groupTag.GetName());
                if (!Internal_RegisterGroup(groupTag, group, ProcessDataPacket.Default(), out _))
                    return false;
            }

            // Determine which data packet the member should use
            var data = useSharedData && explicitData == null
                ? group.Data   // Share the group's data packet
                : explicitData ?? ProcessDataPacket.Default();

            // Register the process normally through ProcessControl
            if (!Register(process, data, out relay))
                return false;

            // Add as a group member
            if (!group.AddMember(relay))
            {
                // Registration succeeded but group rejected the member (full + Reject policy).
                // Terminate the process since it was registered but can't join the group.
                relay.Terminate();
                relay = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tries to get the GroupProcess associated with a tag.
        /// </summary>
        public static bool TryGetGroup(Tag groupTag, out GroupProcess group)
        {
            return Instance._groups.TryGetValue(groupTag, out group);
        }

        /// <summary>
        /// Checks whether a group with the given tag exists and is active.
        /// </summary>
        public static bool HasGroup(Tag groupTag)
        {
            return Instance._groups.ContainsKey(groupTag);
        }

        #endregion

        public bool Unregister(ProcessControlBlock pcb)
        {
            if (pcb.Relay is null) return false;
            
            if (_waiting.Contains(pcb.CacheIndex)) _waiting.Remove(pcb.CacheIndex);
            else RemoveFromStepping(pcb);
            
            CheckWatcherOnUnregister(pcb);

            pcb.Handler?.HandlerVoidProcess(pcb.Relay);
            
            // Remove from hierarchy
            _hierarchy.Unregister(pcb.CacheIndex);

            if (OutputLogs)
            {
                if (DetailedLogs) Debug.Log($"[Process Control] Unregister process: {pcb.Wrapper.ProcessName} ({pcb.Handler?.GetName() ?? "No handler"})");
                else Debug.Log($"[Process Control] Unregister process: {pcb.Wrapper.ProcessName}");
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
        
        public bool TerminateImmediate(int cacheIndex, bool cascade = true)
        {
            if (!_active.ContainsKey(cacheIndex)) return false;

            // Always terminate children when terminating parent
            if (cascade)
            {
                var siblings = _hierarchy.GetSiblingProcessIds(cacheIndex);
                var children = _hierarchy.GetCascadeTargets(cacheIndex, CascadeToSiblings, includeSelf: false, reverseList: true);
                foreach (int childIndex in children)
                {
                    if (!_active.TryGetValue(childIndex, out var childPcb)) continue;

                    // Skip siblings that opted out of cascade
                    if (siblings.Contains(childIndex) && !childPcb.Wrapper.ParticipateInSiblingCascade)
                        continue;

                    childPcb.ForceIntoState(EProcessState.Terminated);
                }
            }

            _active[cacheIndex].ForceIntoState(EProcessState.Terminated);

            return true;
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
            var siblings = _hierarchy.GetSiblingProcessIds(sourceIndex);
            var targets = _hierarchy.GetCascadeTargets(sourceIndex, CascadeToSiblings, includeSelf: false);

            foreach (int targetIndex in targets)
            {
                if (!_active.TryGetValue(targetIndex, out var targetPcb))
                    continue;

                // Skip if already in target state
                if (targetPcb.State == state)
                    continue;

                // Skip siblings that opted out of cascade
                if (siblings.Contains(targetIndex) && !targetPcb.Wrapper.ParticipateInSiblingCascade)
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
            pcb.Wrapper.InitializeWrapper();
            
            _waiting.Add(pcb.CacheIndex);
            _active[pcb.CacheIndex] = pcb;
            
            if (OutputLogs)
            {
                string msg = DetailedLogs 
                    ? $"[Process Control] Registered Process \"{pcb.Wrapper.ProcessName}\" ({pcb.Handler})"
                    : $"[Process Control] Registered Process: {pcb.Wrapper.ProcessName}";
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
                        //Debug.Log($"[{pcb.Process.ProcessName}] from {pcb.State} -> {targetState} --> {(DefaultTransitions.TryGetValue((State, pcb.Process.Lifecycle, pcb.State), out var _qState) ? _qState : $"{pcb.State}/")} ({State}) ({(pcb.State != targetState ? "Will Set" : "No Set")})");
                        
                        if (State is EProcessControlState.Ready or EProcessControlState.Waiting or EProcessControlState.ClosedWaiting)
                            pcb.ForceIntoState(targetState);
                        else
                            pcb.QueueNextState(targetState);

                        //Debug.Log($"[{pcb.Process.ProcessName}] Queues Default State Next {pcb.State} -> {GetDefaultTransitionState(pcb)}");
                        pcb.QueueNextState(GetDefaultTransitionState(pcb));
                    }
                }
            }
        }
        
        #endregion

        #region Stepping Management

        private void MoveToStepping(ProcessControlBlock pcb)
        {
            var timing = pcb.Wrapper.StepTiming;
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
            var timing = pcb.Wrapper.StepTiming;

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
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private int GetEffectivePriority(ProcessControlBlock pcb)
        {
            return pcb.Wrapper.PriorityMethod switch
            {
                EProcessStepPriorityMethod.Manual => pcb.Wrapper.StepPriority,
                EProcessStepPriorityMethod.First => 0,
                EProcessStepPriorityMethod.Last => int.MaxValue,
                _ => pcb.Wrapper.StepPriority
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

        public void ProcessWillChangeState(ProcessControlBlock pcb, EProcessState state)
        {
            switch (state)
            {
                case EProcessState.Created:
                    return;
                case EProcessState.Running:
                    ProcessWillRun(pcb);
                    break;
                case EProcessState.Waiting:
                    ProcessWillWait(pcb);
                    break;
                case EProcessState.Terminated:
                    ProcessWillTerminate(pcb);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            CheckWatcherOnStateChange(pcb);
        }

        private void ProcessWillRun(ProcessControlBlock pcb)
        {
            RemoveFromWaiting(pcb);
            MoveToStepping(pcb);
            pcb.TryRun();
        }

        private void ProcessWillWait(ProcessControlBlock pcb)
        {
            RemoveFromStepping(pcb);
            MoveToWaiting(pcb);
            pcb.TryWait();
        }

        private void ProcessWillTerminate(ProcessControlBlock pcb)
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
            
            { (EProcessControlState.Ready, EProcessLifecycle.Synchronous, EProcessState.Created), EProcessState.Running },
            { (EProcessControlState.Ready, EProcessLifecycle.Synchronous, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Ready, EProcessLifecycle.Synchronous, EProcessState.Waiting), EProcessState.Running },
            { (EProcessControlState.Ready, EProcessLifecycle.Synchronous, EProcessState.Terminated), EProcessState.Terminated },
            
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
            
            { (EProcessControlState.Waiting, EProcessLifecycle.Synchronous, EProcessState.Created), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.Synchronous, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.Synchronous, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.Waiting, EProcessLifecycle.Synchronous, EProcessState.Terminated), EProcessState.Terminated },
            
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
            
            { (EProcessControlState.Terminated, EProcessLifecycle.Synchronous, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.Synchronous, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.Synchronous, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.Terminated, EProcessLifecycle.Synchronous, EProcessState.Terminated), EProcessState.Terminated },
            
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
            
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.Synchronous, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.Synchronous, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.Synchronous, EProcessState.Waiting), EProcessState.Terminated },
            { (EProcessControlState.TerminatedImmediately, EProcessLifecycle.Synchronous, EProcessState.Terminated), EProcessState.Terminated },
            
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
            
            { (EProcessControlState.Closed, EProcessLifecycle.Synchronous, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.Closed, EProcessLifecycle.Synchronous, EProcessState.Running), EProcessState.Terminated },
            { (EProcessControlState.Closed, EProcessLifecycle.Synchronous, EProcessState.Waiting), EProcessState.Running },
            { (EProcessControlState.Closed, EProcessLifecycle.Synchronous, EProcessState.Terminated), EProcessState.Terminated },
            
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
            
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.Synchronous, EProcessState.Created), EProcessState.Terminated },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.Synchronous, EProcessState.Running), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.Synchronous, EProcessState.Waiting), EProcessState.Waiting },
            { (EProcessControlState.ClosedWaiting, EProcessLifecycle.Synchronous, EProcessState.Terminated), EProcessState.Terminated },
        };

        public EProcessState GetDefaultTransitionState(ProcessControlBlock pcb)
        {
            var key = (State, pcb.Wrapper.Lifecycle, pcb.State);
            
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
                EProcessControlState.Ready => pcb.Wrapper.Lifecycle switch
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
                    EProcessLifecycle.Synchronous => pcb.State == EProcessState.Terminated 
                        ? EProcessState.Terminated 
                        : EProcessState.Running,
                    _ => pcb.State
                },
                EProcessControlState.Waiting or EProcessControlState.ClosedWaiting => 
                    pcb.State == EProcessState.Terminated ? EProcessState.Terminated : EProcessState.Waiting,
                EProcessControlState.Closed => pcb.Wrapper.Lifecycle switch
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
                    EProcessLifecycle.Synchronous => pcb.State == EProcessState.Terminated 
                        ? EProcessState.Terminated 
                        : EProcessState.Running,
                    _ => pcb.State
                },
                EProcessControlState.Terminated or EProcessControlState.TerminatedImmediately => EProcessState.Terminated,
                _ => pcb.State
            };
        }

        private bool ValidateStateTransition(ProcessControlBlock pcb, EProcessState to)
        {
            var from = pcb.State;
            var lifecycle = pcb.Wrapper.Lifecycle;
            
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
                (EProcessState.Created, EProcessState.Running, EProcessLifecycle.Synchronous) => true,
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
        /// Checks if a process exists
        /// </summary>
        public bool IsRegistered(int cacheIndex) => _active.ContainsKey(cacheIndex);

        /// <summary>
        /// Checks if a process is currently running (not waiting)
        /// </summary>
        public bool IsRunning(int cacheIndex)
        {
            return StateIs(cacheIndex, EProcessState.Running);
        }

        /// <summary>
        /// Checks if a process is currently waiting/paused
        /// </summary>
        public bool IsWaiting(int cacheIndex) => _waiting.Contains(cacheIndex);

        public bool StateIs(int cacheIndex, EProcessState state) => _active.TryGetValue(cacheIndex, out var pcb) && pcb.State == state;
        
        public bool TryGetQueuedState(int cacheIndex, out EProcessState queuedState)
        {
            if (TryGetPCB(cacheIndex, out var pcb))
            {
                queuedState = pcb.QueuedState;
                return true;
            }

            queuedState = EProcessState.Created;
            return false;
        }

        /// <summary>
        /// Gets the ProcessControlBlock for direct access (use cautiously)
        /// </summary>
        public bool TryGetPCB(int cacheIndex, out ProcessControlBlock pcb)
        {
            return _active.TryGetValue(cacheIndex, out pcb);
        }
        
        #endregion
        
        #region Handler Interface
        public ProcessRelay[] GetRelays()
        {
            return _active.Values.Select(pcb => pcb.Relay).ToArray();
        }

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
        
        public bool HandlerVoidProcess(ProcessRelay relay)
        {
            return true;
        }
        public AbstractMonoProcessInstantiator GetInstantiator(AbstractMonoProcess mono)
        {
            return CustomInstantiator;
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