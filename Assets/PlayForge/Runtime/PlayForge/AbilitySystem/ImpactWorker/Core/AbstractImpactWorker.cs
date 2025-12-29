using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractImpactWorker
    {
        [Header("Impact Worker")]
        
        public readonly bool AcceptUnworkableImpact = false;
        
        public abstract void Activate(AbilityImpactData impactData);

        public abstract bool PreValidateWorkFor(AbilityImpactData impactData);
        public abstract bool ValidateWorkFor(AbilityImpactData impactData);

        public abstract void SubscribeToCache(ImpactWorkerCache cache);

        public abstract void UnsubscribeFromCache(ImpactWorkerCache cache);
    }
}
