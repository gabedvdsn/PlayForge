using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cached scaler that combines a captured attribute value with a level-based scaling curve.
    /// The curve is evaluated at the owner's relative level, then folded with the captured
    /// attribute value via <see cref="RelativeOperation"/>. Returned as a symmetric AttributeValue
    /// (same float on Current and Base) — the blueprint projects the slot it needs.
    /// </summary>
    public class CachedAttributeBackedScaler : CachedAttributeScaler
    {
        [Tooltip("Operation combining the captured attribute value with the level-curve value")]
        public EScalerRelativeOperation RelativeOperation = EScalerRelativeOperation.Multiply;

        public override AttributeValue EvaluateInitialValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return Combine(blueprint, cache, useActive: false);
        }

        public override AttributeValue EvaluateActiveValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return Combine(blueprint, cache, useActive: true);
        }

        private AttributeValue Combine(
            AttributeBlueprint blueprint,
            IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache,
            bool useActive)
        {
            if (CaptureAttribute is null || !cache.TryGetValue(CaptureAttribute, out var captured))
                return new AttributeValue(0f, 0f);

            float attrCapture = ProjectToBinaryTarget(useActive ? captured.ActiveValue : captured.RootValue);

            float relativeLevel = blueprint.Derivation.GetEffectDerivation().GetLevel().ClampedRatio;
            float scalerValue = EvaluateScalingAtRelativeLevel(relativeLevel);

            float folded = ForgeHelper.PerformScalerRelativeOperation(attrCapture, scalerValue, RelativeOperation);
            folded = ApplyBehaviourEvaluation(blueprint.Derivation, folded);
            return new AttributeValue(folded, folded);
        }

        public override bool UseScalingOptions() => true;
    }
}
