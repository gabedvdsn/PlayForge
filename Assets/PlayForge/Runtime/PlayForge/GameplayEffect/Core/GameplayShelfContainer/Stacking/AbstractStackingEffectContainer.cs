using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of the containers that count stacks, the kind an effect lives in when its Re-application isn't Add
    /// Another, as its Stacking says: its <c>Stacks</c>, and <c>Stack</c>, which adds to them, each kind keeping its
    /// clock its own way. An Effect Stacks number reads its count, and Tick On Application executes once for each
    /// stack, unless a kind says otherwise.
    /// </summary>
    public abstract class AbstractStackingEffectContainer : AbstractEffectContainer
    {
        public override int InstantExecuteTicks => Stacks;

        public int Stacks;

        protected AbstractStackingEffectContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        {
            
        }
        
        public abstract void Stack(int amount);

        /// <summary>The stacks a save kept, then the clock.</summary>
        protected internal override void RestoreClock(float total, float period, int stacks, IReadOnlyList<double> remaining, IReadOnlyList<double> untilTick)
        {
            Stacks = stacks;
            base.RestoreClock(total, period, stacks, remaining, untilTick);
        }

        // ReplaceValuesWith intentionally not overridden: the base adopts the new spec +
        // duration values while `Stacks` (accumulated state, not config) is preserved.
        // The old override copied Stacks onto the incoming throwaway container — a no-op.
    }

}
