using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Scales based on an attribute value from source or target.
    /// The attribute value is used as input to the scaling curve.
    /// </summary>
    [NumberKind("× Attribute", Reads = NumberReads.Caster)]
    public class AttributeBackedScaler : AbstractScaler
    {
        [Tooltip("Which attribute to read")]
        public Attribute CaptureAttribute;
        
        [Tooltip("Capture from source or target entity")]
        public ESourceTarget CaptureFrom = ESourceTarget.Source;
        
        [Tooltip("When to capture the attribute value")]
        public ECaptureAttributeWhen CaptureWhen = ECaptureAttributeWhen.OnApplication;
        
        [Tooltip("What of it it reads: its current value, its base, what's missing of it (base − current), or its share " +
                 "of its base (current ÷ base) (TW-9)")]
        public EAttributeTargetBinary CaptureWhat = EAttributeTargetBinary.Current;

        [Tooltip("Which retention groups' parts it reads: all of them, the whole value, unless it says (TW-3)")]
        public RetentionGroups Groups = new();

        [Tooltip("How it reads the attribute: as its role says, a Bonus Share as 1 + x and a Resistance as 1 − x; as it " +
                 "is; as a multiplier, 1 + x; or as what's left, 1 − x (TW-1)")]
        public EAttributeRead ReadAs = EAttributeRead.ByRole;

        /// <summary>How a read of <paramref name="attribute"/> as <paramref name="read"/> reads it, the role resolved.</summary>
        public static EAttributeRead Resolve(EAttributeRead read, Attribute attribute)
        {
            if (read != EAttributeRead.ByRole) return read;
            return attribute == null ? EAttributeRead.AsIs : attribute.Role switch
            {
                EAttributeRole.BonusShare => EAttributeRead.AsMultiplier,
                EAttributeRole.Resistance => EAttributeRead.AsRemainder,
                _ => EAttributeRead.AsIs,
            };
        }

        private float Read(float value) => Resolve(ReadAs, CaptureAttribute) switch
        {
            EAttributeRead.AsMultiplier => 1f + value,
            EAttributeRead.AsRemainder => 1f - value,
            _ => value,
        };
        
        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            if (CaptureWhen != ECaptureAttributeWhen.OnCreation) return;
            if (CaptureAttribute is null) return;
                
            switch (CaptureFrom)
            {
                case ESourceTarget.Source:
                    if (deriv.GetOrigin()?.GetSource() is not { } source || !RetentionGroups.TryRead(Groups, source, CaptureAttribute, out var sourceValue)) 
                        break;
                    deriv.GetSourcedCapturedAttributes()[this] = sourceValue;
                    break;
                    
                case ESourceTarget.Target:
                    if (deriv.GetTarget() is not { } target || !RetentionGroups.TryRead(Groups, target, CaptureAttribute, out var targetValue)) 
                        break;
                    deriv.GetSourcedCapturedAttributes()[this] = targetValue;
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            float attributeValue = GetAttributeValue(deriv);

            return ApplyBehaviourEvaluation(deriv, attributeValue);
        }
        
        private float GetAttributeValue(IAttributeImpactCarrier deriv)
        {
            // Use captured value if captured on creation
            if (CaptureWhen == ECaptureAttributeWhen.OnCreation)
            {
                if (deriv?.GetSourcedCapturedAttributes().TryGetValue(this, out var capturedValue) ?? false)
                {
                    return Read(capturedValue.HasValue ? AttributeParts.Read(capturedValue.Value, CaptureWhat) : 0f);
                }

                Debug.LogWarning($"[PlayForge] Attribute backed scaler operation failed: impact derivation is null.");
                return 0f;
            }
            
            // Get live attribute value
            var entity = CaptureFrom == ESourceTarget.Source 
                ? deriv.GetOrigin()?.GetSource() 
                : deriv.GetTarget();
            
            // A context with no one on that side, as a number read where there's no target, reads 0 (FEAT-29)
            if (entity == null || CaptureAttribute is null || !RetentionGroups.TryRead(Groups, entity, CaptureAttribute, out var attrValue))
            {
                return 0f;
            }
            
            return Read(AttributeParts.Read(attrValue, CaptureWhat));
        }

        public override bool UseScalingOptions()
        {
            return false;
        }

        // It reads the attribute, through its behaviours (ED-51)
        public override EScalerParts Parts => EScalerParts.Behaviours;
    }
    
    /// <summary>How × Attribute reads its attribute (TW-1).</summary>
    public enum EAttributeRead
    {
        /// <summary>As the attribute's role says: a Bonus Share as a multiplier, a Resistance as what's left, else as it is.</summary>
        ByRole,

        /// <summary>The value as it is.</summary>
        AsIs,

        /// <summary>As a multiplier: 1 + x.</summary>
        AsMultiplier,

        /// <summary>As what's left: 1 − x.</summary>
        AsRemainder,
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