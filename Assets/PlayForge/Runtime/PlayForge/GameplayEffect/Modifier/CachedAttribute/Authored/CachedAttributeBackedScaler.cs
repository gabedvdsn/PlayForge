using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cached scaler backed by an attribute value.
    /// Registers the attribute dependency for cache invalidation.
    /// </summary>
    public class CachedAttributeBackedScaler : CachedAttributeScaler
    {
        public ECalculationOperation RelativeOperation = ECalculationOperation.Multiply;
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (CaptureAttribute is null) return 0f;
            
            if (!spec.GetSource().FindAttributeSystem(out var attrSystem) || 
                !attrSystem.TryGetAttributeValue(CaptureAttribute, out AttributeValue attributeValue))
            {
                return 0f;
            }
            
            
        }

        public override AttributeValue EvaluateInitialValue(SourceAttributeImpact deriv1, AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (!cache.TryGetValue(CaptureAttribute, out var value)) return new AttributeValue(0f, 0f);

            var deriv = IAttributeImpactDerivation.GenerateSourceDerivation(deriv1, blueprint.SetElement.Attribute);
            var relativeLevel = GetEffectiveRelativeLevel(deriv);
            var scalingValue = EvaluateScalingAtRelativeLevel(relativeLevel);
            
            var operand = 
            
            var result = ForgeHelper.AttributeMathEvent(value, r)
        }

        public override bool UseScalingOptions()
        {
            return true;
        }
    }
}