using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Simple cached scaler based on relative level.
    /// Does not register any attribute dependencies since it only uses level.
    /// </summary>
    public class CachedSimpleScaler : AbstractCachedScaler
    {
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            return EvaluateFromSpec(spec);
        }

        public override void Regulate(Attribute attribute, AttributeModificationRule rules)
        {
            // No attribute dependencies - this scaler only uses level, not attributes
        }
        public override float Evaluate(IGameplayAbilitySystem gas, AttributeBlueprint blueprint, IReadOnlyDictionary<Attribute, CachedAttributeValue> cache)
        {
            throw new System.NotImplementedException();
        }
    }
}
