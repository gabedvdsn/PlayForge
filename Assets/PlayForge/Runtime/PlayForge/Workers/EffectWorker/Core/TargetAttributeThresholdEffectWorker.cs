using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of an effect worker that acts on each hit of its effect where an attribute meets a threshold:
    /// <c>TargetAttribute</c> on its context's System, which for a hit is the dealer, the effect's caster, its Current,
    /// its Base or both as <c>Target</c> says, compared with <c>Threshold</c> by <c>Policy</c>. Derive from it and
    /// override <c>OnThresholdMet</c> for the actions it queues; <c>CanCompare</c> and <c>MeetsThreshold</c> can be
    /// overridden too.
    /// </summary>
    public abstract class TargetAttributeThresholdEffectWorker : AbstractEffectWorker
    {
        
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
            // A value it can't compare meets no threshold, Not Equal's no more than another's
            if (!CanCompare(value)) return null;
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

        /// <summary>Whether the value can be compared at all: one that can't meets no threshold.</summary>
        protected virtual bool CanCompare(AttributeValue attributeValue) => true;

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
