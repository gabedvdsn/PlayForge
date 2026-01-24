using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractStackingEffectContainer : AbstractEffectContainer
    {
        public override int InstantExecuteTicks => Stacks;

        public int Stacks;
        
        protected AbstractStackingEffectContainer(GameplayEffectSpec spec, bool ongoing) : base(spec, ongoing)
        { }
        
        public abstract void Stack(int amount);

        public override void ReplaceValuesWith(AbstractEffectContainer container)
        {
            if (container is AbstractStackingEffectContainer _container)
            {
                _container.Stacks = Stacks;
            }
            
            base.ReplaceValuesWith(container);
        }
    }

}
