using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AttributeBackedMagnitudeModifier : AbstractMagnitudeModifier
    {
        public AnimationCurve Scaling;
        public EEffectImpactTargetLimited ScalingPolicy;
        
        [Space]
        
        public Attribute CaptureAttribute;
        public ESourceTarget CaptureFrom;
        public ECaptureAttributeWhen CaptureWhen;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            if (CaptureWhen != ECaptureAttributeWhen.OnCreation) return;
                
            switch (CaptureFrom)
            {
                case ESourceTarget.Source:
                    if (!spec.GetSource().FindAttributeSystem(out var attr) || !attr.TryGetAttributeValue(CaptureAttribute, out AttributeValue sourceAttributeValue)) break;
                    spec.GetSourcedCapturedAttributes()[this] = sourceAttributeValue;
                    break;
                case ESourceTarget.Target:
                    if (!spec.GetTarget().FindAttributeSystem(out var attr2) || !attr2.TryGetAttributeValue(CaptureAttribute, out AttributeValue targetAttributeValue)) break;
                    spec.GetSourcedCapturedAttributes()[this] = targetAttributeValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (CaptureWhen == ECaptureAttributeWhen.OnCreation)
            {
                return ScalingPolicy switch
                {
                    EEffectImpactTargetLimited.Current => Scaling.Evaluate(spec.GetSourcedCapturedAttributes()[this].GetValueOrDefault().CurrentValue),
                    EEffectImpactTargetLimited.Base => Scaling.Evaluate(spec.GetSourcedCapturedAttributes()[this].GetValueOrDefault().BaseValue),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            if (CaptureFrom == ESourceTarget.Source)
            {
                if (!spec.GetSource().FindAttributeSystem(out var attr) || !attr.TryGetAttributeValue(CaptureAttribute, out AttributeValue attributeValue)) return 0f;
                return ScalingPolicy switch
                {

                    EEffectImpactTargetLimited.Current => Scaling.Evaluate(attributeValue.CurrentValue),
                    EEffectImpactTargetLimited.Base => Scaling.Evaluate(attributeValue.BaseValue),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            else
            {
                if (!spec.GetTarget().FindAttributeSystem(out var attr) || !attr.TryGetAttributeValue(CaptureAttribute, out AttributeValue attributeValue)) return 0f;
                return ScalingPolicy switch
                {

                    EEffectImpactTargetLimited.Current => Scaling.Evaluate(attributeValue.CurrentValue),
                    EEffectImpactTargetLimited.Base => Scaling.Evaluate(attributeValue.BaseValue),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
    }
    
    public enum ESourceTarget
    {
        Target,
        Source
    }
    
    public enum ECaptureAttributeWhen
    {
        OnCreation,
        OnApplication
    }
}
