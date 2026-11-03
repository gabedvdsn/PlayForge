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

        // The blueprints combined into this one, each resolved on its own and added in (GAS-27, Decision 13)
        private readonly List<AttributeBlueprint> combined = new();

        // Whether the parts are averaged rather than summed, for a set that settles collisions by UseAverage (GAS-40)
        private bool averaged;

        /// <summary>This blueprint and those combined into it, each with scalers of its own to regulate.</summary>
        public IEnumerable<AttributeBlueprint> Parts
        {
            get
            {
                yield return this;
                foreach (var part in combined) yield return part;
            }
        }

        public AttributeBlueprint(AttributeSetElement setElement, ISource owner)
        {
            SetElement = setElement;
            Derivation = IAttributeImpactCarrier.GenerateSourceDerivation(owner, SetElement.Attribute);
        }

        public AttributeValue GetInitialValue(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            // EffectiveCurrent transparently switches to Base when LinkCurrentToBase is set —
            // resolving with isCurrent=true still projects the Current slot from the scaler
            // result, which matches author intent ("use Base's config for the Current side").
            var value = new AttributeValue(
                ResolveSide(SetElement.EffectiveCurrent, cache, isCurrent: true,  initial: true),
                ResolveSide(SetElement.Base,             cache, isCurrent: false, initial: true)
            );
            foreach (var part in combined) value += part.GetInitialValue(cache);
            return averaged ? value / (combined.Count + 1) : value;
        }

        public AttributeValue GetActiveValue(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            var value = new AttributeValue(
                ResolveSide(SetElement.EffectiveCurrent, cache, isCurrent: true,  initial: false),
                ResolveSide(SetElement.Base,             cache, isCurrent: false, initial: false)
            );
            foreach (var part in combined) value += part.GetActiveValue(cache);
            return averaged ? value / (combined.Count + 1) : value;
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
            // Elements set to Combine add up: each resolves on its own, scalers included, and this blueprint's value is
            // the sum. Bounds, rounding and retention group are this one's. It did nothing, so the first element stood
            // alone (GAS-27, Decision 13). RootValue stays this element's own, as cached scalers read it per element
            if (other is null || other == this || combined.Contains(other)) return;
            combined.Add(other);
        }

        /// <summary>Takes in another blueprint to be averaged with this one, for a set that settles collisions by UseAverage (GAS-40).</summary>
        public void Average(AttributeBlueprint other)
        {
            Combine(other);
            averaged = true;
        }
    }
}
