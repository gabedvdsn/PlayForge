using UnityEngine;

namespace FarEmerald.PlayForge.Samples.ProcessGroups
{
    /// <summary>
    /// A buff that lasts a while, then ends. It's a process of its own, stepped each frame with the Synchronous lifecycle,
    /// and it neither knows nor cares that it's in a group: the group sees it end and lets it go.
    /// </summary>
    public class TimedBuff : LazyRuntimeProcess
    {
        /// <summary>How long it has left, in seconds.</summary>
        public float Remaining { get; private set; }

        public TimedBuff(string buffName, float duration)
            : base(buffName, EProcessStepPriorityMethod.First, 0, EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            Remaining = duration;
        }

        public override void WhenUpdate()
        {
            // A paused member isn't stepped, so a group's PauseAll holds this countdown where it is
            Remaining -= ForgeTime.DeltaTime;
            if (Remaining <= 0f) Relay.Terminate();
        }

        public override void WhenTerminate()
        {
            ForgeLog.Info(Remaining > 0f
                ? $"{name} ended with {Remaining:0.0} s left"
                : $"{name} ran out");
            base.WhenTerminate();
        }
    }

    /// <summary>
    /// A summon that stays until something ends it: its group, a cap that refuses it, or the scene. It reads its name
    /// from the data packet it was registered with, which is its own rather than its group's.
    /// </summary>
    public class Summon : LazyRuntimeProcess
    {
        /// <summary>Where a summon finds the name its caller gave it.</summary>
        public static readonly Tag NAME = Tag.GenerateAsUnique("ProcessGroupExamples.SummonName");

        /// <summary>How long it has been out, in seconds.</summary>
        public float Age { get; private set; }

        public Summon()
            : base("Summon", EProcessStepPriorityMethod.First, 0, EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
        }

        public string Called => regData?.GetPrimary(NAME, name) ?? name;

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            ForgeLog.Info($"{Called} is summoned");
        }

        public override void WhenUpdate() => Age += ForgeTime.DeltaTime;

        public override void WhenTerminate()
        {
            ForgeLog.Info($"{Called} is gone after {Age:0.0} s");
            base.WhenTerminate();
        }
    }

    /// <summary>
    /// A hazard that pulses a few times, then ends. Every hazard registers into one group with the group's data packet,
    /// so the total each pulse adds to is the same number for all of them.
    /// </summary>
    public class HazardPulse : LazyRuntimeProcess
    {
        /// <summary>The damage every hazard in the group has dealt, kept in the data packet they share.</summary>
        public static readonly Tag TOTAL = Tag.GenerateAsUnique("ProcessGroupExamples.HazardTotal");

        private readonly float interval;
        private readonly float damage;
        private int pulsesLeft;
        private float untilPulse;

        public HazardPulse(string hazardName, float interval, float damage, int pulses)
            : base(hazardName, EProcessStepPriorityMethod.First, 0, EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            this.interval = interval;
            this.damage = damage;
            pulsesLeft = pulses;
            untilPulse = interval;
        }

        public override void WhenUpdate()
        {
            untilPulse -= ForgeTime.DeltaTime;
            if (untilPulse > 0f) return;
            untilPulse += interval;

            // regData is the group's own packet, as the hazard joined with its shared data: a write here is read by
            // every other hazard in the group
            float total = regData.GetPrimary(TOTAL, 0f) + damage;
            regData.SetPrimary(TOTAL, total);
            ForgeLog.Info($"{name} pulses for {damage:0.#}; the hazards have dealt {total:0.#} between them");

            if (--pulsesLeft <= 0) Relay.Terminate();
        }
    }

    /// <summary>
    /// A unit that keeps its place in a formation behind a leader. Its place is set from outside, by whoever forms the
    /// squad: the group lets that be done to every member at once.
    /// </summary>
    public class SquadMember : LazyRuntimeProcess
    {
        private readonly Transform unit;
        private readonly Transform leader;
        private readonly float speed;

        /// <summary>Its place, relative to the leader and turned with it.</summary>
        public Vector3 Offset { get; set; }

        public SquadMember(Transform unit, Transform leader, float speed)
            : base(unit ? $"Squad: {unit.name}" : "Squad member", EProcessStepPriorityMethod.First, 0,
                EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            this.unit = unit;
            this.leader = leader;
            this.speed = speed;
        }

        public override void WhenUpdate()
        {
            // A unit destroyed in the scene ends its process, which takes it out of the squad
            if (!unit || !leader)
            {
                Relay.Terminate();
                return;
            }

            var place = leader.position + leader.TransformDirection(Offset);
            unit.position = Vector3.MoveTowards(unit.position, place, speed * ForgeTime.DeltaTime);
        }
    }
}
