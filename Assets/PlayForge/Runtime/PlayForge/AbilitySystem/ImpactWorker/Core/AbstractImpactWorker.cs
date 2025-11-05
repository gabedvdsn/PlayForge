using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractImpactWorker
    {
        [Header("Impact Worker")]
        
        public bool AcceptUnworkableImpact = false;
        
        public abstract void InterpretImpact(AbilityImpactData impactData);

        public abstract bool ValidateWorkFor(AbilityImpactData impactData);
        public abstract Attribute GetTargetedAttribute();
        public virtual void SubscribeToCache(ImpactWorkerCache cache)
        {
            cache.AddWorker(GetTargetedAttribute(), this);
        }

        public virtual void UnsubscribeFromCache(ImpactWorkerCache cache)
        {
            cache.RemoveWorker(GetTargetedAttribute(), this);
        }
    }
}
