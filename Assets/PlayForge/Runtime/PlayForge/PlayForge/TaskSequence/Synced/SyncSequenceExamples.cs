using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Examples
{
    /// <summary>
    /// Demo synchronous sequences and processes you can drop into a scene to test.
    /// Each MonoBehaviour self-registers with ProcessControl on Start.
    ///
    /// These demos use LazyMonoProcess with Synchronous lifecycle and the unified
    /// TaskSequenceBuilder, demonstrating the consolidated sync/async framework.
    ///
    /// Setup:
    ///   1. Ensure a ProcessControl instance exists in the scene (via the ProcessControl prefab).
    ///   2. Add the demo component to any GameObject.
    ///   3. Assign inspector fields (target, waypoints, etc.) as needed.
    ///   4. Enter Play mode.
    /// </summary>
    public static class SyncSequenceExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // TAGS
        // ═══════════════════════════════════════════════════════════════════════════

        private static readonly Tag TICK_COUNT = Tag.GenerateAsUnique("SyncDemo.TickCount");
        private static readonly Tag PATROL_INDEX = Tag.GenerateAsUnique("SyncDemo.PatrolIndex");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 1 — Simple Repeating Tick
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Logs a tick every N seconds. Demonstrates the simplest repeating sync sequence.
    /// Pause via relay.Pause(), resume via relay.Unpause(), stop via relay.Terminate().
    /// </summary>
    public class SyncDemo_RepeatingTick : LazyMonoProcess
    {
        [Header("Config")]
        [Tooltip("Seconds between ticks")]
        public float interval = 1f;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        private static readonly Tag TICK = Tag.GenerateAsUnique("SyncDemo.Tick");

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create("Repeating Tick")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Wait")
                    .Task(SyncSeq.Delay(interval))
                )
                .Stage(s => s
                    .WithName("Tick")
                    .Do(d =>
                    {
                        int count = d.Increment(TICK);
                        Debug.Log($"[SyncDemo_RepeatingTick] Tick #{count} at {Time.time:F2}s");
                    })
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 2 — Patrol Between Waypoints
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Moves this transform between a list of waypoints at a fixed speed, looping forever.
    /// Demonstrates SyncSeq.MoveTowards with repeating stages.
    /// Assign waypoint transforms in the inspector.
    /// </summary>
    public class SyncDemo_Patrol : LazyMonoProcess
    {
        [Header("Config")]
        public Transform[] waypoints;
        public float speed = 3f;
        public float stoppingDistance = 0.1f;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;
        private int _waypointIndex;

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);
            _waypointIndex = 0;

            _runner = TaskSequenceBuilder.Create("Patrol")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Move to Waypoint")
                    .SyncTask((_, dt) =>
                    {
                        if (waypoints == null || waypoints.Length == 0) return true;
                        var target = waypoints[_waypointIndex];
                        if (!target) return true;

                        float dist = Vector3.Distance(transform.position, target.position);
                        if (dist <= stoppingDistance)
                        {
                            _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
                            return true;
                        }

                        transform.position = Vector3.MoveTowards(
                            transform.position, target.position, speed * dt);
                        return false;
                    })
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 3 — Chase Target
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Chases a target transform at a fixed speed, faces it while moving.
    /// Demonstrates parallel tasks (MoveTowards + LookAtTracking) in a WhenAll stage.
    /// Pauses when within stopping distance, resumes when target moves away.
    /// </summary>
    public class SyncDemo_ChaseTarget : LazyMonoProcess
    {
        [Header("Config")]
        public Transform target;
        public float moveSpeed = 5f;
        public float turnSpeed = 360f;
        public float stoppingDistance = 1.5f;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create("Chase Target")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Chase")
                    .WhenAll()
                    .Task(SyncSeq.MoveTowards(transform, target, moveSpeed, stoppingDistance))
                    .Task(SyncSeq.LookAtTracking(transform, target, turnSpeed))
                )
                .Stage(s => s
                    .WithName("Wait for target to move away")
                    .WaitUntil(() =>
                        target && Vector3.Distance(transform.position, target.position) > stoppingDistance * 2f)
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 4 — Bobbing Object
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bobs an object up and down using timed MoveBy stages.
    /// Demonstrates basic timed sync tasks with easing curves.
    /// </summary>
    public class SyncDemo_Bob : LazyMonoProcess
    {
        [Header("Config")]
        public float height = 1f;
        public float duration = 1f;
        public AnimationCurve ease;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create("Bob")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Up")
                    .Task(SyncSeq.MoveBy(transform, Vector3.up * height, duration, ease))
                )
                .Stage(s => s
                    .WithName("Down")
                    .Task(SyncSeq.MoveBy(transform, Vector3.down * height, duration, ease))
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 5 — Orbit Around Target
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Orbits around a target transform at a configurable radius and speed.
    /// Demonstrates a never-completing task with manual termination.
    /// </summary>
    public class SyncDemo_Orbit : LazyMonoProcess
    {
        [Header("Config")]
        public Transform center;
        public float radius = 3f;
        public float degreesPerSecond = 90f;
        public Vector3 axis = Vector3.up;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create("Orbit")
                .Stage(s => s
                    .WithName("Orbiting")
                    .Task(SyncSeq.Orbit(transform, center, radius, degreesPerSecond, axis))
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 6 — Damage Over Time (DoT)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Applies damage every tick interval for a total number of ticks, then terminates.
    /// Demonstrates a finite sync sequence with data packet state tracking.
    /// </summary>
    public class SyncDemo_DamageOverTime : LazyMonoProcess
    {
        [Header("Config")]
        public float tickInterval = 0.5f;
        public int totalTicks = 10;
        public float damagePerTick = 5f;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;
        private int _ticksRemaining;
        private float _totalDamage;

        private static readonly Tag DOT_TICKS = Tag.GenerateAsUnique("SyncDemo.DoTTicks");

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);
            _ticksRemaining = totalTicks;
            _totalDamage = 0f;

            _runner = TaskSequenceBuilder.Create("Damage Over Time")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Wait")
                    .Task(SyncSeq.Delay(tickInterval))
                )
                .Stage(s => s
                    .WithName("Apply Damage")
                    .Do(_ =>
                    {
                        _ticksRemaining--;
                        _totalDamage += damagePerTick;
                        Debug.Log($"[SyncDemo_DoT] Tick! Damage: {damagePerTick:F1} " +
                                  $"(Total: {_totalDamage:F1}, Remaining: {_ticksRemaining})");
                    })
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            if (_runner.IsComplete) return;

            _runner.Step(_data, Time.deltaTime);

            if (_ticksRemaining <= 0)
            {
                Debug.Log($"[SyncDemo_DoT] Complete. Total damage: {_totalDamage:F1}");
                relay.Terminate();
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 7 — Pulse Scale (Visual Feedback)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Continuously pulses an object's scale up and down.
    /// Demonstrates PunchScale in a repeating sync sequence.
    /// </summary>
    public class SyncDemo_PulseScale : LazyMonoProcess
    {
        [Header("Config")]
        public float intensity = 0.3f;
        public float duration = 0.6f;
        public float pauseBetween = 0.4f;

        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        protected void Awake()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create("Pulse Scale")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Punch")
                    .Task(SyncSeq.PunchScale(transform, intensity, duration))
                )
                .Stage(s => s
                    .WithName("Pause")
                    .Task(SyncSeq.Delay(pauseBetween))
                )
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEMO 8 — Multi-Stage Choreography (Runtime Process)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A class-based (RuntimeProcess) demo that runs a multi-stage sequence:
    /// scale up, wait, scale down, wait, repeat.
    /// Demonstrates LazyRuntimeProcess with Synchronous lifecycle.
    ///
    /// Usage:
    ///   var demo = new SyncDemo_RuntimeChoreography(myTransform);
    ///   ProcessControl.Instance.Register(demo, ProcessDataPacket.RootDefault(), out var relay);
    /// </summary>
    public class SyncDemo_RuntimeChoreography : LazyRuntimeProcess
    {
        private readonly Transform _target;
        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;

        public SyncDemo_RuntimeChoreography(Transform target)
            : base("RuntimeChoreography", EProcessStepPriorityMethod.First, 0,
                   EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            _target = target;
        }

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            _data = new SequenceDataPacket(regData);

            _runner = TaskSequenceBuilder.Create("Runtime Choreography")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Scale Up")
                    .Task(SyncSeq.ScaleTo(_target, 1.5f, 0.5f))
                )
                .Delay(0.3f)
                .Stage(s => s
                    .WithName("Scale Down")
                    .Task(SyncSeq.ScaleTo(_target, 1f, 0.5f))
                )
                .Delay(0.3f)
                .BuildSyncRunner();
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            _runner.Step(_data, Time.deltaTime);
        }
    }
}
