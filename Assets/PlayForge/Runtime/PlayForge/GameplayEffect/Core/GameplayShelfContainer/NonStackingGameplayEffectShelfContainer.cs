using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class NonStackingGameplayEffectShelfContainer : AbstractGameplayEffectShelfContainer
    {
        protected float durationRemaining;
        protected float timeUntilPeriodTick;
        
        public override float DurationRemaining => durationRemaining;
        public override float TimeUntilPeriodTick => timeUntilPeriodTick;
        
        private SourcedModifiedAttributeValue TrackedImpact;
        
        protected NonStackingGameplayEffectShelfContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
        }

        public static NonStackingGameplayEffectShelfContainer Generate(GameplayEffectSpec spec, bool ongoing)
        {
            return new NonStackingGameplayEffectShelfContainer(spec, ongoing);
        }

        public override void SetDurationRemaining(float duration) => durationRemaining = duration;
        public override void SetTimeUntilPeriodTick(float duration) => timeUntilPeriodTick = duration;

        public override void UpdateTimeRemaining(float deltaTime) => durationRemaining -= deltaTime;

        public override void TickPeriodic(float deltaTime, out int executeTicks)
        {
            timeUntilPeriodTick -= deltaTime;
            if (timeUntilPeriodTick <= 0f)
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
        
        public override void Stack()
        {
            // Does nothing
        }
    }
}
