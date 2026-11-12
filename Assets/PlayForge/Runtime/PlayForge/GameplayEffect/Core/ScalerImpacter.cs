using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The default impacter shape — wraps a <see cref="ScalableFloat"/>
    /// (the editor-authored magnitude + scaler + RealMagnitude triplet that already lives
    /// on <see cref="GameplayEffectImpact"/>) and exposes it through the impacter contract.
    ///
    /// One <see cref="ScalerImpacter"/> per <see cref="GameplayEffectImpact"/> is the
    /// current shape — multi-impacter authoring per asset is intentionally deferred until
    /// the runtime split has shipped (see refactor plan §5A).
    /// </summary>
    public class ScalerImpacter : IAttributeImpacter
    {
        private readonly ScalableFloat magnitudeOperation;

        public ECalculationOperation Operation { get; }
        public EAttributeTarget      TargetSlot { get; }
        public int                   SortOrder { get; }
        public EMultiplyBehaviour    MultiplyBehaviour { get; }
        public EAddBehaviour         AddBehaviour { get; }
        public ValuePipeline         Pipeline { get; }
        public Tag                   Group { get; }

        /// <summary>The modifiers' stages for it (TW-7): on its magnitude where it lasts, after its pipeline as it commits.</summary>
        public IReadOnlyList<AbstractValueStage> Stages { get; }

        // A lasting impact's stages run on its magnitude, as no pipeline runs on it
        private readonly bool stagesOnMagnitude;

        public ScalerImpacter(
            ScalableFloat magnitudeOperation,
            ECalculationOperation operation,
            EAttributeTarget targetSlot,
            int sortOrder = 0,
            EMultiplyBehaviour multiplyBehaviour = EMultiplyBehaviour.Multiplicative,
            Tag group = default,
            EAddBehaviour addBehaviour = EAddBehaviour.AttributeRole,
            ValuePipeline pipeline = null,
            IReadOnlyList<AbstractValueStage> stages = null,
            bool stagesOnMagnitude = false)
        {
            this.magnitudeOperation = magnitudeOperation;
            Operation         = operation;
            TargetSlot        = targetSlot;
            SortOrder         = sortOrder;
            MultiplyBehaviour = multiplyBehaviour;
            AddBehaviour      = addBehaviour;
            Pipeline          = pipeline;
            Group             = group.IsValid ? group : Tags.IgnoreRetention;
            Stages            = stages is { Count: > 0 } ? stages : null;
            this.stagesOnMagnitude = stagesOnMagnitude && Stages != null;
        }

        public float Evaluate(IAttributeImpactCarrier carrier)
        {
            float value = EvaluateOwn(carrier);
            return stagesOnMagnitude ? ValuePipeline.RunAfter(Stages, value, carrier, null, false) : value;
        }

        private float EvaluateOwn(IAttributeImpactCarrier carrier)
        {
            if (magnitudeOperation is null) return 0f;

            // Specs shelved in a stacking container evaluate container-aware so stack
            // count feeds the magnitude (both the retained Recompute path and per-tick
            // commits). Everything else uses the carrier as evaluation context directly
            // (level, captured attrs, etc.).
            if (carrier is GameplayEffectSpec spec
                && spec.RuntimeContainer is AbstractStackingEffectContainer stackingContainer)
            {
                return magnitudeOperation.Evaluate(stackingContainer);
            }

            return magnitudeOperation.Evaluate(carrier);
        }
    }
}
