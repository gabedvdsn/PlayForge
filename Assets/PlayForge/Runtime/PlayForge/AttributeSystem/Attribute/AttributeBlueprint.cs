using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class AttributeBlueprint
    {
        public AttributeValue RootValue;
        
        public readonly AttributeSetElement SetElement;

        public AttributeBlueprint(AttributeSetElement setElement)
        {
            SetElement = setElement;
            RootValue = SetElement.ValueFromMagnitude;
        }

        public AttributeValue GetInitialValue(IGameplayAbilitySystem system, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (SetElement.Scaling is null) return RootValue;
            if (SetElement.RealMagnitude == EMagnitudeOperation.UseMagnitude) return RootValue;
            
            var value = SetElement.Scaling.EvaluateInitialValue(system, this, cache);
            var operand = SetElement.Target switch
            {
                EAttributeTargetLimited.CurrentAndBase => new AttributeValue(value, value),
                EAttributeTargetLimited.Base => new AttributeValue(0f, value),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            var realMagnitude = SetElement.RealMagnitude.Translate();
            return ForgeHelper.AttributeMathEvent(RootValue, operand, realMagnitude, SetElement.Target.Translate(), EMathApplicationPolicy.AsIs);
        }
        
        public AttributeValue GetActiveValue(IGameplayAbilitySystem system, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (SetElement.Scaling is null) return RootValue;
            if (SetElement.RealMagnitude == EMagnitudeOperation.UseMagnitude) return RootValue;

            var deriv = IAttributeImpactDerivation.GenerateLevelerDerivation(system, system.GetLevel(), SetElement.Attribute);
            float value = SetElement.Scaling.Evaluate(deriv);
            var operand = SetElement.Target switch
            {
                EAttributeTargetLimited.CurrentAndBase => new AttributeValue(value, value),
                EAttributeTargetLimited.Base => new AttributeValue(0f, value),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            var realMagnitude = SetElement.RealMagnitude.Translate();
            return ForgeHelper.AttributeMathEvent(RootValue, operand, realMagnitude, SetElement.Target.Translate(), EMathApplicationPolicy.AsIs);
        }

        public void Combine(AttributeBlueprint other)
        {
            RootValue.Combine(other.RootValue);
        }
    }
}
