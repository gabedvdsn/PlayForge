using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The "applicable" facet of a carrier — duration, container generation, contained
    /// effects, worker phases, validation. <see cref="ITarget"/> exposes its application
    /// surface against this interface (not against a concrete <see cref="GameplayEffectSpec"/>)
    /// so any future carrier kind that participates in the apply-shelf pipeline can plug in
    /// without subclassing the spec.
    ///
    /// <para>Plain <see cref="IAttributeImpactCarrier"/>s — synthetic deltas, leveler
    /// snapshots, nullified markers — intentionally do NOT implement this interface. They
    /// are identity + magnitude only; forcing them to expose duration policy or worker hooks
    /// would be lying about their lifecycle.</para>
    /// </summary>
    public interface ICarrierApplication : IAttributeImpactCarrier
    {
        /// <summary>Instant / Durational / Infinite — drives shelf placement.</summary>
        EEffectDurationPolicy GetDurationPolicy();

        /// <summary>Generate the runtime container that will sit on the target's effect shelf.</summary>
        AbstractEffectContainer GenerateContainer(bool ongoing);

        /// <summary>
        /// Carriers that should be applied alongside this one at the given lifecycle phase
        /// (apply / tick / remove). Replaces the old direct-on-spec
        /// <c>GetContainedEffects(EApplyTickRemove)</c> path with carrier-typed plumbing.
        /// </summary>
        IEnumerable<ICarrierApplication> GetContainedCarriers(EApplyTickRemove phase);

        // ─── Worker phases — moved here from IAttributeImpactCarrier ──────────────
        // Synthetic carriers don't have workers; only applicables do.
        void RunWorkerApplication(EffectWorkerContext ctx);
        void RunWorkerTick(EffectWorkerContext ctx);
        void RunWorkerRemoval(EffectWorkerContext ctx);
        void RunWorkerImpact(EffectWorkerContext ctx);

        /// <summary>
        /// Pre-application validation (affiliation, source / target tag requirements).
        /// Returns false to refuse application; the target should drop the carrier with no
        /// side effect.
        /// </summary>
        bool ValidateApplication(ITarget target);

        // IsSustainedModifier removed (impact-model overhaul). The old Mode A/B inference
        // from duration + tick flags is replaced by per-entry EImpactDelivery declarations:
        // Continuous entries are modifiers (transient, derived, auto-reversing);
        // OnApply/Periodic entries are commits (permanent). See AttributeImpactEntry.
    }
}
