using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A field's reference to a shared <see cref="ScalerAsset"/>: it reads the asset's scaler, so an edit to the asset
    /// reaches every field that uses it, with nothing to propagate (ED-33, Decision 28). It keeps no values of its own;
    /// the field's Magnitude adjusts what it reads, as in Fire Duration × 1.5.
    /// </summary>
    [Serializable]
    [NumberKind("Shared")]
    public class SharedScaler : AbstractScaler
    {
        [Tooltip("The Scaler asset this field reads")]
        public ScalerAsset Asset;

        // How deep a chain of shared scalers runs, so one whose asset reads itself reads 0 rather than overflow the stack
        [ThreadStatic] private static int depth;
        private const int MaxDepth = 8;

        // The asset's scaler, while the chain is shallow enough to read
        private AbstractScaler Shared => Asset != null && depth < MaxDepth ? Asset.Scaler : null;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            var shared = Shared;
            if (shared == null) return;
            depth++;
            try { shared.Initialize(deriv); }
            finally { depth--; }
        }

        public override float Evaluate(IAttributeImpactCarrier deriv)
        {
            var shared = Shared;
            if (shared == null) return 0f;
            depth++;
            try { return shared.Evaluate(deriv); }
            finally { depth--; }
        }

        public override void Initialize(AbstractStackingEffectContainer container)
        {
            var shared = Shared;
            if (shared == null) return;
            depth++;
            try { shared.Initialize(container); }
            finally { depth--; }
        }

        public override float Evaluate(AbstractStackingEffectContainer container)
        {
            var shared = Shared;
            if (shared == null) return 0f;
            depth++;
            try { return shared.Evaluate(container); }
            finally { depth--; }
        }

        // Its values are the asset's, so the field shows no table of its own
        public override bool UseScalingOptions() => false;

        // It reads its asset's scaler alone (ED-51)
        public override EScalerParts Parts => EScalerParts.None;
    }
}
