using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ProcessControl : MonoBehaviour, IGameplayProcessHandler, IManagerial
    {
        public static ProcessControl Instance;

        // Statics outlive play mode when domain reload is off, so the last session's destroyed ProcessControl was
        // still Instance when the next began (PC-26)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearInstance() => Instance = null;

        [Header("Process Control")]
        [SerializeField] private EProcessControlState StartState = EProcessControlState.Ready;
        [SerializeField] private new bool DontDestroyOnLoad = true;
        [SerializeField] private bool CollectOnAwake = true;
        
        [Header("Hierarchy Settings")]
        [Tooltip("When true, pausing/terminating a parent will affect all children")]
        [SerializeField] private bool CascadeToChildren = true;
        [Tooltip("When true, processes on the same GameObject are treated as a group")]
        [SerializeField] private bool CascadeToSiblings = true;
        
        [Header("Fixed Tick")]
        [Tooltip("Ticks a second of game time. Each steps every process, its end of frame with it, and lets each " +
                 "sequence's wait go on, a fixed step of time, and a frame runs as many as its time holds, so a run " +
                 "comes out the same at any frame rate. 0 steps once a frame (FEAT-7)")]
        [SerializeField] private int fixedTickRate;
        [Tooltip("The most ticks a frame runs, so a long frame doesn't fall further behind: the rest of its time is " +
                 "dropped")]
        [SerializeField] private int maxTicksPerFrame = 8;

        [Space]
        public bool OutputLogs;
        public bool DetailedLogs = true;

        public AbstractMonoProcessInstantiator CustomInstantiator;
        
        /// <summary>
        /// The world of processes this ProcessControl steps from Unity's loop (FEAT-10). Its bookkeeping was this class's own,
        /// so no process ran without a ProcessControl in a scene; it's a plain class now, which code can own and step too.
        /// </summary>
        public ProcessWorld World => world ??= new ProcessWorld(this);

        private ProcessWorld world;

        // The inspector's settings, which its world reads
        internal EProcessControlState InspectorStartState { get => StartState; set => StartState = value; }
        internal bool InspectorCascadeToChildren { get => CascadeToChildren; set => CascadeToChildren = value; }
        internal bool InspectorCascadeToSiblings { get => CascadeToSiblings; set => CascadeToSiblings = value; }
        internal int InspectorMaxTicksPerFrame { get => maxTicksPerFrame; set => maxTicksPerFrame = value; }

        public EProcessControlState State => World.State;

        /// <summary>
        /// High-level lifecycle callbacks for observers (debuggers, recorders, etc.).
        /// </summary>
        public ProcessControlCallbacks Callbacks => World.Callbacks;

        public int Created => World.Created;
        public int Active => World.Active;
        public int Running => World.Running;

        #region Process Watching

        public void WatchProcess(ProcessRelay toWatch, object caller, Action<ProcessDataPacket> onStartWatching = null,
            Action<EProcessState, EProcessState, ProcessDataPacket> onStateChange = null, Action<ProcessDataPacket> onUnregister = null) =>
            World.WatchProcess(toWatch, caller, onStartWatching, onStateChange, onUnregister);

        public void StopWatchingProcess(ProcessRelay toWatch) => World.StopWatchingProcess(toWatch);

        public void StopWatchingProcess(ProcessRelay toWatch, object caller) => World.StopWatchingProcess(toWatch, caller);

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

        public void Bootstrap() => Bootstrap(deferStartState: false);

        /// <summary>
        /// Makes this the ProcessControl and resets it into StartState, or into Ready when <paramref name="deferStartState"/>
        /// is set, so the framework's own processes can register first; <see cref="EnterStartState"/> then enters it.
        /// A StartState that refuses processes refused GameRoot too (PC-29).
        /// </summary>
        public void Bootstrap(bool deferStartState)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (DontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            World.ResetProcessControl(deferStartState ? EProcessControlState.Ready : StartState);
            ApplyTickRate();
        }

        /// <summary>
        /// Ticks a second of game time, each a fixed step that steps every process once, its end of frame with it, and
        /// lets each sequence's wait go on; a frame runs as many as its time holds, up to the inspector's most a frame.
        /// 0, the default, steps once a frame. Each process stepped once a frame, by the frame's time, so a run at
        /// another frame rate came out otherwise (FEAT-7, Decision 30).
        /// </summary>
        public int FixedTickRate
        {
            get => fixedTickRate;
            set
            {
                fixedTickRate = Mathf.Max(0, value);
                ApplyTickRate();
            }
        }

        // The ProcessControl's rate, in force: the tick's waits follow it
        private void ApplyTickRate()
        {
            World.ResetTickTime();
            if (Instance != this) return;
            if (fixedTickRate > 0) ForgeTick.Scene.Ticking = true;
            else if (ForgeTick.Scene.Ticking) ForgeTick.Scene.Stop();
        }

        /// <summary>Enters the StartState set in the inspector, after a Bootstrap that deferred it (PC-29).</summary>
        public void EnterStartState() => World.EnterStartState();

        public void DeferredInit()
        {
            if (!CollectOnAwake) return;
            CollectSceneProcesses();
        }

        // Start, not Awake: a Bootstrapper bootstraps the ProcessControl it makes after that one's Awake (PC-23)
        private void Start()
        {
            if (Instance != this)
            {
                ForgeLog.Warning($"{name} was never bootstrapped, so it steps nothing. A Bootstrapper makes the " +
                                 "ProcessControl it uses, so this one can go.", this);
            }
        }

        private void Update()
        {
            World.SweepDestroyed();
            if (fixedTickRate > 0 && Instance == this) World.RunTicks();
            else World.StepProcesses(EProcessStepTiming.Update);
        }

        private void LateUpdate()
        {
            // Each tick stepped its own end of frame
            if (fixedTickRate <= 0 || Instance != this) World.StepProcesses(EProcessStepTiming.LateUpdate);

            // The frame's costliest processes go into the Profiler's capture while it records (FEAT-18)
            if (Instance == this) ForgeFrameCosts.EndFrame();
        }

        private void FixedUpdate()
        {
            World.StepProcesses(EProcessStepTiming.FixedUpdate);
        }

        // By this name the golden tests step a ProcessControl of their own
        private void StepProcesses(EProcessStepTiming timing) => World.StepProcesses(timing);

        private void OnDestroy()
        {
            World.TerminateAllImmediately();
            // What waits for a tick goes on, as a frame's wait, once the ticks are gone
            if (Instance == this && ForgeTick.Scene.Ticking) ForgeTick.Scene.Stop();
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
        
                // Not SceneRoot's packet, which reparents what it's handed: a scene's processes stay where the
                // scene put them (PC-20)
                Register(process).With(ProcessDataPacket.Default()).In(this).Run();
            }
    
            if (OutputLogs) Debug.Log($"[ProcessControl] {World.HierarchyDebugInfo()}");
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

        #region Registering (FEAT-15)

        /// <summary>
        /// Starts a MonoBehaviour process's registration, which <see cref="ProcessRegistration.Run()"/> makes:
        /// <c>ProcessControl.Register(process).HandledBy(gas).With(data).Run()</c>. What isn't given takes its default,
        /// and a refused registration says why (FEAT-15).
        /// </summary>
        public static ProcessRegistration Register(AbstractMonoProcess process) => new(process);

        /// <summary>Starts a runtime process's registration. See <see cref="Register(AbstractMonoProcess)"/>.</summary>
        public static ProcessRegistration Register(AbstractRuntimeProcess process) => new(process);

        /// <summary>Starts a task sequence's registration, whose packet is a sequence's. See <see cref="Register(AbstractMonoProcess)"/>.</summary>
        public static ProcessRegistration Register(TaskSequence sequence) => new(sequence);

        /// <summary>Starts a chain of task sequences' registration. See <see cref="Register(AbstractMonoProcess)"/>.</summary>
        public static ProcessRegistration Register(TaskSequenceChain chain) => new(chain);

        #endregion

        #region Mono Process

        // The overloads' one message: each is a wrapper of the builder, and they go before the first Asset Store
        // submission (FEAT-15, Decision 30)
        private const string UseTheBuilder = "Use ProcessControl.Register(process), then HandledBy, With and Run. " +
                                             "These overloads go before the first Asset Store submission (FEAT-15).";

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractMonoProcess process, out ProcessRelay relay) => Register(process).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractMonoProcess process, AbilityDataPacket data, out ProcessRelay relay) =>
            Register(process).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractMonoProcess process, ProcessDataPacket data, out ProcessRelay relay) =>
            Register(process).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractMonoProcess process, IGameplayProcessHandler handler, out ProcessRelay relay) =>
            Register(process).HandledBy(handler).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractMonoProcess process, IGameplayProcessHandler handler, ProcessDataPacket data, out ProcessRelay relay) =>
            Register(process).HandledBy(handler).With(data).Run(out relay);

        // A MonoBehaviour process's registration, in this ProcessControl's world
        internal ProcessRegistrationResult RegisterMono(AbstractMonoProcess process, IGameplayProcessHandler handler, ProcessDataPacket data) =>
            World.RegisterMono(process, handler, data);

        #endregion

        #region Runtime Process

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractRuntimeProcess process, out ProcessRelay relay) => Register(process).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractRuntimeProcess process, AbilityDataPacket data, out ProcessRelay relay) =>
            Register(process).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractRuntimeProcess process, IGameplayProcessHandler handler, out ProcessRelay relay) =>
            Register(process).HandledBy(handler).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractRuntimeProcess process, ProcessDataPacket data, out ProcessRelay relay) =>
            Register(process).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractRuntimeProcess process, IGameplayProcessHandler handler, ProcessDataPacket data, out ProcessRelay relay) =>
            Register(process).HandledBy(handler).With(data).Run(out relay);

        // A runtime process's registration, a task sequence's among them, in this ProcessControl's world
        internal ProcessRegistrationResult RegisterRuntime(AbstractRuntimeProcess process, IGameplayProcessHandler handler, ProcessDataPacket data) =>
            World.RegisterRuntime(process, handler, data);

        #endregion

        #region Task Sequences (Async)

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequence sequence, out ProcessRelay relay) => Register(sequence).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequence sequence, AbilityDataPacket data, out ProcessRelay relay) =>
            Register(sequence).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequence sequence, IGameplayProcessHandler handler, out ProcessRelay relay) =>
            Register(sequence).HandledBy(handler).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequence sequence, SequenceDataPacket data, out ProcessRelay relay) =>
            Register(sequence).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequence sequence, IGameplayProcessHandler handler, SequenceDataPacket data, out ProcessRelay relay) =>
            Register(sequence).HandledBy(handler).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequenceChain chain, out ProcessRelay relay) => Register(chain).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequenceChain chain, AbilityDataPacket data, out ProcessRelay relay) =>
            Register(chain).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequenceChain chain, IGameplayProcessHandler handler, out ProcessRelay relay) =>
            Register(chain).HandledBy(handler).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequenceChain chain, SequenceDataPacket data, out ProcessRelay relay) =>
            Register(chain).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequenceChain chain, IGameplayProcessHandler handler, SequenceDataPacket data, out ProcessRelay relay) =>
            Register(chain).HandledBy(handler).With(data).Run(out relay);

        #endregion

        #region Group Processes

        /// <summary>
        /// Registers a pre-configured GroupProcess under the given tag.
        /// Use this when you need to set MaxMembers, OverflowPolicy, callbacks, etc.
        /// before any members are added.
        /// </summary>
        public static bool RegisterGroup(Tag groupTag, GroupProcess group, out ProcessRelay relay) =>
            Instance.World.RegisterGroup(groupTag, group, ProcessDataPacket.Default(), out relay);

        public static bool RegisterGroup(Tag groupTag, GroupProcess group, ProcessDataPacket data, out ProcessRelay relay) =>
            Instance.World.RegisterGroup(groupTag, group, data, out relay);

        /// <summary>
        /// Registers a runtime process as a member of the group identified by groupTag.
        /// If the group doesn't exist yet, it is auto-created with default settings.
        /// The member uses the group's shared data packet by default.
        /// </summary>
        public static bool RegisterWithGroup(Tag groupTag, AbstractRuntimeProcess process, out ProcessRelay relay, bool useSharedData = true) =>
            Instance.World.RegisterWithGroup(groupTag, process, null, useSharedData, out relay);

        /// <summary>
        /// Registers a runtime process as a member with an explicit standalone data packet.
        /// </summary>
        public static bool RegisterWithGroup(Tag groupTag, AbstractRuntimeProcess process, ProcessDataPacket data, out ProcessRelay relay) =>
            Instance.World.RegisterWithGroup(groupTag, process, data, false, out relay);

        /// <summary>
        /// Tries to get the GroupProcess associated with a tag.
        /// </summary>
        public static bool TryGetGroup(Tag groupTag, out GroupProcess group) => Instance.World.TryGetGroup(groupTag, out group);

        /// <summary>
        /// Checks whether a group with the given tag exists and is active.
        /// </summary>
        public static bool HasGroup(Tag groupTag) => Instance.World.HasGroup(groupTag);

        #endregion

        public bool Unregister(ProcessControlBlock pcb) => World.Unregister(pcb);

        public Dictionary<int, ProcessControlBlock> FetchActiveProcesses() => World.FetchActiveProcesses();

        /// <summary>True while ProcessControl itself is paused.</summary>
        public bool IsPaused => World.IsPaused;

        /// <summary>True while ProcessControl takes new processes, which it stops doing once closed or terminated.</summary>
        public bool AcceptsNewProcesses => World.AcceptsNewProcesses;

        #endregion

        #region Process State Control

        public void SetControlState(EProcessControlState state) => World.SetControlState(state);

        /// <summary>
        /// Runs the process. If cascading is enabled, also runs children.
        /// </summary>
        public bool Run(int cacheIndex, bool cascade = false) => World.Run(cacheIndex, cascade);

        /// <summary>
        /// Pauses/waits the process. If cascading is enabled, also pauses children. It takes effect at once.
        /// </summary>
        public bool Wait(int cacheIndex, bool cascade = true) => World.Wait(cacheIndex, cascade);

        /// <summary>
        /// Alias for Wait - pauses the process.
        /// </summary>
        public bool Pause(int cacheIndex, bool cascade = true) => World.Pause(cacheIndex, cascade);

        /// <summary>
        /// Alias for Run - unpauses/resumes the process.
        /// </summary>
        public bool Unpause(int cacheIndex, bool cascade = true) => World.Unpause(cacheIndex, cascade);

        /// <summary>
        /// Terminates the process. Always cascades to children.
        /// </summary>
        public bool Terminate(int cacheIndex, bool cascade = true) => World.Terminate(cacheIndex, cascade);

        public bool TerminateImmediate(int cacheIndex, bool cascade = true) => World.TerminateImmediate(cacheIndex, cascade);

        /// <summary>
        /// Ends the process of an object that has been destroyed, which nothing else tells ProcessControl about.
        /// </summary>
        public void EndDestroyedProcess(ProcessRelay relay) => World.EndDestroyedProcess(relay);

        public void TerminateAll() => World.TerminateAll();

        public void TerminateAllImmediately() => World.TerminateAllImmediately();

        #endregion

        #region Internal Process Communication

        public void ProcessWillChangeState(ProcessControlBlock pcb, EProcessState state) => World.ProcessWillChangeState(pcb, state);

        public EProcessState GetDefaultTransitionState(ProcessControlBlock pcb) => World.GetDefaultTransitionState(pcb);

        // By this name the harness's oracle reads the transition table
        private static readonly Dictionary<(EProcessControlState control, EProcessLifecycle lifecycle, EProcessState from), EProcessState>
            DefaultTransitions = ProcessWorld.DefaultTransitions;

        #endregion

        #region Public API

        /// <summary>
        /// A Mono process's transform has a new parent, so the hierarchy files it again under its new ancestors. It
        /// heard only where the process registered (PC-28, Decision 11).
        /// </summary>
        public void ProcessReparented(ProcessRelay relay) => World.ProcessReparented(relay);

        /// <summary>
        /// Gets all child process IDs for a given process
        /// </summary>
        public List<int> GetChildProcessIds(int cacheIndex) => World.GetChildProcessIds(cacheIndex);

        /// <summary>
        /// Gets all sibling process IDs (same GameObject) for a given process
        /// </summary>
        public List<int> GetSiblingProcessIds(int cacheIndex) => World.GetSiblingProcessIds(cacheIndex);

        /// <summary>
        /// Checks if a process exists
        /// </summary>
        public bool IsRegistered(int cacheIndex) => World.IsRegistered(cacheIndex);

        /// <summary>
        /// Checks if a process is currently running (not waiting)
        /// </summary>
        public bool IsRunning(int cacheIndex) => World.IsRunning(cacheIndex);

        /// <summary>
        /// Checks if a process is currently waiting/paused
        /// </summary>
        public bool IsWaiting(int cacheIndex) => World.IsWaiting(cacheIndex);

        public bool StateIs(int cacheIndex, EProcessState state) => World.StateIs(cacheIndex, state);

        public bool TryGetQueuedState(int cacheIndex, out EProcessState queuedState) => World.TryGetQueuedState(cacheIndex, out queuedState);

        /// <summary>
        /// Gets the ProcessControlBlock for direct access (use cautiously)
        /// </summary>
        public bool TryGetPCB(int cacheIndex, out ProcessControlBlock pcb) => World.TryGetPCB(cacheIndex, out pcb);

        // ───────────────────────────────────────────────────────────────────────
        // GAS-SPECIFIC REGISTRATION HELPERS
        //
        // These convenience overloads make the owning GAS explicit at the call
        // site. Any process registered with an IGameplayAbilitySystem handler
        // (or whose data packet resolves to one) is auto-tracked in _gasProcesses.
        // ───────────────────────────────────────────────────────────────────────

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractRuntimeProcess process, IGameplayAbilitySystem gas, out ProcessRelay relay) =>
            Register(process).HandledBy(gas).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractRuntimeProcess process, IGameplayAbilitySystem gas, ProcessDataPacket data, out ProcessRelay relay) =>
            Register(process).HandledBy(gas).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractMonoProcess process, IGameplayAbilitySystem gas, out ProcessRelay relay) =>
            Register(process).HandledBy(gas).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(AbstractMonoProcess process, IGameplayAbilitySystem gas, ProcessDataPacket data, out ProcessRelay relay) =>
            Register(process).HandledBy(gas).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequence sequence, IGameplayAbilitySystem gas, out ProcessRelay relay) =>
            Register(sequence).HandledBy(gas).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequence sequence, IGameplayAbilitySystem gas, SequenceDataPacket data, out ProcessRelay relay) =>
            Register(sequence).HandledBy(gas).With(data).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequenceChain chain, IGameplayAbilitySystem gas, out ProcessRelay relay) =>
            Register(chain).HandledBy(gas).Run(out relay);

        [Obsolete(UseTheBuilder)]
        public static bool Register(TaskSequenceChain chain, IGameplayAbilitySystem gas, SequenceDataPacket data, out ProcessRelay relay) =>
            Register(chain).HandledBy(gas).With(data).Run(out relay);

        /// <summary>
        /// Returns the cache indices of all active processes owned by the given GAS.
        /// </summary>
        public static IReadOnlyCollection<int> GetProcessIdsForGAS(IGameplayAbilitySystem gas) =>
            gas == null || Instance == null ? Array.Empty<int>() : Instance.World.ProcessIdsForGAS(gas);

        /// <summary>
        /// Returns ProcessRelays for all active processes owned by the given GAS.
        /// </summary>
        public static IEnumerable<ProcessRelay> GetProcessesForGAS(IGameplayAbilitySystem gas) =>
            gas == null || Instance == null ? Enumerable.Empty<ProcessRelay>() : Instance.World.ProcessesForGAS(gas);

        /// <summary>
        /// Number of active processes owned by the given GAS.
        /// </summary>
        public static int GetProcessCountForGAS(IGameplayAbilitySystem gas) =>
            gas == null || Instance == null ? 0 : Instance.World.ProcessCountForGAS(gas);

        /// <summary>
        /// Terminates every process owned by the given GAS. When <paramref name="immediate"/>
        /// is true, uses TerminateImmediate; otherwise queues graceful termination.
        /// Returns the number of processes that received a termination request.
        /// </summary>
        public static int TerminateAllForGAS(IGameplayAbilitySystem gas, bool immediate = false) =>
            gas == null || Instance == null ? 0 : Instance.World.TerminateAllForGAS(gas, immediate);

        #endregion

        #region Handler Interface

        public ProcessRelay[] GetRelays() => World.GetRelays();

        // By reference: a cast threw for any handler but a ProcessControl
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler) => ReferenceEquals(handler, this);

        public bool HandlerProcessIsSubscribed(ProcessRelay relay) => World.IsRegistered(relay.CacheIndex);

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
