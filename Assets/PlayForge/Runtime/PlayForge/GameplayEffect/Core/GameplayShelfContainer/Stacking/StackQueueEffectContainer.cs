namespace FarEmerald.PlayForge
{
    public class StackQueueEffectContainer : AbstractStackingEffectContainer
    {
        protected float durationRemaining;
        protected float timeUntilPeriodTick;
        
        protected StackQueueEffectContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
        }

        public static StackQueueEffectContainer Generate(GameplayEffectSpec spec, bool ongoing)
        {
            return new StackQueueEffectContainer(spec, ongoing);
        }
        
        public override float DurationRemaining => durationRemaining + TotalDuration * (Stacks - 1);
        public override float NextDurationRemaining => durationRemaining;
        public override float TimeUntilPeriodTick => timeUntilPeriodTick;
        public override void SetDurationRemaining(float duration)
        {
            durationRemaining = duration;
        }
        public override void SetTimeUntilPeriodTick(float duration)
        {
            timeUntilPeriodTick = duration;
        }
        public override void UpdateTimeRemaining(float deltaTime)
        {
            durationRemaining -= deltaTime;
            if (durationRemaining <= 0)
            {
                Stacks -= 1;
                Refresh();
            }
        }
        public override void TickPeriodic(float deltaTime, out int executeTicks)
        {
            timeUntilPeriodTick -= deltaTime;
            if (timeUntilPeriodTick <= 0f)
            {
                timeUntilPeriodTick += periodDuration;
                executeTicks = Stacks;
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
        public override void Stack(int amount)
        {
            Stacks += 1;
            Refresh();
        }
        
         
    }
}
