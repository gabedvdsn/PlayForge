using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The scene's scheduler of processes: every process, a GAS, a projectile, an ability's sequence, registers with
    /// it, and it steps each from Unity's player loop, in Update, LateUpdate or FixedUpdate as the process's timing
    /// asks, moves it between its states as its lifecycle says, and ends it. A Bootstrapper brings it up as the one
    /// <c>Instance</c>, and it then registers the scene's own mono processes, as its Collect On Awake says.
    ///
    /// <para><c>ProcessControl.Register(process).HandledBy(handler).With(data).Run()</c> registers a process and starts
    /// it, and its result holds the process's relay, which pauses, resumes and ends it; <c>RegisterGroup</c> and
    /// <c>RegisterWithGroup</c> hold runtime processes in a group by a tag. Its control state (<c>SetControlState</c>)
    /// pauses, closes or ends every process at once; its Fixed Tick Rate, above 0, steps every process at fixed steps
    /// of game time, so a run comes out the same at any frame rate; and its <c>Callbacks</c> hear every process
    /// register, change state and go. Its bookkeeping is its <c>World</c>, a <c>ProcessWorld</c>, which code can also
    /// own and step with no scene.</para>
    /// </summary>
    public class ProcessControl : MonoBehaviour, IGameplayProcessHandler, IManagerial
    {
        public static ProcessControl Instance;

        // Statics outlive play mode when domain reload is off, so the last session's destroyed ProcessControl was
        // still Instance when the next began, and the defaults went on with it
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearInstance()
        {
            Instance = null;
            defaultHandler = null;
            defaultParent = null;
        }

        private static IGameplayProcessHandler defaultHandler;
        private static Transform defaultParent;

        /// <summary>
        /// The handler a registration that names none hands its process to, outside a world of its own, where its packet
        /// has no <see cref="ProcessDataPacket.Source"/>: the GameRoot, where there is one, or none.
        /// </summary>
        public static IGameplayProcessHandler DefaultHandler
        {
            // Unity's null, so a destroyed handler is none
            get => defaultHandler is UnityEngine.Object made && !made ? null : defaultHandler;
            set => defaultHandler = value;
        }

        /// <summary>
        /// What a scene root's packet parents its process under (<see cref="ProcessDataPacket.SceneRoot"/>): the GameRoot,
        /// where there is one, or nothing.
        /// </summary>
        public static Transform DefaultParent
        {
            get => defaultParent ? defaultParent : null;
            set => defaultParent = value;
        }

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
                 "comes out the same at any frame rate. 0 steps once a frame")]
        [SerializeField] private int fixedTickRate;
        [Tooltip("The most ticks a frame runs, so a long frame doesn't fall further behind: the rest of its time is " +
                 "dropped")]
        [SerializeField] private int maxTicksPerFrame = 8;

        [Space]
        public bool OutputLogs;
        public bool DetailedLogs = true;

        public AbstractMonoProcessInstantiator CustomInstantiator;
        
        /// <summary>
        /// The world of processes this ProcessControl steps from Unity's loop. Its bookkeeping was this class's own,
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
        /// A StartState that refuses processes refused GameRoot too.
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
        /// Brings a ProcessControl up where none is, the prefab's copy or one it makes, and bootstraps it; null where one is
        /// up already. With <paramref name="deferStartState"/> set it waits in Ready for <see cref="EnterStartState"/>.
        /// </summary>
        public static ProcessControl BringUp(ProcessControl prefab = null, bool deferStartState = false)
        {
            // Unity's null, so a destroyed one is replaced; `is null` kept it
            if (Instance) return null;

            var control = prefab
                ? Instantiate(prefab, Vector3.zero, Quaternion.identity)
                : new GameObject().AddComponent<ProcessControl>();
            control.name = "ProcessControl";
            control.Bootstrap(deferStartState);
            return control;
        }

        /// <summary>
        /// Ticks a second of game time, each a fixed step that steps every process once, its end of frame with it, and
        /// lets each sequence's wait go on; a frame runs as many as its time holds, up to the inspector's most a frame.
        /// 0, the default, steps once a frame. Each process stepped once a frame, by the frame's time, so a run at
        /// another frame rate came out otherwise.
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

        /// <summary>Enters the StartState set in the inspector, after a Bootstrap that deferred it.</summary>
        public void EnterStartState() => World.EnterStartState();

        public void DeferredInit()
        {
            if (!CollectOnAwake) return;
            CollectSceneProcesses();
        }

        // Start, not Awake: a Bootstrapper bootstraps the ProcessControl it makes after that one's Awake
        private void Start()
        {
            if (Instance != this)
            {
                ForgeLog.Warning(ForgeLog.ESystem.Processes, $"{name} was never bootstrapped, so it steps nothing. A Bootstrapper makes the " +
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

            // The frame's costliest processes go into the Profiler's capture while it records
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
                var found = new System.Text.StringBuilder($"[ProcessControl] Found {processes.Length} processes in scene");
                if (DetailedLogs)
                {
                    for (int i = 0; i < processes.Length; i++) found.Append($"\n\t[{i + 1}] {processes[i]}");
                }
                ForgeLog.Info(ForgeLog.ESystem.Processes, found.ToString(), this);
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
                // scene put them
                Register(process).With(ProcessDataPacket.Default()).In(this).Run();
            }
    
            if (OutputLogs) ForgeLog.Info(ForgeLog.ESystem.Processes, $"[ProcessControl] {World.HierarchyDebugInfo()}", this);
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

        #region Registering

        /// <summary>
        /// Starts a MonoBehaviour process's registration, which <see cref="ProcessRegistration.Run()"/> makes:
        /// <c>ProcessControl.Register(process).HandledBy(gas).With(data).Run()</c>. What isn't given takes its default,
        /// and a refused registration says why.
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

        // A MonoBehaviour process's registration, in this ProcessControl's world
        internal ProcessRegistrationResult RegisterMono(AbstractMonoProcess process, IGameplayProcessHandler handler, ProcessDataPacket data) =>
            World.RegisterMono(process, handler, data);

        #endregion

        #region Runtime Process

        // A runtime process's registration, a task sequence's among them, in this ProcessControl's world
        internal ProcessRegistrationResult RegisterRuntime(AbstractRuntimeProcess process, IGameplayProcessHandler handler, ProcessDataPacket data) =>
            World.RegisterRuntime(process, handler, data);

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
        /// heard only where the process registered.
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
        // OWNERS' QUERIES
        //
        // A process registered with an IProcessOwner as its handler, or with a
        // packet whose Source is one, is counted under that owner.
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>The cache indices of every active process the owner owns.</summary>
        public static IReadOnlyCollection<int> GetProcessIdsFor(IProcessOwner owner) =>
            owner == null || Instance == null ? Array.Empty<int>() : Instance.World.ProcessIdsFor(owner);

        /// <summary>The relays of every active process the owner owns.</summary>
        public static IEnumerable<ProcessRelay> GetProcessesFor(IProcessOwner owner) =>
            owner == null || Instance == null ? Enumerable.Empty<ProcessRelay>() : Instance.World.ProcessesFor(owner);

        /// <summary>How many active processes the owner owns.</summary>
        public static int GetProcessCountFor(IProcessOwner owner) =>
            owner == null || Instance == null ? 0 : Instance.World.ProcessCountFor(owner);

        /// <summary>
        /// Terminates every process the owner owns, at once when <paramref name="immediate"/> is set, or else queued to end
        /// gracefully. Returns how many were told to.
        /// </summary>
        public static int TerminateAllFor(IProcessOwner owner, bool immediate = false) =>
            owner == null || Instance == null ? 0 : Instance.World.TerminateAllFor(owner, immediate);

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

    /// <summary>
    /// What ProcessControl does as a whole: <c>Ready</c> takes new processes and runs them; <c>Waiting</c> takes them
    /// and holds every process where it is; <c>Closed</c> takes no more and runs those it has; <c>ClosedWaiting</c>
    /// takes no more and holds those it has; <c>Terminated</c> ends every process, a running one as its run ends, and
    /// <c>TerminatedImmediately</c> ends them all at once, and neither takes more. <c>SetControlState</c> sets it, and
    /// leaving a waiting state sends back what it held; the inspector's Start State is the one it starts in.
    /// </summary>
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
