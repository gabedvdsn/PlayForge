using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The container of an application with no stacks, which Add Another makes for each application of its effect: its
    /// own clock, its ticks executing once each. Refresh restarts its time and its tick, and Extend adds to its time.
    /// </summary>
    public class NonStackingEffectContainer : AbstractEffectContainer
    {
        // Doubles because a float's rounding grows with its size and every frame adds to it: over five minutes
        // at 144 fps the duration drifted far enough from the ticks to end before the last one
        protected double durationRemaining;
        protected double timeUntilPeriodTick;
        
        public override float DurationRemaining => (float)durationRemaining;
        public override float NextDurationRemaining => (float)durationRemaining;
        public override int InstantExecuteTicks => 1;
        public override float TimeUntilPeriodTick => (float)timeUntilPeriodTick;
        
        private SourcedModifiedAttributeValue TrackedImpact;
        
        protected NonStackingEffectContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
        }

        public static NonStackingEffectContainer Generate(GameplayEffectSpec spec, bool ongoing)
        {
            return new NonStackingEffectContainer(spec, ongoing);
        }

        // Its timers in full, for a save
        protected internal override double ExactDurationRemaining => durationRemaining;
        protected internal override double ExactTimeUntilPeriodTick => timeUntilPeriodTick;
        protected internal override void SetExactDurationRemaining(double duration) => durationRemaining = duration;
        protected internal override void SetExactTimeUntilPeriodTick(double duration) => timeUntilPeriodTick = duration;

        public override void SetDurationRemaining(float duration) => durationRemaining = duration;
        public override void SetTimeUntilPeriodTick(float duration) => timeUntilPeriodTick = duration;

        public override void UpdateTimeRemaining(float deltaTime) => durationRemaining -= deltaTime;

        public override void TickPeriodic(float deltaTime, out int executeTicks)
        {
            timeUntilPeriodTick -= deltaTime;
            if (TickIsDue(timeUntilPeriodTick, periodDuration, durationRemaining - deltaTime))
            {
                timeUntilPeriodTick += periodDuration;
                executeTicks = 1;
            }
            else
            {
                executeTicks = 0;
            }
        }
        
        public override void Refresh()
        {
            durationRemaining = totalDuration;
            timeUntilPeriodTick = periodDuration;
        }

        public override void Extend(float duration)
        {
            totalDuration += duration;
            durationRemaining += duration;
        }
    }
}
