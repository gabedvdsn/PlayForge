using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// What the synced scene components share: a LazyMonoProcess that builds a synced sequence, a SyncedTaskSequence, as
    /// its process starts, and steps it once a frame. ProcessControl registers every process in a scene as it starts, so
    /// a component needs only to be there. Its Lifecycle and Timing are its inspector's, in the Process section: a new one
    /// is Synchronous and steps in Update, and a Timing of LateUpdate or FixedUpdate alone steps it there instead.
    /// <para>
    /// A synced sequence steps its tasks and has no runtime, so it has no conditions, no injections, no async tasks, which
    /// finish at once and do nothing, and no timers but a stage's own time limit. Do, SyncTask, WaitUntil, SyncDelay and
    /// the synced library's tasks are what it runs. Its owner steers its runner by hand: SkipStage, JumpToStage,
    /// BreakStageRepeat, Interrupt and Reset.
    /// </para>
    /// </summary>
    public abstract class SyncedSequenceComponent : LazyMonoProcess
    {
        [Tooltip("The clock its sequence runs by: none for the sequence's own or the game's; Unscaled runs it while the game is paused")]
        [SerializeReference] public AbstractClockChoice Clock;

        /// <summary>The runner stepping the component's sequence, or null with none.</summary>
        protected SyncedTaskSequence Runner { get; private set; }

        /// <summary>The packet the sequence runs on, made from the one the process was registered with.</summary>
        protected SequenceDataPacket Packet { get; private set; }

        // Unity calls this as the component is added: a new one is synced and steps in Update, and both are the
        // inspector's to change
        protected virtual void Reset()
        {
            ProcessLifecycle = EProcessLifecycle.Synchronous;
            ProcessTiming = EProcessStepTiming.Update;
        }

        public override void WhenInitialize()
        {
            base.WhenInitialize();

            // A scene's processes are registered with a plain packet, which a sequence's takes the data of
            Packet = regData as SequenceDataPacket ?? new SequenceDataPacket(regData);
            // The clock picked, on the packet, which comes before the sequence's own; none leaves the packet's
            if (Clock?.ClockFor(Packet) is { } picked) Packet.Clock = picked;
            Runner = BuildRunner();
            if (Runner != null)
                ForgeLog.Verbose($"{name}: a synced sequence of {Runner.StageCount} stages, on {Runner.ClockFor(Packet).GetType().Name}.", this);
        }

        /// <summary>The component's sequence, built as its process starts. With none, it steps nothing.</summary>
        protected virtual SyncedTaskSequence BuildRunner() => null;

        /// <summary>Called as the sequence finishes, or is stopped from inside: one that repeats never finishes. Does nothing here.</summary>
        protected virtual void WhenFinished() { }

        /// <summary>
        /// The time a step moves the sequence on by: a step of its runner's clock, the packet's, its sequence's
        /// (WithClock) or the game's, or that clock's fixed step in FixedUpdate.
        /// </summary>
        protected virtual float StepTime(EProcessStepTiming timing)
        {
            var clock = Runner.ClockFor(Packet);
            return timing == EProcessStepTiming.FixedUpdate ? clock.FixedDeltaTime : clock.DeltaTime;
        }

        public override void WhenUpdate() => StepAt(EProcessStepTiming.Update);

        public override void WhenLateUpdate() => StepAt(EProcessStepTiming.LateUpdate);

        public override void WhenFixedUpdate() => StepAt(EProcessStepTiming.FixedUpdate);

        // Ended part way, as with its scene, it cleans the stage it was in
        public override void WhenTerminate()
        {
            Runner?.Interrupt(Packet);
            base.WhenTerminate();
        }

        // Once a frame, at one timing: Update if the process steps then, else LateUpdate, else FixedUpdate, as a sequence's
        // own process does. A runner that has finished, or was stopped, waits here for a Reset
        private void StepAt(EProcessStepTiming timing)
        {
            if (Runner == null || timing != SteppedIn || Runner.IsComplete || Runner.State == SyncedTaskSequence.ERunnerState.Interrupted) return;
            if (Runner.Step(Packet, StepTime(timing))) WhenFinished();
        }

        private EProcessStepTiming SteppedIn => ProcessTiming switch
        {
            EProcessStepTiming.LateUpdate or EProcessStepTiming.LateAndFixed => EProcessStepTiming.LateUpdate,
            EProcessStepTiming.FixedUpdate => EProcessStepTiming.FixedUpdate,
            _ => EProcessStepTiming.Update
        };
    }

    /// <summary>
    /// A tick every few seconds, for good: the simplest synced sequence, a wait and an action, repeating. Its runner's
    /// OnComplete hears each time round, Verbose in ForgeLog. Pause its process to stop the ticks, unpause it to go on.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Ticker")]
    public class SyncedTicker : SyncedSequenceComponent
    {
        [Tooltip("Seconds between ticks")]
        [Min(0f)] public float Interval = 1f;

        private static readonly Tag TICKS = Tag.GenerateAsUnique("Examples.Ticks");

        protected override SyncedTaskSequence BuildRunner()
        {
            var runner = TaskSequenceBuilder.Create("Synced Ticker")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Wait")
                    .Task(SyncSeq.Delay(Interval)))
                .Stage(s => s
                    .WithName("Tick")
                    .Do(d => ForgeLog.Info($"Tick {d.Increment(TICKS)}, at {ForgeTime.Time:F1}s.", this)))
                .BuildSyncRunner();

            runner.OnComplete = (data, completed) => ForgeLog.Verbose($"Round {data.GetPrimary<int>(TICKS)} done.", this);
            return runner;
        }
    }

    /// <summary>
    /// A patrol: to each waypoint in turn at a steady speed, turning to face where it's going, with a pause at each, for
    /// good. A builder is plain code, so the stages are made in a loop, two to a waypoint. Each move stage is WhenAny:
    /// the move ends it, and the turning, which never ends by itself, stops with it.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Patrol")]
    public class SyncedPatrol : SyncedSequenceComponent
    {
        [Tooltip("The points it walks between, in order")]
        public Transform[] Waypoints;

        [Tooltip("Its speed, in units a second")]
        [Min(0f)] public float Speed = 3f;

        [Tooltip("How fast it turns, in degrees a second")]
        [Min(0f)] public float TurnSpeed = 360f;

        [Tooltip("Seconds it waits at each waypoint")]
        [Min(0f)] public float PauseAtEach = 0.5f;

        protected override SyncedTaskSequence BuildRunner()
        {
            if (Waypoints == null || Waypoints.Length == 0)
            {
                ForgeLog.Warning("The patrol has no waypoints.", this);
                return null;
            }

            var builder = TaskSequenceBuilder.Create("Synced Patrol").WithRepeat(true);
            foreach (var waypoint in Waypoints)
            {
                if (!waypoint) continue;

                builder
                    .Stage(s => s
                        .WithName($"To {waypoint.name}")
                        .WhenAny()
                        .Task(SyncSeq.MoveTowards(transform, waypoint, Speed, 0.05f))
                        .Task(SyncSeq.LookAtTracking(transform, waypoint, TurnSpeed)))
                    .Stage(s => s
                        .WithName($"At {waypoint.name}")
                        .SyncDelay(PauseAtEach));
            }

            return builder.BuildSyncRunner();
        }
    }

    /// <summary>
    /// A chase: after its target at a steady speed, facing it, until it's in reach or it gives up; then it waits for the
    /// target to get away, and chases again. The chase stage is WhenAny over three tasks: the move, which ends in reach,
    /// the turning, which never ends, and the delay that gives up. Its OnTerminate says which.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Chase")]
    public class SyncedChase : SyncedSequenceComponent
    {
        [Tooltip("What it chases")]
        public Transform Target;

        [Tooltip("Its speed, in units a second")]
        [Min(0f)] public float Speed = 5f;

        [Tooltip("How fast it turns, in degrees a second")]
        [Min(0f)] public float TurnSpeed = 360f;

        [Tooltip("How close it gets before it stops")]
        [Min(0f)] public float Reach = 1.5f;

        [Tooltip("Seconds it chases before it gives up")]
        [Min(0f)] public float GiveUpAfter = 6f;

        protected override SyncedTaskSequence BuildRunner()
        {
            if (!Target)
            {
                ForgeLog.Warning("The chase has no target.", this);
                return null;
            }

            return TaskSequenceBuilder.Create("Synced Chase")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Chase")
                    .WhenAny()
                    .Task(SyncSeq.MoveTowards(transform, Target, Speed, Reach))
                    .Task(SyncSeq.LookAtTracking(transform, Target, TurnSpeed))
                    .Task(SyncSeq.Delay(GiveUpAfter))
                    .OnTerminate((ctx, completed) => ForgeLog.Info(Within(1f) ? "Caught up." : "Gave up.", this)))
                .Stage(s => s
                    .WithName("Wait for it to get away")
                    .WaitUntil(() => !Within(2f)))
                .BuildSyncRunner();
        }

        // Whether the target is within its reach, times a factor
        private bool Within(float times) => Target && Vector3.Distance(transform.position, Target.position) <= Reach * times + 0.01f;
    }

    /// <summary>
    /// Bobs up and down for good, eased by a curve of the inspector's: two timed moves, repeating.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Bob")]
    public class SyncedBob : SyncedSequenceComponent
    {
        [Tooltip("How far it rises")]
        public float Height = 0.5f;

        [Tooltip("Seconds each way")]
        [Min(0f)] public float Duration = 1f;

        [Tooltip("The easing each way; with none, linear")]
        public AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        protected override SyncedTaskSequence BuildRunner()
        {
            return TaskSequenceBuilder.Create("Synced Bob")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Up")
                    .Task(SyncSeq.MoveBy(transform, Vector3.up * Height, Duration, Ease)))
                .Stage(s => s
                    .WithName("Down")
                    .Task(SyncSeq.MoveBy(transform, Vector3.down * Height, Duration, Ease)))
                .BuildSyncRunner();
        }
    }

    /// <summary>
    /// Circles a center for good, and stops and starts again on a key. Interrupt stops its runner and cleans the stage it
    /// was in; Reset readies it to start again from its first stage.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Orbit")]
    public class SyncedOrbit : SyncedSequenceComponent
    {
        [Tooltip("What it circles")]
        public Transform Center;

        [Tooltip("How far from the center it circles")]
        [Min(0f)] public float Radius = 3f;

        [Tooltip("Its speed round, in degrees a second")]
        public float DegreesPerSecond = 90f;

        [Tooltip("The axis it circles about")]
        public Vector3 Axis = Vector3.up;

        [Tooltip("The key that stops it, and starts it again, read through ForgeInput")]
        public KeyCode ToggleKey = KeyCode.O;

        protected override SyncedTaskSequence BuildRunner()
        {
            if (!Center)
            {
                ForgeLog.Warning("The orbit has no center.", this);
                return null;
            }

            return TaskSequenceBuilder.Create("Synced Orbit")
                .Stage(s => s
                    .WithName("Orbit")
                    .Task(SyncSeq.Orbit(transform, Center, Radius, DegreesPerSecond, Axis)))
                .BuildSyncRunner();
        }

        public override void WhenUpdate()
        {
            if (Runner != null && ForgeInput.Current.KeyPressed(ToggleKey))
            {
                if (Runner.State == SyncedTaskSequence.ERunnerState.Interrupted)
                {
                    Runner.Reset();
                    ForgeLog.Info("The orbit starts again.", this);
                }
                else
                {
                    Runner.Interrupt(Packet);
                    ForgeLog.Info("The orbit stops.", this);
                }
            }

            base.WhenUpdate();
        }
    }

    /// <summary>
    /// A fire that burns for a number of ticks, then goes out and takes its object with it: a finite synced sequence,
    /// whose process ends as it finishes, as a mono process's end destroys its object. The burning is a repeating stage
    /// that counts its rounds in the packet and breaks its own repeat from OnRepeat, through the packet, as an async
    /// sequence's would. A key douses it early, breaking the repeat through the runner.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Burn")]
    public class SyncedBurn : SyncedSequenceComponent
    {
        [Tooltip("Seconds between burns")]
        [Min(0.01f)] public float Interval = 0.5f;

        [Tooltip("How many times it burns")]
        [Min(1)] public int Burns = 6;

        [Tooltip("The damage each burn does")]
        public float DamagePerBurn = 5f;

        [Tooltip("The key that douses it early, read through ForgeInput")]
        public KeyCode DouseKey = KeyCode.F;

        [Tooltip("Ends its process as it goes out, which destroys its object")]
        public bool DestroyWhenOut = true;

        private static readonly Tag BURNED = Tag.GenerateAsUnique("Examples.Burned");

        protected override SyncedTaskSequence BuildRunner()
        {
            return TaskSequenceBuilder.Create("Synced Burn")
                .Stage(s => s
                    .WithName("Burning")
                    .WithRepeat(true)
                    .OnInit(d => d.SetPrimary(BURNED, 0))
                    .OnRepeat(ctx =>
                    {
                        if (ctx.Data.GetPrimary<int>(BURNED) >= Burns) ctx.Data.BreakStageRepeat();
                    })
                    // Each round burns as it starts, then waits its interval
                    .Do(d => ForgeLog.Info($"Burn {d.Increment(BURNED)}, for {DamagePerBurn}.", this))
                    .Task(SyncSeq.Delay(Interval)))
                .Do(d => ForgeLog.Info($"The fire goes out after {d.GetPrimary<int>(BURNED)} burns.", this))
                .BuildSyncRunner();
        }

        public override void WhenUpdate()
        {
            if (Runner != null && ForgeInput.Current.KeyPressed(DouseKey) && Runner.BreakStageRepeat())
            {
                ForgeLog.Info("Doused: this burn is the last.", this);
            }

            base.WhenUpdate();
        }

        protected override void WhenFinished()
        {
            if (DestroyWhenOut) Relay.Terminate();
        }
    }

    /// <summary>
    /// Pulses its object's scale for good, a punch and a pause. With its Clock picked as Unscaled it goes on while the game's
    /// time is slowed or stopped, as a menu's highlight should, its runner stepping by the clock its packet carries.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Pulse")]
    public class SyncedPulse : SyncedSequenceComponent
    {
        [Tooltip("How far it swells, as a share of its size")]
        [Min(0f)] public float Intensity = 0.3f;

        [Tooltip("Seconds a pulse takes")]
        [Min(0.01f)] public float Duration = 0.6f;

        [Tooltip("Seconds between pulses")]
        [Min(0f)] public float PauseBetween = 0.4f;

        protected override SyncedTaskSequence BuildRunner()
        {
            return TaskSequenceBuilder.Create("Synced Pulse")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Punch")
                    .Task(SyncSeq.PunchScale(transform, Intensity, Duration)))
                .Stage(s => s
                    .WithName("Pause")
                    .SyncDelay(PauseBetween))
                .BuildSyncRunner();
        }
    }

    /// <summary>
    /// A signal cycling green, amber and red for good, steered by hand: one key skips the light it's on, and others jump
    /// to a light. Amber is a critical stage, and the runner's OnCriticalSectionExited hears it end each time round. A
    /// synced runner has no injections, so it refuses none of this: steering one is its owner's to get right.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Synced Signal")]
    public class SyncedSignal : SyncedSequenceComponent
    {
        [Tooltip("Seconds on green")]
        [Min(0f)] public float Green = 4f;

        [Tooltip("Seconds on amber")]
        [Min(0f)] public float Amber = 1.5f;

        [Tooltip("Seconds on red")]
        [Min(0f)] public float Red = 4f;

        [Tooltip("The key that skips the light it's on, read through ForgeInput")]
        public KeyCode SkipKey = KeyCode.Tab;

        [Tooltip("The keys that jump to green, amber and red")]
        public KeyCode[] JumpKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

        protected override SyncedTaskSequence BuildRunner()
        {
            var runner = TaskSequenceBuilder.Create("Synced Signal")
                .WithRepeat(true)
                .Stage(s => Lamp(s, "Green", Green))
                .Stage(s => Lamp(s, "Amber", Amber).WithCriticalFlag(true))
                .Stage(s => Lamp(s, "Red", Red))
                .BuildSyncRunner();

            runner.OnCriticalSectionExited = () => ForgeLog.Info("Past amber.", this);
            return runner;
        }

        private StageBuilder Lamp(StageBuilder stage, string color, float seconds) => stage
            .WithName(color)
            .OnInit(_ => ForgeLog.Info($"{color}.", this))
            .SyncDelay(seconds);

        public override void WhenUpdate()
        {
            if (Runner != null)
            {
                var input = ForgeInput.Current;
                if (input.KeyPressed(SkipKey) && Runner.SkipStage(Packet))
                {
                    ForgeLog.Info($"Skipped to light {Runner.StageIndex}.", this);
                }

                for (int i = 0; i < JumpKeys.Length; i++)
                {
                    if (input.KeyPressed(JumpKeys[i]) && Runner.JumpToStage(Packet, i)) ForgeLog.Info($"Jumped to light {i}.", this);
                }
            }

            base.WhenUpdate();
        }
    }

    /// <summary>
    /// The same, as a runtime process: a plain class with no object of its own, which ProcessControl steps once it's
    /// registered, its lifecycle and timing given as it's made. It grows and shrinks a transform for a number of rounds,
    /// its runner's OnComplete counting them, then ends its process. Code starts it:
    /// <code>var relay = SyncedChoreography.Start(transform, rounds: 3);</code>
    /// </summary>
    public class SyncedChoreography : LazyRuntimeProcess
    {
        private readonly Transform _target;
        private readonly int _rounds;
        private SyncedTaskSequence _runner;
        private SequenceDataPacket _data;
        private int _done;

        public SyncedChoreography(Transform target, int rounds)
            : base("Synced Choreography", EProcessStepPriorityMethod.First, 0, EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            _target = target;
            _rounds = rounds;
        }

        /// <summary>Registers one with ProcessControl and starts it: its relay, or null when it's refused.</summary>
        public static ProcessRelay Start(Transform target, int rounds = 3)
        {
            var result = ProcessControl.Register(new SyncedChoreography(target, rounds)).With(SequenceDataPacket.Default()).Run();
            if (!result) ForgeLog.Warning($"ProcessControl refused the choreography: {result.Refusal}.");
            return result.Relay;
        }

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            _data = regData as SequenceDataPacket ?? new SequenceDataPacket(regData);
            _done = 0;

            Vector3 size = _target.localScale;
            _runner = TaskSequenceBuilder.Create("Synced Choreography")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Grow")
                    .Task(SyncSeq.ScaleTo(_target, size * 1.5f, 0.5f)))
                .Stage(s => s
                    .WithName("Hold")
                    .SyncDelay(0.3f))
                .Stage(s => s
                    .WithName("Shrink")
                    .Task(SyncSeq.ScaleTo(_target, size, 0.5f)))
                .Stage(s => s
                    .WithName("Rest")
                    .SyncDelay(0.3f))
                .BuildSyncRunner();

            // Each time round, with whether it got there; an interrupt says it didn't
            _runner.OnComplete = (data, completed) =>
            {
                if (completed) ForgeLog.Info($"Round {++_done} of {_rounds}.");
            };
        }

        public override void WhenUpdate()
        {
            // Done once its rounds are up, or its target is gone. A step of its clock, the packet's or the game's
            if (!_target || _done >= _rounds || _runner.Step(_data)) Relay.Terminate();
        }

        public override void WhenTerminate()
        {
            // Ended part way, it cleans the stage it was in
            _runner?.Interrupt(_data);
            base.WhenTerminate();
        }
    }
}
