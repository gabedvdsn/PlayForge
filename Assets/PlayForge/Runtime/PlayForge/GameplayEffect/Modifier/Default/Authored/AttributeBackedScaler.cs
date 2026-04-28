using System;
using UnityEngine;
using UnityEngine.Serialization;

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
        public EAttributeTargetBinary CaptureWhat = EAttributeTargetBinary.Current;
        
        public override void Initialize(IAttributeImpactDerivation deriv)
        {
            if (CaptureWhen != ECaptureAttributeWhen.OnCreation) return;
            if (CaptureAttribute is null) return;
                
            switch (CaptureFrom)
            {
                case ESourceTarget.Source:
                    if (!deriv.GetSource().FindAttributeSystem(out var sourceAttr) || 
                        !sourceAttr.TryGetAttributeValue(CaptureAttribute, out AttributeValue sourceValue)) 
                        break;
                    deriv.GetSourcedCapturedAttributes()[this] = sourceValue;
                    break;
                    
                case ESourceTarget.Target:
                    if (!deriv.GetTarget().FindAttributeSystem(out var targetAttr) || 
                        !targetAttr.TryGetAttributeValue(CaptureAttribute, out AttributeValue targetValue)) 
                        break;
                    deriv.GetSourcedCapturedAttributes()[this] = targetValue;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public override float Evaluate(IAttributeImpactDerivation deriv)
        {
            float attributeValue = GetAttributeValue(deriv);

            return ApplyBehaviourEvaluation(deriv, attributeValue);
        }
        
        private float GetAttributeValue(IAttributeImpactDerivation deriv)
        {
            // Use captured value if captured on creation
            if (CaptureWhen == ECaptureAttributeWhen.OnCreation)
            {
                if (deriv?.GetSourcedCapturedAttributes().TryGetValue(this, out var capturedValue) ?? false)
                {
                    return CaptureWhat switch
                    {
                        EAttributeTargetBinary.Current => capturedValue?.CurrentValue ?? 0f,
                        EAttributeTargetBinary.Base => capturedValue?.BaseValue ?? 0f,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }

                Debug.LogWarning($"[PlayForge] Attribute backed scaler operation failed: impact derivation is null.");
                return 0f;
            }
            
            // Get live attribute value
            var entity = CaptureFrom == ESourceTarget.Source 
                ? deriv.GetSource() 
                : deriv.GetTarget();
            
            if (!entity.FindAttributeSystem(out var attrSystem) || 
                !attrSystem.TryGetAttributeValue(CaptureAttribute, out AttributeValue attrValue))
            {
                return 0f;
            }
            
            return CaptureWhat switch
            {
                EAttributeTargetBinary.Current => attrValue.CurrentValue,
                EAttributeTargetBinary.Base => attrValue.BaseValue,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public override bool UseScalingOptions()
        {
            return false;
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