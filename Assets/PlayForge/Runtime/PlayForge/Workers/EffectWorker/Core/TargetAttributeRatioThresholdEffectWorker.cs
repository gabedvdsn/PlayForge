using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class TargetAttributeRatioThresholdEffectWorker : TargetAttributeThresholdEffectWorker
    {
        protected override bool MeetsThreshold(AttributeValue attributeValue, Func<float, float, bool> policyFunc)
        {
            return Target switch
            {

                EAttributeTarget.Current => policyFunc(attributeValue.CurrentValue / attributeValue.BaseValue, Threshold.CurrentValue),
                EAttributeTarget.Base => policyFunc(attributeValue.BaseValue / attributeValue.BaseValue, Threshold.CurrentValue),
                EAttributeTarget.CurrentAndBase => policyFunc(attributeValue.CurrentValue / attributeValue.BaseValue, Threshold.CurrentValue) &&
                                                      policyFunc(attributeValue.BaseValue / attributeValue.BaseValue, Threshold.CurrentValue),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        
    }
}
