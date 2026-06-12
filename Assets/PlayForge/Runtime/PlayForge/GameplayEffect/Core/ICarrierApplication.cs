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

        /// <summary>
        /// The single retention predicate. A carrier interacts with an attribute in
        /// exactly one of two modes:
        ///
        /// <list type="bullet">
        ///   <item><b>Mode A — Sustained modifier</b> (<c>IsSustainedModifier() == true</c>):
        ///   retained in <c>CachedAttributeValue.RetainedGroups</c>, re-evaluated on every
        ///   Recompute, reversed automatically when removed. e.g. "+50% damage while
        ///   equipped", "+100 max HP buff".</item>
        ///
        ///   <item><b>Mode B — Discrete impact</b> (<c>IsSustainedModifier() == false</c>):
        ///   committed one-shot into <c>committedOneShotDelta</c> (instant effects on
        ///   apply, every tick of a tick-delivered effect). Reversed only via the
        ///   explicit <c>ReverseImpactOnRemoval</c> + <c>TrackedImpact</c> rollback.</item>
        /// </list>
        ///
        /// <para>A carrier is a sustained modifier iff it is durational/infinite AND does
        /// NOT deliver its impact via ticks (no TickOnApplication, no periodic ticks).
        /// A durational non-ticking effect can only have effect by being retained — it
        /// has no tick events to commit through — so that's exactly Mode A. Everything
        /// else (instant, or anything tick-delivered) is Mode B.</para>
        ///
        /// <para>Note: neither "touches Base" nor "wants reversal on removal" influence
        /// this decision. Base one-shots survive Recompute via <c>committedOneShotDelta.Base</c>;
        /// reversal-on-removal is a removal-time concern handled separately in
        /// <c>AbstractEffectContainer.OnRemove</c>.</para>
        /// </summary>
        bool IsSustainedModifier();
    }
}
