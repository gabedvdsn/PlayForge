namespace FarEmerald.PlayForge
{
    public class IncrementalStackableGameplayShelfContainer : NonStackingGameplayEffectShelfContainer
    {
        private int stacks;
        
        protected IncrementalStackableGameplayShelfContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
            Spec.Base.ApplyDurationSpecifications(this);
        }

        public new static IncrementalStackableGameplayShelfContainer Generate(GameplayEffectSpec spec, bool ongoing)
        {
            return new IncrementalStackableGameplayShelfContainer(spec, ongoing);
        }

        public override void TickPeriodic(float deltaTime, out int executeTicks)
        {
            timeUntilPeriodTick -= deltaTime;
            if (timeUntilPeriodTick <= 0f)
            {
                timeUntilPeriodTick += periodDuration;
                executeTicks = stacks;
            }
            else executeTicks = 0;
        }

        public override void Stack()
        {
            stacks += 1;
        }
    }
}
