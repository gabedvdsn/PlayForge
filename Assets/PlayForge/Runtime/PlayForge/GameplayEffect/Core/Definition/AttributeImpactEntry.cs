using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How an impact entry rides its carrier's lifecycle. Delivery IMPLIES the
    /// contribution kind (see IMPACT_MODEL_REFACTOR.md §1) — there is deliberately no
    /// independent "retained?" switch to get wrong:
    ///
    /// <list type="bullet">
    ///   <item><see cref="Continuous"/> → <b>Modifier</b> (transient, derived). Registered
    ///   while the carrier is present, re-evaluated on every recompute, reversed
    ///   automatically and exactly when the carrier is removed. Only valid on
    ///   durational/infinite carriers.</item>
    ///
    ///   <item><see cref="OnApply"/> → <b>Commit</b> (permanent). One commit at
    ///   application time. Never reversed. This is what an Instant effect is.</item>
    ///
    ///   <item><see cref="Periodic"/> → <b>Commit</b> (permanent) once per execute tick.
    ///   Never reversed. Requires a carrier with periodic ticks enabled.</item>
    /// </list>
    /// </summary>
    public enum EImpactDelivery
    {
        Continuous,
        OnApply,
        Periodic,
    }

    /// <summary>
    /// One authored impact: WHAT attribute, HOW (operation/slot/magnitude), WHEN
    /// (<see cref="Delivery"/>), and WHO it belongs to (retention group, for the
    /// per-group attribution surface). A <see cref="GameplayEffectImpact"/> holds a list
    /// of these — one effect can carry e.g. a Continuous "+10% speed" AND a Periodic
    /// "5 HP per tick" without contained-effect workarounds.
    /// </summary>
    [Serializable]
    public class AttributeImpactEntry
    {
        public Attribute AttributeTarget;

        public EImpactDelivery Delivery = EImpactDelivery.Continuous;

        public EAttributeTarget TargetSlot = EAttributeTarget.Current;
        public ECalculationOperation Operation = ECalculationOperation.Add;

        /// <summary>Only meaningful for Multiply — see <see cref="EMultiplyBehaviour"/>.</summary>
        public EMultiplyBehaviour MultiplyBehaviour = EMultiplyBehaviour.Multiplicative;

        /// <summary>Tie-break for Override entries (lowest wins); ordering hint elsewhere.</summary>
        public int SortOrder;

        [ScalerOperationKeyword("Magnitude")]
        public ScalableFloat Magnitude = new();

        /// <summary>
        /// Retention group this entry's contribution is attributed to. Invalid (unset)
        /// → falls back to the effect's <see cref="GameplayEffectDefinition.RetentionGroup"/>.
        /// Groups are pure attribution (base vs bonus health, base vs bonus damage) —
        /// they partition the pool for queries/UI, never change the math.
        /// </summary>
        [ForgeTagContext(ForgeContext.RetentionGroup)]
        public Tag RetentionGroupOverride;

        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactTypes = new();

        public AttributeImpactEntry() { }

        public AttributeImpactEntry(AttributeImpactEntry o)
        {
            AttributeTarget = o.AttributeTarget;
            Delivery = o.Delivery;
            TargetSlot = o.TargetSlot;
            Operation = o.Operation;
            MultiplyBehaviour = o.MultiplyBehaviour;
            SortOrder = o.SortOrder;
            Magnitude = o.Magnitude;
            RetentionGroupOverride = o.RetentionGroupOverride;
            ImpactTypes = o.ImpactTypes != null ? new List<Tag>(o.ImpactTypes) : new List<Tag>();
        }

        public Tag ResolveGroup(Tag effectDefault)
            => RetentionGroupOverride.IsValid ? RetentionGroupOverride : effectDefault;

        /// <summary>Validation used by the drawer AND the runtime migration warnings.</summary>
        public bool IsDeliveryValidFor(GameplayEffectDuration duration)
        {
            if (duration == null) return true;
            return Delivery switch
            {
                EImpactDelivery.Continuous => duration.DurationPolicy != EEffectDurationPolicy.Instant,
                EImpactDelivery.Periodic => duration.DurationPolicy != EEffectDurationPolicy.Instant
                                            && (duration.EnablePeriodicTicks || duration.TickOnApplication),
                EImpactDelivery.OnApply => true,
                _ => false,
            };
        }
    }
}
