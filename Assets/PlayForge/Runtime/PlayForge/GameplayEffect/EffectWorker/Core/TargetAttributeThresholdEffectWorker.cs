using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class TargetAttributeThresholdEffectWorker : AbstractEffectWorker
    {
        [Header("Focused Effect Worker")]
        
        public Attribute TargetAttribute;
        public AttributeValue Threshold;
        
        [Space]
        
        public EEffectImpactTarget Target;
        public EComparisonOperator Policy;
        
        public override void OnEffectApplication(IAttributeImpactDerivation derivation)
        {
            // PerformThresholdWork(derivation);
        }
        public override void OnEffectTick(IAttributeImpactDerivation derivation)
        {
            // PerformThresholdWork(derivation);
        }
        public override void OnEffectRemoval(IAttributeImpactDerivation derivation)
        {
            // PerformThresholdWork(derivation);
        }
        public override void OnEffectImpact(AbilityImpactData impactData)
        {
            PerformThresholdWork(impactData.SourcedModifier.BaseDerivation);
        }

        protected void PerformThresholdWork(IAttributeImpactDerivation derivation)
        {
            if (!derivation.GetTarget().FindAttributeSystem(out var attrSystem) || !attrSystem.TryGetAttributeValue(TargetAttribute, out AttributeValue value)) return;
            switch (Policy)
            {

                case EComparisonOperator.GreaterThan:
                    if (!MeetsThreshold(value, (v, threshold) => v > threshold)) return;
                    break;
                case EComparisonOperator.LessThan:
                    if (!MeetsThreshold(value, (v, threshold) => v < threshold)) return;
                    break;
                case EComparisonOperator.GreaterOrEqual:
                    if (!MeetsThreshold(value, (v, threshold) => v >= threshold)) return;
                    break;
                case EComparisonOperator.LessOrEqual:
                    if (!MeetsThreshold(value, (v, threshold) => v <= threshold)) return;
                    break;
                case EComparisonOperator.Equal:
                    if (!MeetsThreshold(value, Mathf.Approximately)) return;
                    break;
                case EComparisonOperator.NotEqual:
                    if (MeetsThreshold(value, Mathf.Approximately)) return;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            OnThresholdMet(derivation);
        }

        protected virtual bool MeetsThreshold(AttributeValue attributeValue, Func<float, float, bool> policyFunc)
        {
            return Target switch
            {
                EEffectImpactTarget.Current => policyFunc(attributeValue.CurrentValue, Threshold.CurrentValue),
                EEffectImpactTarget.Base => policyFunc(attributeValue.BaseValue, Threshold.BaseValue),
                EEffectImpactTarget.CurrentAndBase => policyFunc(attributeValue.CurrentValue, Threshold.CurrentValue) && policyFunc(attributeValue.BaseValue, Threshold.BaseValue),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        protected abstract void OnThresholdMet(IAttributeImpactDerivation derivation);
    }

    public enum EComparisonOperator
    {
        LessThan,
        LessOrEqual,
        Equal,
        NotEqual,
        GreaterOrEqual,
        GreaterThan
    }
}
