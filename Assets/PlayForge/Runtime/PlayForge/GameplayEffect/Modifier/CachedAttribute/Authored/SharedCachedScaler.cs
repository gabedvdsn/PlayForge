using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An attribute set's reference to a shared <see cref="ScalerAsset"/> whose scaler is one of the cached kinds: it reads
    /// the asset's scaler as the entity's values change, so an edit to the asset reaches every element that uses it
    /// (ED-33). It keeps no values of its own.
    /// </summary>
    [Serializable]
    public class SharedCachedScaler : AbstractCachedScaler
    {
        [Tooltip("The Scaler asset this element reads; its scaler is one of the cached kinds")]
        public ScalerAsset Asset;

        // How deep a chain of shared scalers runs, so one whose asset reads itself reads its root value rather than
        // overflow the stack
        [ThreadStatic] private static int depth;
        private const int MaxDepth = 8;

        private AbstractCachedScaler Shared => Asset != null && depth < MaxDepth ? Asset.Scaler as AbstractCachedScaler : null;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            var shared = Shared;
            if (shared == null) return;
            depth++;
            try { shared.Initialize(deriv); }
            finally { depth--; }
        }

        public override void RegulateContactWith(IAttribute related, AttributeRegulationCache rules)
        {
            var shared = Shared;
            if (shared == null) return;
            depth++;
            try { shared.RegulateContactWith(related, rules); }
            finally { depth--; }
        }

        public override AttributeValue EvaluateActiveValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            var shared = Shared;
            if (shared == null) return blueprint.RootValue;
            depth++;
            try { return shared.EvaluateActiveValue(blueprint, cache); }
            finally { depth--; }
        }

        public override AttributeValue EvaluateInitialValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            var shared = Shared;
            if (shared == null) return blueprint.RootValue;
            depth++;
            try { return shared.EvaluateInitialValue(blueprint, cache); }
            finally { depth--; }
        }

        // Its values are the asset's, so the field shows no table of its own
        public override bool UseScalingOptions() => false;
    }
}
