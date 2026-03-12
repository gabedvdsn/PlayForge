using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class AttributeBlueprint
    {
        public AttributeValue RootValue;
        
        public readonly AttributeSetElement Base;

        public AttributeBlueprint(AttributeSetElement @base)
        {
            Base = @base;
            RootValue = Base.RootValue;
        }
        
        public AttributeValue GetDefaultValue(IGameplayAbilitySystem system, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (Base.Scaling is null) return RootValue;
            if (Base.RealMagnitude == EMagnitudeOperation.UseMagnitude) return RootValue;
            
            float value = Base.Scaling.Evaluate(system, this, cache);
            var operand = Base.Target switch
            {
                ELimitedEffectImpactTarget.CurrentAndBase => new AttributeValue(value, value),
                ELimitedEffectImpactTarget.Base => new AttributeValue(0f, value),
                _ => throw new ArgumentOutOfRangeException()
            };
            var realMagnitude = Base.RealMagnitude switch
            {
                EMagnitudeOperation.MultiplyWithScaler => ECalculationOperation.Multiply,
                EMagnitudeOperation.AddScaler => ECalculationOperation.Add,
                EMagnitudeOperation.UseMagnitude => ECalculationOperation.Override,  // Never true
                EMagnitudeOperation.UseScaler => ECalculationOperation.Override,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            return Base.Target switch
            {
                ELimitedEffectImpactTarget.Base => ForgeHelper.AttributeMathEvent(RootValue, operand, realMagnitude, EEffectImpactTarget.Base, EMathApplicationPolicy.AsIs),
                ELimitedEffectImpactTarget.CurrentAndBase => ForgeHelper.AttributeMathEvent(RootValue, operand, realMagnitude, EEffectImpactTarget.CurrentAndBase, EMathApplicationPolicy.AsIs),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public void Combine(AttributeBlueprint other)
        {
            RootValue.Combine(other.RootValue);
        }
    }
}
