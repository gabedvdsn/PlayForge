using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A threshold on an attribute's ratio, its Current to its Base, as below half its health: the ratio against
    /// Threshold's Current, whatever Target says, as a ratio has no other side. Its Base target compared Base to Base,
    /// which is always one; and with no Base there's no ratio, so no threshold is met.
    /// </summary>
    public abstract class TargetAttributeRatioThresholdEffectWorker : TargetAttributeThresholdEffectWorker
    {
        protected override bool CanCompare(AttributeValue attributeValue) => !Mathf.Approximately(attributeValue.BaseValue, 0f);

        protected override bool MeetsThreshold(AttributeValue attributeValue, Func<float, float, bool> policyFunc) =>
            policyFunc(attributeValue.CurrentValue / attributeValue.BaseValue, Threshold.CurrentValue);
    }
}
