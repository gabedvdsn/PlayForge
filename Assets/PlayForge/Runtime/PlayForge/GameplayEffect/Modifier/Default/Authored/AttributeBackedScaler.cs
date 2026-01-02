using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Scales based on an attribute value from source or target.
    /// The attribute value is used as input to the scaling curve.
    /// </summary>
    public class AttributeBackedScaler : AbstractScaler
    {
        [Tooltip("Which attribute to read")]
        public Attribute CaptureAttribute;
        
        [Tooltip("Capture from source or target entity")]
        public ESourceTarget CaptureFrom = ESourceTarget.Source;
        
        [Tooltip("When to capture the attribute value")]
        public ECaptureAttributeWhen CaptureWhen = ECaptureAttributeWhen.OnApplication;
        
        [Tooltip("Use base or current attribute value")]
        public EEffectImpactTargetLimited ScalingPolicy = EEffectImpactTargetLimited.Current;
        
        [Tooltip("Minimum expected attribute value (maps to 0 on curve)")]
        public float AttributeMin = 0f;
        
        [Tooltip("Maximum expected attribute value (maps to 1 on curve)")]
        public float AttributeMax = 100f;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            if (CaptureWhen != ECaptureAttributeWhen.OnCreation) return;
            if (CaptureAttribute == null) return;
                
            switch (CaptureFrom)
            {
                case ESourceTarget.Source:
                    if (!spec.GetSource().FindAttributeSystem(out var sourceAttr) || 
                        !sourceAttr.TryGetAttributeValue(CaptureAttribute, out AttributeValue sourceValue)) 
                        break;
                    spec.GetSourcedCapturedAttributes()[this] = sourceValue;
                    break;
                    
                case ESourceTarget.Target:
                    if (!spec.GetTarget().FindAttributeSystem(out var targetAttr) || 
                        !targetAttr.TryGetAttributeValue(CaptureAttribute, out AttributeValue targetValue)) 
                        break;
                    spec.GetSourcedCapturedAttributes()[this] = targetValue;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            float attributeValue = GetAttributeValue(spec);
            
            // Normalize attribute value to 0-1 range for curve evaluation
            float normalizedValue = Mathf.InverseLerp(AttributeMin, AttributeMax, attributeValue);
            normalizedValue = Mathf.Clamp01(normalizedValue);
            
            return EvaluateAtRelativeLevel(normalizedValue);
        }
        
        private float GetAttributeValue(IAttributeImpactDerivation spec)
        {
            // Use captured value if captured on creation
            if (CaptureWhen == ECaptureAttributeWhen.OnCreation)
            {
                if (spec.GetSourcedCapturedAttributes().TryGetValue(this, out var capturedValue))
                {
                    return ScalingPolicy switch
                    {
                        EEffectImpactTargetLimited.Current => capturedValue?.CurrentValue ?? 0f,
                        EEffectImpactTargetLimited.Base => capturedValue?.BaseValue ?? 0f,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }
                return 0f;
            }
            
            // Get live attribute value
            var entity = CaptureFrom == ESourceTarget.Source 
                ? spec.GetSource() 
                : spec.GetTarget();
            
            if (!entity.FindAttributeSystem(out var attrSystem) || 
                !attrSystem.TryGetAttributeValue(CaptureAttribute, out AttributeValue attrValue))
            {
                return 0f;
            }
            
            return ScalingPolicy switch
            {
                EEffectImpactTargetLimited.Current => attrValue.CurrentValue,
                EEffectImpactTargetLimited.Base => attrValue.BaseValue,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
    
    public enum ESourceTarget
    {
        [Tooltip("Use the target entity's attribute")]
        Target,
        
        [Tooltip("Use the source entity's attribute")]
        Source
    }
    
    public enum ECaptureAttributeWhen
    {
        [Tooltip("Capture once when effect is created (snapshot)")]
        OnCreation,
        
        [Tooltip("Read live value each time effect is applied")]
        OnApplication
    }
}