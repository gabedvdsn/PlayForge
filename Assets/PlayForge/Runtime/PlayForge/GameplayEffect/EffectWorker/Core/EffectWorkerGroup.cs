using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(fileName = "EW_Group", menuName = "FESGAS/Effect/Worker/Group", order = 0)]
    public class EffectWorkerGroup : AbstractEffectWorker
    {
        public List<AbstractEffectWorker> EffectWorkers;
        
        public override void OnEffectApplication(IAttributeImpactDerivation derivation)
        {
            foreach (var worker in EffectWorkers) worker.OnEffectApplication(derivation);
        }
        public override void OnEffectTick(IAttributeImpactDerivation derivation)
        {
            foreach (var worker in EffectWorkers) worker.OnEffectTick(derivation);
        }
        public override void OnEffectRemoval(IAttributeImpactDerivation derivation)
        {
            foreach (var worker in EffectWorkers) worker.OnEffectRemoval(derivation);
        }
        public override void OnEffectImpact(AbilityImpactData impactData)
        {
            foreach (var worker in EffectWorkers) worker.OnEffectImpact(impactData);
        }
    }
}
