using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractEffectWorker
    {
        public abstract void OnEffectApplication(IAttributeImpactDerivation derivation);
        public abstract void OnEffectTick(IAttributeImpactDerivation derivation);
        public abstract void OnEffectRemoval(IAttributeImpactDerivation derivation);
        public abstract void OnEffectImpact(AbilityImpactData impactData);
    }
}
