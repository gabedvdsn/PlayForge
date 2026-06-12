namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The default impacter shape — wraps a <see cref="ScalerMagnitudeOperation"/>
    /// (the editor-authored magnitude + scaler + RealMagnitude triplet that already lives
    /// on <see cref="GameplayEffectImpact"/>) and exposes it through the impacter contract.
    ///
    /// One <see cref="ScalerImpacter"/> per <see cref="GameplayEffectImpact"/> is the
    /// current shape — multi-impacter authoring per asset is intentionally deferred until
    /// the runtime split has shipped (see refactor plan §5A).
    /// </summary>
    public class ScalerImpacter : IAttributeImpacter
    {
        private readonly ScalerMagnitudeOperation magnitudeOperation;

        public ECalculationOperation Operation { get; }
        public EAttributeTarget      TargetSlot { get; }
        public int                   SortOrder { get; }
        public EMultiplyBehaviour    MultiplyBehaviour { get; }

        public ScalerImpacter(
            ScalerMagnitudeOperation magnitudeOperation,
            ECalculationOperation operation,
            EAttributeTarget targetSlot,
            int sortOrder = 0,
            EMultiplyBehaviour multiplyBehaviour = EMultiplyBehaviour.Multiplicative)
        {
            this.magnitudeOperation = magnitudeOperation;
            Operation         = operation;
            TargetSlot        = targetSlot;
            SortOrder         = sortOrder;
            MultiplyBehaviour = multiplyBehaviour;
        }

        public float Evaluate(IAttributeImpactCarrier carrier)
        {
            // ScalerMagnitudeOperation already takes the carrier as evaluation context
            // (level, captured attrs, etc.) — reuse that surface unchanged.
            return magnitudeOperation?.Evaluate(carrier) ?? 0f;
        }
    }
}
