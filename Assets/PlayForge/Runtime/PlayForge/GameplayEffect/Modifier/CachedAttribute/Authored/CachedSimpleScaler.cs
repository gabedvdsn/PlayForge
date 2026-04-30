using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Simple cached scaler based on the owning system's relative level.
    /// Has no attribute dependencies, so RegulateContactWith is a no-op (inherited from base).
    /// Returns the same scalar on both Current and Base sides — the blueprint will project
    /// whichever slot it needs.
    /// </summary>
    public class CachedSimpleScaler : AbstractCachedScaler
    {
        public override AttributeValue EvaluateActiveValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            float v = EvaluateFromSpec(blueprint.Derivation);
            return new AttributeValue(v, v);
        }

        public override AttributeValue EvaluateInitialValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            // Initial = active for level-only scalers (no transient attribute state involved).
            return EvaluateActiveValue(blueprint, cache);
        }
    }
}
