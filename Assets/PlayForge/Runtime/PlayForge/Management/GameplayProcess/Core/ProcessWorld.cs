using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The processes of one world and how they're stepped (FEAT-10, Decision 30): every process's block, its step groups,
    /// the watches, the groups, which processes each GAS owns, the control state and its transitions, and the callbacks.
    /// A scene's ProcessControl wraps one and steps it from Unity's loop, and code can own one and step it itself, as a
    /// headless run does. It was ProcessControl itself, so no process ran without a ProcessControl in a scene.
    /// </summary>
    public sealed class ProcessWorld
    {
        /// <summary>The ProcessControl that steps this world in a scene, or null for a world its owner steps.</summary>
        public ProcessControl Control { get; }

        /// <summary>A world of its own, ready to take processes, which its owner steps with <see cref="Step"/>.</summary>
        public ProcessWorld()
        {
            ResetProcessControl(StartState);
        }

        // A scene's world, which its ProcessControl readies as it bootstraps
        internal ProcessWorld(ProcessControl control) => Control = control;

        /// <summary>
        /// The world that holds a process, by its relay: an entity's, a runtime process's or a Mono process's. One that no
        /// world holds takes the scene's. An ability's processes run in their caster's world; they ran in the scene's,
        /// wherever the caster was (FEAT-10).
        /// </summary>
        public static ProcessWorld Of(object process)
        {
            var relay = process switch
            {
                AbstractRuntimeProcess runtime => runtime.ProcessRelay,
                AbstractMonoProcess mono => mono.ProcessRelay,
                _ => null,
            };
            var world = relay?.World;
            if (world != null) return world;
            var scene = ProcessControl.Instance;
            return scene != null ? scene.World : null;
        }

        // The settings. A scene's world reads its ProcessControl's inspector, so a change there holds at once; a world of
        // its own keeps its own
        private EProcessControlState startState = EProcessControlState.Ready;
        private bool cascadeToChildren = true, cascadeToSiblings = true, outputLogs, detailedLogs = true;
        private int fixedTickRate, maxTicksPerFrame = 8;

        /// <summary>The state the world takes as it's readied.</summary>
        public EProcessControlState StartState
        {
            get => Control is null ? startState : Control.InspectorStartState;
            set
            {
                if (Control is null) startState = value;
                else Control.InspectorStartState = value;
            }
        }

        /// <summary>Whether pausing or ending a process does the same to its children's.</summary>
        public bool CascadeToChildren
        {
            get => Control is null ? cascadeToChildren : Control.InspectorCascadeToChildren;
            set
            {
                if (Control is null) cascadeToChildren = value;
                else Control.InspectorCascadeToChildren = value;
            }
        }

        /// <summary>Whether the processes on one object are treated as a group.</summary>
        public bool CascadeToSiblings
        {
            get => Control is null ? cascadeToSiblings : Control.InspectorCascadeToSiblings;
            set
            {
                if (Control is null) cascadeToSiblings = value;
                else Control.InspectorCascadeToSiblings = value;
            }
        }

        public bool OutputLogs
        {
            get => Control is null ? outputLogs : Control.OutputLogs;
            set
            {
                if (Control is null) outputLogs = value;
                else Control.OutputLogs = value;
            }
        }

        public bool DetailedLogs
        {
            get => Control is null ? detailedLogs : Control.DetailedLogs;
            set
            {
                if (Control is null) detailedLogs = value;
                else Control.DetailedLogs = value;
            }
        }

        /// <summary>Ticks a second of game time, or 0 to step once a frame (FEAT-7).</summary>
        public int FixedTickRate
        {
            get => Control is null ? fixedTickRate : Control.FixedTickRate;
            set
            {
                if (Control is not null)
                {
                    Control.FixedTickRate = value;
                    return;
                }

                fixedTickRate = Mathf.Max(0, value);
                ResetTickTime();
            }
        }

        /// <summary>The most ticks a frame runs; the rest of its time is dropped.</summary>
        public int MaxTicksPerFrame
        {
            get => Control is null ? maxTicksPerFrame : Control.InspectorMaxTicksPerFrame;
            set
            {
                if (Control is null) maxTicksPerFrame = value;
                else Control.InspectorMaxTicksPerFrame = value;
            }
        }

        public EProcessControlState State { get; private set; }

        // Core process storage
        private Dictionary<int, ProcessControlBlock> _active = new();
        private HashSet<int> _waiting = new();
        
        // Optimized stepping - flat lists per timing type for cache-friendly iteration
        // Empty until Bootstrap fills it, so a ProcessControl nothing bootstrapped steps nothing. Null, it threw in
        // every Update, LateUpdate and FixedUpdate (PC-23)
        private Dictionary<EProcessStepTiming, StepGroup> _stepping = new();

        // The timing each block joined its step groups by, which is the one it leaves them by. A process's own can
        // change while it runs, and leaving by that left the block stepping where it had joined (PC-26)
        private Dictionary<int, EProcessStepTiming> _steppedAs = new();

        // Blocks whose object wasn't active when they registered. Unity sends no OnDestroy to an object that was never
        // active, so one destroyed before it woke left its block stepping with nothing behind it (PC-30)
        private readonly List<int> _dormant = new();

        // Blocks whose process's class declares its own OnDestroy, which hides the base's, so Unity never says when
        // their object goes. Watched for as long as they run (PC-31)
        private readonly List<int> _unheard = new();

        // Per class, whether it hides AbstractMonoProcess's OnDestroy, so each is looked up, and warned of, once (PC-31)
        private readonly Dictionary<Type, bool> _hidesOnDestroy = new();
        
        // Hierarchy management for MonoProcesses
        private ProcessHierarchy _hierarchy = new();

        // The processes this world has taken
        private int _created;

        // The cache indices, which every world draws from, so a process's is its own among all worlds'. Each world
        // counted its own from 0, so a handler with processes in two worlds, as GameRoot is by default, refused the
        // second under the first's index (FEAT-10)
        private static int s_cacheCounter;

        private int NextCacheIndex
        {
            get
            {
                _created++;
                return System.Threading.Interlocked.Increment(ref s_cacheCounter) - 1;
            }
        }

        private Dictionary<int, ProcessWatcher> _watchers = new();

        // Group process registry — maps user-facing Tag to the GroupProcess instance
        private Dictionary<Tag, GroupProcess> _groups = new();

        // GAS-owned process registry — maps the registering IGameplayAbilitySystem to
        // the set of process cache indices it owns. A process is associated with a GAS
        // when its handler is an IGameplayAbilitySystem (either directly passed as the
        // handler or derived from an AbilityDataPacket's EffectOrigin.GetOwner()).
        private Dictionary<IGameplayAbilitySystem, HashSet<int>> _gasProcesses = new();

        /// <summary>
        /// High-level lifecycle callbacks for observers (debuggers, recorders, etc.).
        /// </summary>
        public ProcessControlCallbacks Callbacks { get; private set; } = new();

        /// <summary>The processes the world has taken.</summary>
        public int Created => _created;
        public int Active => _active.Count;
        public int Running => _active.Count - _waiting.Count;

        
        /// <summary>
        /// The processes stepped at one timing, in step order: by priority, lowest first, and within a
        /// priority in the order they arrived. Stepping reads this every frame, so it's kept in order as
        /// processes come and go. The priority used to be stored and never applied (PC-22).
        ///
        /// A process that goes leaves a <see cref="Hole"/>, found at once by its position, which stepping
        /// passes over as over any index no longer active. Its removal searched the list from the front and
        /// shifted what came after, so ending the newest of 10,000 cost several times what ending the newest of
        /// 1,000 did, and ending every one of them cost the square of how many there were (PC-34). The holes
        /// are swept out, in place, once they outnumber the processes.
        /// </summary>
        private class StepGroup
        {
            public const int Hole = -1;

            private readonly List<int> _indices = new();
            private readonly List<int> _priorities = new();
            private readonly Dictionary<int, int> _positions = new();
            private int _holes;

            public void Add(int cacheIndex, int priority)
            {
                int position = _priorities.Count;
                while (position > 0 && _priorities[position - 1] > priority) position--;

                _indices.Insert(position, cacheIndex);
                _priorities.Insert(position, priority);

                // It and whatever the insert moved along, where they are now: only it, unless it came before others
                for (int i = position; i < _indices.Count; i++)
                {
                    if (_indices[i] != Hole) _positions[_indices[i]] = i;
                }
            }

            public bool Remove(int cacheIndex)
            {
                if (!_positions.Remove(cacheIndex, out int position)) return false;

                _indices[position] = Hole;
                _holes++;
                if (_holes > 16 && _holes * 2 > _indices.Count) Sweep();
                return true;
            }

            // The holes out, in place, the order kept
            private void Sweep()
            {
                int kept = 0;
                for (int i = 0; i < _indices.Count; i++)
                {
                    if (_indices[i] == Hole) continue;

                    _indices[kept] = _indices[i];
                    _priorities[kept] = _priorities[i];
                    _positions[_indices[kept]] = kept;
                    kept++;
                }
                _indices.RemoveRange(kept, _indices.Count - kept);
                _priorities.RemoveRange(kept, _priorities.Count - kept);
                _holes = 0;
            }

            /// <summary>The step order, holes among it: stepping passes over a hole as over a process no longer active.</summary>
            public List<int> GetIndices() => _indices;

            public int Count => _indices.Count - _holes;
        }


        
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
                // Written back, as WatcherActions is a struct: changing the copy read out changes nothing
                actions.TryGetValue(caller, out var _actions);
                actions[caller] = new WatcherActions()
                {
                    onStateChange = _actions.onStateChange + onStateChange,
                    onUnregister = _actions.onUnregister + onUnregister
                };
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
                // A snapshot, as a callback can stop watching, which changes what's being walked
                foreach (var _actions in actions.Values.ToArray()) _actions.onStateChange?.Invoke(oldState, newState, data);
            }
            
            public void RunOnUnregister(ProcessDataPacket data)
            {
                foreach (var _actions in actions.Values.ToArray()) _actions.onUnregister?.Invoke(data);
            }
        }

        public void WatchProcess(ProcessRelay toWatch, object caller, Action<ProcessDataPacket> onStartWatching = null, Action<EProcessState, EProcessState, ProcessDataPacket> onStateChange = null,
            Action<ProcessDataPacket> onUnregister = null)
        {
            // In this control's watchers: it went in Instance's, whichever control held the process (FEAT-10)
            if (!_watchers.TryGetValue(toWatch.CacheIndex, out var watcher))
            {
                _watchers[toWatch.CacheIndex] = new ProcessWatcher(toWatch, caller, onStateChange, onUnregister);
            }
            else
            {
                watcher.AddActions(caller, onStateChange, onUnregister);
            }

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

            // A callback's throw can't be let past here: the unregister still has to finish
            try
            {
                _watchers[pcb.CacheIndex].RunOnUnregister(pcb.Wrapper.Data);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            StopWatchingProcess(pcb.Relay);
        }
        
        
        // The frame's time a tick hasn't stepped yet
        private double tickAccumulator;

        // A tick's clock: the tick's fixed step, and the tick's number as its frame
        private sealed class TickClock : IForgeClock
        {
            public IForgeClock Frame;
            public float Step;
            public float DeltaTime => Step;
            public float UnscaledDeltaTime => Step;
            public float FixedDeltaTime => Frame.FixedDeltaTime;
            public float Time => Frame.Time;
            public float UnscaledTime => Frame.UnscaledTime;
            public int FrameCount => ForgeTick.Ticks;
        }

        private readonly TickClock tickClock = new();

        // As many ticks as the frame's time holds: in each, what waits for a tick goes on, every process steps, and
        // then steps its end of frame, all by the tick's clock
        internal void RunTicks()
        {
            double step = 1.0 / FixedTickRate;
            tickAccumulator += ForgeTime.DeltaTime;
            int most = Mathf.Max(1, MaxTicksPerFrame);
            int ran = 0;
            while (tickAccumulator + 1e-6 >= step && ran < most)
            {
                tickAccumulator -= step;
                ran++;
                tickClock.Frame = ForgeTime.Clock;
                tickClock.Step = (float)step;
                var frame = ForgeTime.Use(tickClock);
                try
                {
                    Tick.Advance();
                    StepProcesses(EProcessStepTiming.Update);
                    StepProcesses(EProcessStepTiming.LateUpdate);
                }
                finally
                {
                    ForgeTime.Use(frame);
                }
            }

            // A frame too long for its ticks drops the rest, so the game doesn't fall further behind each frame
            if (ran >= most && tickAccumulator >= step) tickAccumulator = 0;
        }

        // A world of its own's tick and clock: it ticks once each time its owner steps it, so a sequence in it waits on its
        // steps, and its clock reads each step's time. A scene's world has the scene's tick and Unity's clock (FEAT-10)
        private readonly ForgeTickSource ownTick = new() { Ticking = true };
        private WorldClock ownClock;

        /// <summary>The world's tick: its own for a world of its own, the scene's for a scene's.</summary>
        public ForgeTickSource Tick => Control is null ? ownTick : ForgeTick.Scene;

        /// <summary>The game time a world of its own has been stepped through.</summary>
        public float Elapsed => ownClock?.Elapsed ?? 0f;

        // A world of its own's clock: the step's time, and the steps and their time so far
        private sealed class WorldClock : IForgeClock
        {
            public ForgeTickSource Tick;
            public float Step, Elapsed;
            public float DeltaTime => Step;
            public float UnscaledDeltaTime => Step;
            public float FixedDeltaTime => Step;
            public float Time => Elapsed;
            public float UnscaledTime => Elapsed;
            public int FrameCount => Tick.Ticks;
        }

        /// <summary>
        /// Steps every process the world holds once, as a frame does: Update, then LateUpdate. A world of its own steps by
        /// Unity's frame time; a scene's, by the clock read now, as its ProcessControl steps it from Unity's loop (FEAT-10).
        /// </summary>
        public void Step()
        {
            if (Control is null)
            {
                Step(ForgeTime.DeltaTime);
                return;
            }

            SweepDestroyed();
            StepProcesses(EProcessStepTiming.Update);
            StepProcesses(EProcessStepTiming.LateUpdate);
        }

        /// <summary>
        /// Steps a world of its own by a time its owner gives: its clock reads that time, its tick goes on, so what waits on
        /// it goes on, and every process steps once, Update and then LateUpdate. A loop of steps runs the world through its
        /// time within one frame, as a headless run does; its waits were Unity's next frame (FEAT-10). A scene's world
        /// steps with Unity's loop, so this steps it once by the clock read now.
        /// </summary>
        public void Step(float deltaTime)
        {
            if (Control is not null)
            {
                Step();
                return;
            }

            ownClock ??= new WorldClock { Tick = ownTick };
            ownClock.Step = deltaTime;
            ownClock.Elapsed += deltaTime;
            var clock = ForgeTime.Use(ownClock);
            var tick = ForgeTick.Enter(ownTick);
            try
            {
                ownTick.Advance();
                SweepDestroyed();
                StepProcesses(EProcessStepTiming.Update);
                StepProcesses(EProcessStepTiming.LateUpdate);
            }
            finally
            {
                ForgeTick.Enter(tick);
                ForgeTime.Use(clock);
            }
        }

        /// <summary>Steps the processes that step at the fixed rate, as a physics step does.</summary>
        public void StepFixed() => StepProcesses(EProcessStepTiming.FixedUpdate);

        /// <summary>Enters the start state, after readying that deferred it (PC-29).</summary>
        public void EnterStartState() => SetControlState(StartState);

        // Ends the blocks of processes whose objects went with no word: one never awake (PC-30), and one whose class hides
        // OnDestroy (PC-31)
        internal void SweepDestroyed()
        {
            Sweep(_dormant, untilAwake: true);
            Sweep(_unheard, untilAwake: false);
        }

        // A tick's time starts again as the rate changes
        internal void ResetTickTime() => tickAccumulator = 0;

        /// <summary>
        /// Ends the block of each watched process whose object has been destroyed. A dormant one stops being watched once
        /// it wakes, as its OnDestroy will come then (PC-30); one whose class hides OnDestroy never does (PC-31).
        /// </summary>
        private void Sweep(List<int> watched, bool untilAwake)
        {
            for (int i = watched.Count - 1; i >= 0; i--)
            {
                if (!_active.TryGetValue(watched[i], out var pcb) || !pcb.Wrapper.TryGetProcess<AbstractMonoProcess>(out var mono))
                {
                    watched.RemoveAt(i);
                }
                else if (!mono)
                {
                    watched.RemoveAt(i);
                    EndDestroyedProcess(pcb.Relay);
                }
                else if (untilAwake && mono.gameObject.activeInHierarchy)
                {
                    watched.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Whether the process's class, or one between it and AbstractMonoProcess, declares its own OnDestroy. Unity calls
        /// only that one, so AbstractMonoProcess's, which ends the process, never runs (PC-31).
        /// </summary>
        private bool HidesOnDestroy(AbstractMonoProcess mono)
        {
            var type = mono.GetType();
            if (_hidesOnDestroy.TryGetValue(type, out bool hides)) return hides;

            const System.Reflection.BindingFlags declared = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(AbstractMonoProcess) && !hides; t = t.BaseType)
            {
                hides = t.GetMethod("OnDestroy", declared, null, Type.EmptyTypes, null) != null;
            }

            _hidesOnDestroy[type] = hides;
            if (hides)
            {
                ForgeLog.Warning($"{type.Name} declares its own OnDestroy, which hides {nameof(AbstractMonoProcess)}'s, so " +
                                 "Unity never says when its object goes. ProcessControl watches its processes instead; " +
                                 $"override {nameof(AbstractMonoProcess.WhenDestroy)} to act on it.", mono);
            }

            return hides;
        }

        // Reusable snapshot buffer to avoid allocation during stepping.
        // A step callback (e.g. a Synchronous process self-terminating) can mutate the
        // live indices list, so we iterate a snapshot instead.
        private readonly List<int> _stepSnapshot = new();

        internal void StepProcesses(EProcessStepTiming timing)
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

            // Under its timing's marker in the Profiler, and timed for its counter (FEAT-18)
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            using (ForgeProfiler.StepLoop(timing).Auto())
            {
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
            ForgeCounters.AddStepTime(System.Diagnostics.Stopwatch.GetTimestamp() - started);

            // The scene's levels, as its LateUpdate loop ends (FEAT-18)
            if (timing == EProcessStepTiming.LateUpdate && Control is not null)
                ForgeCounters.SceneLevels(Running, _waiting.Count, _created);
        }
        
        // Empties the world into a state: what a ProcessControl's bootstrap does, and a world of its own as it's made
        internal void ResetProcessControl(EProcessControlState nextState)
        {
            TerminateAllImmediately();

            _active = new Dictionary<int, ProcessControlBlock>();
            _waiting = new HashSet<int>();
            _hierarchy.Clear();
            _groups = new Dictionary<Tag, GroupProcess>();
            _gasProcesses = new Dictionary<IGameplayAbilitySystem, HashSet<int>>();

            _steppedAs = new Dictionary<int, EProcessStepTiming>();
            _dormant.Clear();
            _unheard.Clear();

            // Initialize stepping groups for each timing type
            _stepping = new Dictionary<EProcessStepTiming, StepGroup>
            {
                [EProcessStepTiming.None] = new StepGroup(),
                [EProcessStepTiming.Update] = new StepGroup(),
                [EProcessStepTiming.LateUpdate] = new StepGroup(),
                [EProcessStepTiming.FixedUpdate] = new StepGroup()
            };

            var oldState = State;
            State = nextState;
            if (oldState != nextState) Callbacks?.ControlStateChanged(oldState, nextState);
        }

        /// <summary>
        /// Resolves the owning IGameplayAbilitySystem (if any) for a registration.
        /// Checks the handler first, then falls back to data.EffectOrigin.GetOwner().
        /// </summary>
        private static IGameplayAbilitySystem ResolveOwningGAS(IGameplayProcessHandler handler, ProcessDataPacket data)
        {
            if (handler is IGameplayAbilitySystem handlerGas) return handlerGas;
            if (data is AbilityDataPacket ability)
            {
                var owner = ability.EffectOrigin?.GetSource();
                if (owner is IGameplayAbilitySystem ownerGas) return ownerGas;
            }
            return null;
        }
        
        // A MonoBehaviour process's registration, as the builder makes it, and why it's refused when it is
        internal ProcessRegistrationResult RegisterMono(AbstractMonoProcess process, IGameplayProcessHandler handler, ProcessDataPacket data)
        {
            if (process == null) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NoProcess);
            if (process.IsInitialized) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.AlreadyRegistered);
            if (!CanAcceptNewProcesses()) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.ProcessControlClosed);

            var wrapper = new MonoWrapperProcess(process, data, handler);
            var pcb = ProcessControlBlock.Generate(NextCacheIndex, wrapper, this);
            pcb.isMono = true;

            var relay = pcb.Relay;
            handler?.HandlerSubscribeProcess(relay);
            data.HandlerSubscribeProcess(relay);

            return SetProcess(pcb)
                ? new ProcessRegistrationResult(relay, EProcessRegistrationRefusal.None)
                : new ProcessRegistrationResult(null, EProcessRegistrationRefusal.InitializeThrew);
        }

        // A runtime process's registration, a task sequence's among them, as the builder makes it, and why it's refused
        // when it is
        internal ProcessRegistrationResult RegisterRuntime(AbstractRuntimeProcess process, IGameplayProcessHandler handler, ProcessDataPacket data)
        {
            if (process == null) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NoProcess);
            if (process.IsInitialized) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.AlreadyRegistered);
            if (!CanAcceptNewProcesses()) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.ProcessControlClosed);

            var wrapper = new RuntimeWrapperProcess(process, data, handler);
            var pcb = ProcessControlBlock.Generate(NextCacheIndex, wrapper, this);

            var relay = pcb.Relay;
            handler?.HandlerSubscribeProcess(relay);
            data.HandlerSubscribeProcess(relay);

            return SetProcess(pcb)
                ? new ProcessRegistrationResult(relay, EProcessRegistrationRefusal.None)
                : new ProcessRegistrationResult(null, EProcessRegistrationRefusal.InitializeThrew);
        }

        internal bool RegisterGroup(Tag groupTag, GroupProcess group, ProcessDataPacket data, out ProcessRelay relay)
        {
            relay = default;
            if (group == null || _groups.ContainsKey(groupTag)) return false;

            if (!ProcessControl.Register(group).With(data).In(this).Run(out relay)) return false;

            _groups[groupTag] = group;

            // When the group terminates, remove it from the registry
            WatchProcess(relay, group, onUnregister: _ => _groups.Remove(groupTag));

            return true;
        }

        internal bool RegisterWithGroup(Tag groupTag, AbstractRuntimeProcess process, ProcessDataPacket explicitData, bool useSharedData, out ProcessRelay relay)
        {
            relay = default;
            if (process == null) return false;

            // Auto-create the group if it doesn't exist
            if (!_groups.TryGetValue(groupTag, out var group))
            {
                group = new GroupProcess(groupTag.GetName());
                if (!RegisterGroup(groupTag, group, ProcessDataPacket.Default(), out _))
                    return false;
            }

            // Determine which data packet the member should use
            var data = useSharedData && explicitData == null
                ? group.Data   // Share the group's data packet
                : explicitData ?? ProcessDataPacket.Default();

            // Register the process normally, in this world
            if (!ProcessControl.Register(process).With(data).In(this).Run(out relay))
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

        public bool Unregister(ProcessControlBlock pcb)
        {
            if (pcb.Relay is null) return false;

            if (_waiting.Contains(pcb.CacheIndex)) _waiting.Remove(pcb.CacheIndex);
            else RemoveFromStepping(pcb);

            CheckWatcherOnUnregister(pcb);

            pcb.Handler?.HandlerVoidProcess(pcb.Relay);
            pcb.Wrapper.Data?.HandlerVoidProcess(pcb.Relay);

            // Remove from hierarchy
            _hierarchy.Unregister(pcb.CacheIndex);

            // Clear GAS association
            var gas = ResolveOwningGAS(pcb.Handler, pcb.Wrapper.Data);
            if (gas != null && _gasProcesses.TryGetValue(gas, out var set))
            {
                set.Remove(pcb.CacheIndex);
                if (set.Count == 0) _gasProcesses.Remove(gas);
            }

            if (OutputLogs)
            {
                if (DetailedLogs) Debug.Log($"[Process Control] Unregister process: {pcb.Wrapper.ProcessName} ({pcb.Handler?.GetName() ?? "No handler"})");
                else Debug.Log($"[Process Control] Unregister process: {pcb.Wrapper.ProcessName}");
            }

            // Fired ahead of the removal, as the callback says, and guarded so a throw can't stop it
            if (_active.ContainsKey(pcb.CacheIndex))
            {
                try
                {
                    Callbacks?.ProcessUnregistered(pcb.Relay, pcb.Wrapper.Data);
                    if (gas != null) Callbacks?.GASProcessUnregistered(gas, pcb.Relay, pcb.Wrapper.Data);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            if (!_active.Remove(pcb.CacheIndex)) return false;
            ForgeCounters.Terminations.Value++;
            return true;
        }
        
        public Dictionary<int, ProcessControlBlock> FetchActiveProcesses() => _active;
        
        /// <summary>True while ProcessControl itself is paused.</summary>
        public bool IsPaused => State is EProcessControlState.Waiting or EProcessControlState.ClosedWaiting;

        /// <summary>True while ProcessControl takes new processes, which it stops doing once closed or terminated.</summary>
        public bool AcceptsNewProcesses => CanAcceptNewProcesses();

        private bool CanAcceptNewProcesses()
        {
            return State is not (EProcessControlState.Closed 
                or EProcessControlState.ClosedWaiting 
                or EProcessControlState.Terminated 
                or EProcessControlState.TerminatedImmediately);
        }
        


        public void SetControlState(EProcessControlState state)
        {
            if (state == State) return;

            var oldState = State;
            State = state;
            SetAllProcessesUponStateChange();
            Callbacks?.ControlStateChanged(oldState, state);
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
        /// Pauses/waits the process. If cascading is enabled, also pauses children. It takes effect at once:
        /// a process that can hold its run keeps it, and one that can't has it cancelled, to start again on
        /// resume. Refused for a process that's been told to terminate.
        /// </summary>
        public bool Wait(int cacheIndex, bool cascade = true)
        {
            if (!_active.TryGetValue(cacheIndex, out var pcb)) 
                return false;
            // Already stopped by ProcessControl, asking makes the pause its own, so it outlasts ProcessControl's
            if (pcb.HeldByControl) pcb.KeepPaused();
            else if (ValidateStateTransition(pcb, EProcessState.Waiting)) pcb.ForceIntoState(EProcessState.Waiting);
            else return false;

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
            
            _active[cacheIndex].QueueTerminate();
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
        
        /// <summary>
        /// Ends the process of an object that has been destroyed, which nothing else tells ProcessControl about.
        /// </summary>
        public void EndDestroyedProcess(ProcessRelay relay)
        {
            if (_active.TryGetValue(relay.CacheIndex, out var pcb) && pcb.Relay == relay)
            {
                TerminateImmediate(relay.CacheIndex);
            }
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

                // Skip siblings that opted out of cascade
                if (siblings.Contains(targetIndex) && !targetPcb.Wrapper.ParticipateInSiblingCascade)
                    continue;

                // Skip if already in target state
                if (targetPcb.State == state)
                {
                    if (state == EProcessState.Waiting) targetPcb.KeepPaused();
                    continue;
                }

                switch (state)
                {
                    case EProcessState.Running:
                        if (ValidateStateTransition(targetPcb, EProcessState.Running))
                            targetPcb.QueueNextState(EProcessState.Running);
                        break;
                    case EProcessState.Waiting:
                        if (ValidateStateTransition(targetPcb, EProcessState.Waiting))
                            targetPcb.ForceIntoState(EProcessState.Waiting);
                        break;
                    case EProcessState.Terminated:
                        targetPcb.QueueTerminate();
                        break;
                }
            }
        }
        


        private bool SetProcess(ProcessControlBlock pcb)
        {
            if (pcb.State == EProcessState.Created && !PrepareCreatedProcess(pcb)) return false;
            
            var state = GetDefaultTransitionState(pcb);
            pcb.QueueNextState(state);

            // Registered during a pause, it waits with the rest and goes when they do, unless it waits to be told
            if (IsPaused && pcb.Wrapper.Lifecycle != EProcessLifecycle.RequiresControl) pcb.WaitForControl();

            return true;
        }

        private bool PrepareCreatedProcess(ProcessControlBlock pcb)
        {
            pcb.Wrapper.ConfigureWrapper();

            // By the live object, which for a prefab is its clone. The prefab is shared, so every clone of it
            // landed on one node and ending any of them cascaded to the rest (PC-3)
            if (pcb.Wrapper.TryGetProcess<AbstractMonoProcess>(out var mono)) _hierarchy.Register(mono, pcb.CacheIndex);

            _waiting.Add(pcb.CacheIndex);
            _active[pcb.CacheIndex] = pcb;
            ForgeCounters.Registrations.Value++;

            // Track GAS-owned processes so observers can query "what processes does this GAS own?"
            var gas = ResolveOwningGAS(pcb.Handler, pcb.Wrapper.Data);
            if (gas != null)
            {
                if (!_gasProcesses.TryGetValue(gas, out var set))
                {
                    set = new HashSet<int>();
                    _gasProcesses[gas] = set;
                }
                set.Add(pcb.CacheIndex);
            }

            if (OutputLogs)
            {
                string msg = DetailedLogs
                    ? $"[Process Control] Registered Process \"{pcb.Wrapper.ProcessName}\" ({pcb.Handler})"
                    : $"[Process Control] Registered Process: {pcb.Wrapper.ProcessName}";
                Debug.Log(msg);
            }

            // The process's own code, with the block already in place. A throw goes through the usual
            // teardown and refuses the process, rather than leaving it half registered (PC-15)
            try
            {
                pcb.Initialize();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                pcb.ForceIntoState(EProcessState.Terminated);
                return false;
            }

            // WhenInitialize applies the parent the process's data asks for after the hierarchy filed it, so it's filed
            // again where its transform now is. It stayed no one's child (PC-28)
            if (mono) _hierarchy.Reparent(mono.gameObject);
            if (mono)
            {
                // A class that hides OnDestroy is watched for as long as it runs, one asleep only until it wakes (PC-31)
                if (HidesOnDestroy(mono)) _unheard.Add(pcb.CacheIndex);
                else if (!mono.gameObject.activeInHierarchy) _dormant.Add(pcb.CacheIndex);
            }

            // Fire callbacks AFTER pcb.Initialize so state is fully consistent
            Callbacks?.ProcessRegistered(pcb.Relay, pcb.Wrapper.Data);
            if (gas != null) Callbacks?.GASProcessRegistered(gas, pcb.Relay, pcb.Wrapper.Data);

            return true;
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
                // Only a pause or the end of one moves anything. Giving every process its default transition
                // here parked Synchronous processes, started RequiresControl ones and re-ran finished ones,
                // and closing ended GameRoot and everything it handled (PC-7)
                var pcbs = _active.Values.ToArray();
                foreach (var pcb in pcbs)
                {
                    if (pcb.State == EProcessState.Created) SetProcess(pcb);
                    else if (IsPaused) pcb.HoldForControl();
                    else pcb.ReleaseFromControl();
                }
            }
        }
        


        private void MoveToStepping(ProcessControlBlock pcb)
        {
            var timing = pcb.Wrapper.StepTiming;
            int priority = GetEffectivePriority(pcb);

            _steppedAs[pcb.CacheIndex] = timing;
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
            if (!_steppedAs.TryGetValue(pcb.CacheIndex, out var timing)) return;
            _steppedAs.Remove(pcb.CacheIndex);

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
        


        public void ProcessWillChangeState(ProcessControlBlock pcb, EProcessState state)
        {
            if (state == EProcessState.Created) return;

            // A world of its own's tick is in force as a process moves, between its steps as at them: a run that starts
            // now waits on it (FEAT-10)
            var tick = Control is null ? ForgeTick.Enter(ownTick) : null;
            try
            {
                ChangeState(pcb, state);
            }
            finally
            {
                if (tick != null) ForgeTick.Enter(tick);
            }
        }

        private void ChangeState(ProcessControlBlock pcb, EProcessState state)
        {
            ForgeCounters.StateChanges.Value++;

            // Reported ahead of acting on it: terminating takes the watchers away, and a run that ends at
            // once moves the state on again, so afterwards was too late to say this (PC-13)
            ReportStateChange(pcb);

            // A listener can move the process on itself
            if (pcb.State != state) return;

            switch (state)
            {
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
        }

        private void ReportStateChange(ProcessControlBlock pcb)
        {
            try
            {
                CheckWatcherOnStateChange(pcb);
                Callbacks?.ProcessStateChanged(pcb.Relay, pcb.LastState, pcb.State, pcb.Wrapper.Data);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
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
        


        // Simplified state transition tables using tuples for cleaner code
        internal static readonly Dictionary<(EProcessControlState control, EProcessLifecycle lifecycle, EProcessState from), EProcessState> 
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
                // The table has the last word on a resume, so what a paused or closed ProcessControl keeps still
                // stays still. Allowing every resume let Unpause walk a process out of a pause (PC-27)
                (EProcessState.Waiting, EProcessState.Running, _) => GetDefaultTransitionState(pcb) == EProcessState.Running,
                
                // Waiting transitions
                (EProcessState.Running, EProcessState.Waiting, _) => !pcb.TerminateRequested,
                (EProcessState.Created, EProcessState.Waiting, EProcessLifecycle.RequiresControl) => true,
                
                _ => false
            };
        }
        

        /// <summary>
        /// A Mono process's transform has a new parent, so the hierarchy files it again under its new ancestors. It
        /// heard only where the process registered (PC-28, Decision 11).
        /// </summary>
        public void ProcessReparented(ProcessRelay relay)
        {
            if (relay is null || !_active.TryGetValue(relay.CacheIndex, out var pcb) || pcb.Relay != relay) return;
            if (pcb.Wrapper.TryGetProcess<AbstractMonoProcess>(out var mono) && mono) _hierarchy.Reparent(mono.gameObject);
        }

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

        /// <summary>Whether a group of the tag is registered here, and the group.</summary>
        public bool TryGetGroup(Tag groupTag, out GroupProcess group) => _groups.TryGetValue(groupTag, out group);

        /// <summary>Whether a group of the tag is registered here.</summary>
        public bool HasGroup(Tag groupTag) => _groups.ContainsKey(groupTag);

        /// <summary>The cache indices of the processes the GAS owns here.</summary>
        public IReadOnlyCollection<int> ProcessIdsForGAS(IGameplayAbilitySystem gas) =>
            gas != null && _gasProcesses.TryGetValue(gas, out var set) ? (IReadOnlyCollection<int>)set : Array.Empty<int>();

        /// <summary>The relays of the processes the GAS owns here.</summary>
        public IEnumerable<ProcessRelay> ProcessesForGAS(IGameplayAbilitySystem gas)
        {
            if (gas == null || !_gasProcesses.TryGetValue(gas, out var set)) yield break;
            foreach (int idx in set)
            {
                if (_active.TryGetValue(idx, out var pcb)) yield return pcb.Relay;
            }
        }

        /// <summary>How many processes the GAS owns here.</summary>
        public int ProcessCountForGAS(IGameplayAbilitySystem gas) => gas != null && _gasProcesses.TryGetValue(gas, out var set) ? set.Count : 0;

        /// <summary>
        /// Terminates every process the GAS owns here, at once when <paramref name="immediate"/> is set. Returns how many
        /// were told to.
        /// </summary>
        public int TerminateAllForGAS(IGameplayAbilitySystem gas, bool immediate = false)
        {
            if (gas == null || !_gasProcesses.TryGetValue(gas, out var set) || set.Count == 0) return 0;

            // Copy because Terminate paths may mutate the backing set
            var indices = new List<int>(set);
            int count = 0;
            foreach (int idx in indices)
            {
                if (immediate ? TerminateImmediate(idx) : Terminate(idx)) count++;
            }
            return count;
        }

        /// <summary>The relays of every process the world holds.</summary>
        public ProcessRelay[] GetRelays() => _active.Values.Select(pcb => pcb.Relay).ToArray();

        internal string HierarchyDebugInfo() => _hierarchy.GetDebugInfo();
    }
}
