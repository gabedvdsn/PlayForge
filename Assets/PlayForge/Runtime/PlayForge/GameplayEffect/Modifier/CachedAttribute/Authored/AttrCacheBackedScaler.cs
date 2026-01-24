using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cached scaler backed by an attribute value.
    /// Registers the attribute dependency for cache invalidation.
    /// </summary>
    public class AttrCacheBackedScaler : AbstractCachedScaler
    {
        [Tooltip("The attribute to read for scaling")]
        public Attribute CaptureAttribute;
        
        [Tooltip("Use base or current attribute value")]
        public EEffectImpactTargetLimited ScalingPolicy = EEffectImpactTargetLimited.Current;
        
        [Tooltip("Minimum expected attribute value (maps to 0 on curve)")]
        public float AttributeMin = 0f;
        
        [Tooltip("Maximum expected attribute value (maps to 1 on curve)")]
        public float AttributeMax = 100f;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            // No special initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (CaptureAttribute == null) return 0f;
            
            if (!spec.GetSource().FindAttributeSystem(out var attrSystem) || 
                !attrSystem.TryGetAttributeValue(CaptureAttribute, out AttributeValue attributeValue))
            {
                return 0f;
            }
            
            float rawValue = ScalingPolicy switch
            {
                EEffectImpactTargetLimited.Current => attributeValue.CurrentValue,
                EEffectImpactTargetLimited.Base => attributeValue.BaseValue,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            // Normalize to 0-1 range for curve evaluation
            float normalized = Mathf.InverseLerp(AttributeMin, AttributeMax, rawValue);
            return EvaluateAtRelativeLevel(Mathf.Clamp01(normalized));
        }
        
        public override void Regulate(Attribute attribute, AttributeModificationRule rules)
        {
            if (CaptureAttribute != null && attribute != null)
            {
                rules.RegisterRelation(CaptureAttribute, attribute);
            }
        }
        public override void Evaluate(CachedAttributeValue value)
        {
            
        }
    }
}