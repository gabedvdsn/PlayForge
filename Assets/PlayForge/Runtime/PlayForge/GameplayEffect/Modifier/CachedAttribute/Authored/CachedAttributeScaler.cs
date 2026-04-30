using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cached scaler that mirrors a captured attribute's value. Registers a regulation relation
    /// so the owning attribute is recomputed whenever the captured attribute changes.
    /// Initial value reads from the captured attribute's RootValue (its base definition);
    /// active value reads from its current ActiveValue.
    /// </summary>
    public class CachedAttributeScaler : AbstractCachedScaler
    {
        [Tooltip("Which attribute to read")]
        public Attribute CaptureAttribute;

        [Tooltip("Use base or current attribute value (used when projecting to a single float)")]
        public EAttributeTargetBinary CaptureWhat = EAttributeTargetBinary.Current;
        
        public override void RegulateContactWith(IAttribute related, AttributeRegulationCache rules)
        {
            if (CaptureAttribute != null && related != null)
            {
                rules.RegisterRelation(CaptureAttribute, related);
            }
        }

        public override AttributeValue EvaluateInitialValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return cache.TryGetValue(CaptureAttribute, out var value)
                ? value.RootValue
                : new AttributeValue(0f, 0f);
        }

        public override AttributeValue EvaluateActiveValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return cache.TryGetValue(CaptureAttribute, out var value)
                ? value.ActiveValue
                : new AttributeValue(0f, 0f);
        }

        protected float ProjectToBinaryTarget(AttributeValue value)
        {
            return CaptureWhat switch
            {
                EAttributeTargetBinary.Current => value.CurrentValue,
                EAttributeTargetBinary.Base => value.BaseValue,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public override bool UseScalingOptions() => false;
    }
}
