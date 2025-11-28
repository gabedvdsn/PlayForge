using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ThresholdDebuggerEffectWorker : TargetAttributeThresholdEffectWorker
    {
        protected override void OnThresholdMet(IAttributeImpactDerivation derivation)
        {
            Debug.Log($"Threshold is met: {derivation}");       
        }
    }
}
