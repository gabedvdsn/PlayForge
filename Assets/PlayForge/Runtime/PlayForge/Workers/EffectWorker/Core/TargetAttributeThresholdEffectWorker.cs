using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class TargetAttributeThresholdEffectWorker : AbstractEffectWorker
    {
        [Header("Focused Effect Worker")]
        
        public Attribute TargetAttribute;
        public AttributeValue Threshold;
        
        [Space]
        
        public EAttributeTarget Target;
        public EComparisonOperator Policy;
        
        public override IEnumerable<IRootAction> OnEffectImpact(EffectWorkerContext ctx)
        {
            return PerformThresholdWork(ctx);
        }

        protected IEnumerable<IRootAction> PerformThresholdWork(EffectWorkerContext ctx)
        {
            if (!ctx.System.TryGetAttributeValue(TargetAttribute, out var value)) return null;
            switch (Policy)
            {

                case EComparisonOperator.GreaterThan:
                    if (!MeetsThreshold(value, (v, threshold) => v > threshold)) return null;
                    break;
                case EComparisonOperator.LessThan:
                    if (!MeetsThreshold(value, (v, threshold) => v < threshold)) return null;
                    break;
                case EComparisonOperator.GreaterOrEqual:
                    if (!MeetsThreshold(value, (v, threshold) => v >= threshold)) return null;
                    break;
                case EComparisonOperator.LessOrEqual:
                    if (!MeetsThreshold(value, (v, threshold) => v <= threshold)) return null;
                    break;
                case EComparisonOperator.Equal:
                    if (!MeetsThreshold(value, Mathf.Approximately)) return null;
                    break;
                case EComparisonOperator.NotEqual:
                    if (MeetsThreshold(value, Mathf.Approximately)) return null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            return OnThresholdMet(ctx);
        }

        protected virtual bool MeetsThreshold(AttributeValue attributeValue, Func<float, float, bool> policyFunc)
        {
            return Target switch
            {
                EAttributeTarget.Current => policyFunc(attributeValue.CurrentValue, Threshold.CurrentValue),
                EAttributeTarget.Base => policyFunc(attributeValue.BaseValue, Threshold.BaseValue),
                EAttributeTarget.CurrentAndBase => policyFunc(attributeValue.CurrentValue, Threshold.CurrentValue) && policyFunc(attributeValue.BaseValue, Threshold.BaseValue),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        protected abstract IEnumerable<IRootAction> OnThresholdMet(EffectWorkerContext ctx);
    }

}
