using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class IndependentDurationsContainer : AbstractStackingEffectContainer
    {
        protected IndependentDurationsContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
        }

        public static IndependentDurationsContainer Generate(GameplayEffectSpec spec, bool ongoing)
        {
            return new IndependentDurationsContainer(spec, ongoing);
        }

        // Made here and not in the constructor: the base constructor applies the duration spec, which reads
        // the packets and can add to them
        private readonly List<StackableContainerPacket> Packets = new();

        // The effect lasts as long as its longest stack, and is over when none are left
        public override float DurationRemaining
        {
            get
            {
                double longest = 0d;
                foreach (var packet in Packets) longest = Math.Max(longest, packet.DurationRemaining);
                return (float)longest;
            }
        }

        public override float NextDurationRemaining
        {
            get
            {
                double soonest = float.MaxValue;
                foreach (var packet in Packets) soonest = Math.Min(soonest, packet.DurationRemaining);
                return (float)soonest;
            }
        }

        public override float TimeUntilPeriodTick
        {
            get
            {
                double soonest = float.MaxValue;
                foreach (var packet in Packets) soonest = Math.Min(soonest, packet.TimeUntilPeriodTick);
                return (float)soonest;
            }
        }

        public override int InstantExecuteTicks => 1;

        private sealed class StackableContainerPacket
        {
            // Doubles for the reason NonStackingEffectContainer's timers are
            public double DurationRemaining;
            public double TimeUntilPeriodTick;

            public StackableContainerPacket(float totalDuration, float periodDuration)
            {
                DurationRemaining = totalDuration;
                TimeUntilPeriodTick = periodDuration;
            }

            public void UpdateTimeRemaining(float deltaTime)
            {
                DurationRemaining -= deltaTime;
            }

            public void TickPeriodic(float deltaTime, float periodDuration, out bool executeTick)
            {
                TimeUntilPeriodTick -= deltaTime;
                if (TickIsDue(TimeUntilPeriodTick, periodDuration, DurationRemaining - deltaTime))
                {
                    TimeUntilPeriodTick += periodDuration;
                    executeTick = true;
                }
                else
                {
                    executeTick = false;
                }
            }

            public void Refresh(float totalDuration, float periodDuration)
            {
                DurationRemaining = totalDuration;
                TimeUntilPeriodTick = periodDuration;
            }
        }

        public override void SetDurationRemaining(float duration)
        {
            foreach (var packet in Packets) packet.DurationRemaining = Math.Min(packet.DurationRemaining, duration);
        }

        public override void SetTimeUntilPeriodTick(float duration)
        {
            foreach (var packet in Packets) packet.TimeUntilPeriodTick = Math.Min(packet.TimeUntilPeriodTick, duration);
        }

        public override void UpdateTimeRemaining(float deltaTime)
        {
            for (int i = Packets.Count - 1; i >= 0; i--)
            {
                Packets[i].UpdateTimeRemaining(deltaTime);
                if (Packets[i].DurationRemaining > 0d) continue;

                Packets.RemoveAt(i);
                Stacks -= 1;
            }
        }

        public override void TickPeriodic(float deltaTime, out int executeTicks)
        {
            executeTicks = 0;
            foreach (var packet in Packets)
            {
                packet.TickPeriodic(deltaTime, periodDuration, out bool execute);
                if (execute) executeTicks += 1;
            }
        }
        public override void Refresh()
        {
            if (Packets.Count > 0) Packets[0].Refresh(totalDuration, periodDuration);
        }
        public override void Extend(float duration)
        {
            Stack(1);
        }
        public override void Stack(int amount)
        {
            for (int _ = 0; _ < amount; _++)
            {
                Packets.Add(new StackableContainerPacket(totalDuration, periodDuration));
                Stacks += 1;
            }
        }

        /// <summary>Each stack's own time left and to its tick (FEAT-9).</summary>
        protected internal override void CaptureClock(List<double> remaining, List<double> untilTick)
        {
            // In full, as the stacks keep them (GAS-61)
            foreach (var packet in Packets)
            {
                remaining.Add(packet.DurationRemaining);
                untilTick.Add(packet.TimeUntilPeriodTick);
            }
        }

        /// <summary>A stack for each time a save kept, each with its own (FEAT-9).</summary>
        protected internal override void RestoreClock(float total, float period, int stacks, IReadOnlyList<double> remaining, IReadOnlyList<double> untilTick)
        {
            base.RestoreClock(total, period, stacks, remaining, untilTick);

            // As RestoreClock keeps them, an infinite effect's time and a period that never ticks are the spec's
            var duration = Spec.Base.DurationSpecification;
            bool durational = duration.DurationPolicy == EEffectDurationPolicy.Durational;
            bool ticks = duration.EnablePeriodicTicks;

            Packets.Clear();
            for (int i = 0; i < remaining.Count; i++)
            {
                var packet = new StackableContainerPacket(totalDuration, periodDuration);
                if (durational) packet.DurationRemaining = remaining[i];
                if (ticks && i < untilTick.Count) packet.TimeUntilPeriodTick = untilTick[i];
                Packets.Add(packet);
            }
            Stacks = Packets.Count;
        }
    }
}
