using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractEffectContainer : ITagSource
    {
        public GameplayEffectSpec Spec;
        public bool Ongoing;
        public bool ContainerIsActive;
        
        protected float totalDuration;
        protected float periodDuration;

        public float TotalDuration => totalDuration;
        public abstract float DurationRemaining { get; }
        public abstract float NextDurationRemaining { get; }
        public float PeriodDuration => periodDuration;
        
        public abstract int InstantExecuteTicks { get; }
        
        public abstract float TimeUntilPeriodTick { get; }
        
        protected AbstractEffectContainer(GameplayEffectSpec spec, bool ongoing)
        {
            Spec = spec;
            Ongoing = ongoing;

            ContainerIsActive = true;

            Spec.SetRuntimeContainer(this);
            Spec.Base.ApplyDurationSpecifications(this);
        }

        /// <summary>
        /// Adopt another container's configuration: duration values AND its spec. Used by
        /// ReplaceOldContainer re-application — the old spec's contribution must be reversed
        /// by the caller (via <see cref="ReverseImpact"/>) BEFORE calling this, and the new
        /// spec's sustained contribution registered after. Accumulated container state
        /// (e.g. stack count) is intentionally preserved.
        /// </summary>
        public virtual void ReplaceValuesWith(AbstractEffectContainer container)
        {
            totalDuration = container.totalDuration;
            periodDuration = container.periodDuration;

            Spec = container.Spec;
            Spec.SetRuntimeContainer(this);
        }

        public void SetTotalDuration(float duration)
        {
            totalDuration = duration;
            if (DurationRemaining > totalDuration) SetDurationRemaining(totalDuration);
        }
        public abstract void SetDurationRemaining(float duration);

        public void SetPeriodDuration(float duration)
        {
            periodDuration = duration;
            if (TimeUntilPeriodTick > periodDuration) SetTimeUntilPeriodTick(periodDuration);
        }
        public abstract void SetTimeUntilPeriodTick(float duration);
        
        public abstract void UpdateTimeRemaining(float deltaTime);
        public abstract void TickPeriodic(float deltaTime, out int executeTicks);
        
        public abstract void Refresh();
        public abstract void Extend(float duration);

        public virtual void OnRemove()
        {
            ContainerIsActive = false;

            ReverseImpact();

            foreach (var containedEffect in Spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnRemove))
            {
                Spec.Source.ApplyCarrier(Spec.Source.GenerateEffectSpec(Spec.Origin, containedEffect));
            }
        }

        /// <summary>
        /// Mode-aware reversal of this container's spec contribution. Called by
        /// <see cref="OnRemove"/> and by the ReplaceOldContainer re-application path
        /// (replacing a spec ends its contribution exactly like removal does).
        /// Safe to call for specs that contributed nothing.
        /// </summary>
        public void ReverseImpact()
        {
            var attribute = Spec.Base.ImpactSpecification.AttributeTarget;
            AttributeSystemComponent attrSystem = null;
            bool foundAttrSys = attribute && (Spec.Target?.FindAttributeSystem(out attrSystem) ?? false);

            // Containers only exist for durational/infinite effects, so the spec is in
            // exactly one of two modes (see ICarrierApplication.IsSustainedModifier):
            //   • Mode A — sustained modifier  → it was retained.
            //   • Mode B — tick-delivered      → it committed one-shot per tick.
            bool wasSustainedModifier = Spec.IsSustainedModifier();

            // ─── Mode A reversal — drop from the retained list ───────────────
            // RemoveCarrier triggers Recompute, which re-derives ActiveValue without
            // the spec's impacters. That IS the reversal for sustained modifiers.
            // Skipped for Mode B specs: they were never retained, so there's nothing
            // to remove and the recompute would be a pure no-op.
            if (wasSustainedModifier && foundAttrSys)
            {
                attrSystem.RemoveAttributeDerivation(Spec);
            }

            // ─── Mode B reversal — roll back the cumulative tick history ─────
            // Tick-delivered specs committed each tick one-shot into the attribute's
            // committedOneShotDelta. TrackedImpact.Total is exactly the sum of those
            // commits, so subtracting it "undoes every tick this spec ever ran."
            //
            // Gated on !wasSustainedModifier: a Mode A spec's TrackedImpact.Total is
            // its apply-time retained contribution (also non-zero!), and that was
            // ALREADY reversed by the RemoveAttributeDerivation above — rolling it back
            // again here would double-undo.
            if (!wasSustainedModifier
                && Spec.Base.ImpactSpecification.ReverseImpactOnRemoval
                && foundAttrSys
                && attrSystem.TryGetCachedValue(attribute, out var cachedAttr))
            {
                AttributeValue tickedTotal = Spec.TrackedImpact?.Total ?? default;
                if (tickedTotal.CurrentValue != 0f || tickedTotal.BaseValue != 0f)
                {
                    cachedAttr.RollbackCommittedDelta(tickedTotal, attrSystem.GetAttributeCache());
                }
            }
        }
        public override string ToString()
        {
            return Spec.Base.ToString();
        }
        public IEnumerable<Tag> GetGrantedTags()
        {
            yield return Spec.Origin.GetAssetTag();
            foreach (var t in Spec.Base.GetGrantedTags()) yield return t;
        }

    }

}
