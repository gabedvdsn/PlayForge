using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractAbilityTask : AbstractAbilityRelatedTask
    {
        public virtual string Description => null;
    }
}
