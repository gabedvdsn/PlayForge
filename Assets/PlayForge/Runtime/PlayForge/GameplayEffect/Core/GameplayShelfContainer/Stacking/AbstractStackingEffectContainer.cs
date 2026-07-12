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

        // ReplaceValuesWith intentionally not overridden: the base adopts the new spec +
        // duration values while `Stacks` (accumulated state, not config) is preserved.
        // The old override copied Stacks onto the incoming throwaway container — a no-op.
    }

}
