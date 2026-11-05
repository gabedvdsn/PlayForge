using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractStackingEffectContainer : AbstractEffectContainer
    {
        public override int InstantExecuteTicks => Stacks;

        public int Stacks;

        protected AbstractStackingEffectContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
            
        }
        
        public abstract void Stack(int amount);

        /// <summary>The stacks a save kept, then the clock (FEAT-9).</summary>
        protected internal override void RestoreClock(float total, float period, int stacks, IReadOnlyList<float> remaining, IReadOnlyList<float> untilTick)
        {
            Stacks = stacks;
            base.RestoreClock(total, period, stacks, remaining, untilTick);
        }

        // ReplaceValuesWith intentionally not overridden: the base adopts the new spec +
        // duration values while `Stacks` (accumulated state, not config) is preserved.
        // The old override copied Stacks onto the incoming throwaway container — a no-op.
    }

}
