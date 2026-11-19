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
