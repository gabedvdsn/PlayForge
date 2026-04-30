using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Describes the basest definition of an attribute's value on an entity — analogous to
    /// "base stats" before items, effects, or other transient sources contribute. Each
    /// <see cref="AttributeSetElement"/> exposes Current and Base configs; this blueprint
    /// resolves both sides independently and assembles the resulting <see cref="AttributeValue"/>.
    ///
    /// Per-side resolution rules (applied identically for Current and Base):
    ///   1. If the side's scaler is null OR its RealMagnitude is UseMagnitude → return Magnitude.
    ///   2. Otherwise call the cached scaler (Initial vs Active path) and project the side's
    ///      slot from the returned AttributeValue (Current → CurrentValue, Base → BaseValue),
    ///      then combine with the side's Magnitude via its RealMagnitude operation.
    /// </summary>
    public class AttributeBlueprint
    {
        public AttributeValue RootValue => SetElement.ValueFromMagnitude;

        public readonly AttributeSetElement SetElement;
        public readonly SourceAttributeImpact Derivation;

        public AttributeBlueprint(AttributeSetElement setElement, ISource owner)
        {
            SetElement = setElement;
            Derivation = IAttributeImpactDerivation.GenerateSourceDerivation(owner, SetElement.Attribute);
        }

        public AttributeValue GetInitialValue(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            // EffectiveCurrent transparently switches to Base when LinkCurrentToBase is set —
            // resolving with isCurrent=true still projects the Current slot from the scaler
            // result, which matches author intent ("use Base's config for the Current side").
            return new AttributeValue(
                ResolveSide(SetElement.EffectiveCurrent, cache, isCurrent: true,  initial: true),
                ResolveSide(SetElement.Base,             cache, isCurrent: false, initial: true)
            );
        }

        public AttributeValue GetActiveValue(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return new AttributeValue(
                ResolveSide(SetElement.EffectiveCurrent, cache, isCurrent: true,  initial: false),
                ResolveSide(SetElement.Base,             cache, isCurrent: false, initial: false)
            );
        }

        private float ResolveSide(
            AttributeMagnitudeSpec spec,
            IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache,
            bool isCurrent,
            bool initial)
        {
            if (spec is null || spec.BypassScaling) return spec?.Magnitude ?? 0f;

            var scalerResult = initial
                ? spec.Scaling.EvaluateInitialValue(this, cache)
                : spec.Scaling.EvaluateActiveValue(this, cache);

            float scalerValue = isCurrent ? scalerResult.CurrentValue : scalerResult.BaseValue;
            return ForgeHelper.MagnitudeAndScalerOperation(spec.Magnitude, scalerValue, spec.RealMagnitude);
        }

        public void Combine(AttributeBlueprint other)
        {
            // Element-level Combine: base values bubble up. Combine semantics for the per-side
            // scalers are not defined yet — current behaviour mirrors the prior single-magnitude
            // path. Authors using Combine should be aware that scaler/RealMagnitude config from
            // 'other' is not merged here.
            // (Intentional no-op aside from RootValue accumulation already done at the
            // AttributeValue level if needed.)
        }
    }
}
