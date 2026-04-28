using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class CachedAttributeScaler : AbstractCachedScaler
    {
        [Tooltip("Which attribute to read")]
        public Attribute CaptureAttribute;
        
        [Tooltip("Use base or current attribute value")]
        public EAttributeTargetBinary CaptureWhat = EAttributeTargetBinary.Current;
        
        public override float Evaluate(IAttributeImpactDerivation deriv)
        {
            float attributeValue = CaptureAttribute is null ? 0f : GetAttributeValueTarget(GetAttributeValue(deriv));
            return ApplyBehaviourEvaluation(deriv, attributeValue);
        }
        
        public override void RegulateContactWith(IAttribute related, AttributeRegulationCache rules)
        {
            if (CaptureAttribute != null && related != null)
            {
                rules.RegisterRelation(CaptureAttribute, related);
            }
        }
        
        public override AttributeValue EvaluateInitialValue(SourceAttributeImpact deriv, AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return cache.TryGetValue(CaptureAttribute, out var value) 
                ? value.RootValue 
                : new AttributeValue(0f, 0f);
        }

        protected float GetAttributeValueTarget(AttributeValue value)
        {
            return CaptureWhat switch
            {
                EAttributeTargetBinary.Current => value.CurrentValue,
                EAttributeTargetBinary.Base => value.BaseValue,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        protected AttributeValue GetAttributeValue(IAttributeImpactDerivation deriv)
        {
            if (!deriv.GetSource().FindAttributeSystem(out var attrSystem) || 
                !attrSystem.TryGetAttributeValue(CaptureAttribute, out AttributeValue attrValue))
            {
                return new AttributeValue(0f, 0f);
            }

            return attrValue;
        }

        public override bool UseScalingOptions()
        {
            return false;
        }

    }
}
