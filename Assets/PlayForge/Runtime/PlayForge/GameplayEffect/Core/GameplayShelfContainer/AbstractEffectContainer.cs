using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractEffectContainer : ITagSource
    {
        public GameplayEffectSpec Spec;
        public bool Ongoing;
        public bool ContainerIsActive;

        // How near due a tick can be on a duration's last frame and still count, as a share of the period
        private const float LastTickTolerance = 0.001f;
        
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
        
        /// <summary>
        /// Whether a tick falls due this frame. A duration's last tick falls due as the duration ends, and the
        /// two count down apart, so rounding could end the duration a hair before the tick, which then never
        /// landed (GAS-33). A tick that near due on the duration's last frame is that tick.
        /// </summary>
        /// <param name="timeUntilTick">Time to the tick, this frame included</param>
        /// <param name="period">Time between ticks</param>
        /// <param name="durationLeft">Duration left, this frame included</param>
        protected static bool TickIsDue(double timeUntilTick, float period, double durationLeft)
        {
            return timeUntilTick <= 0d || (durationLeft <= 0d && timeUntilTick <= period * LastTickTolerance);
        }

        public abstract void Refresh();
        public abstract void Extend(float duration);

        /// <summary>
        /// The time each stack has left and to its next tick, as a save keeps them: one of each, for a clock that counts one
        /// stack at a time. A container whose clock is more than that overrides this and <see cref="RestoreClock"/> (FEAT-9).
        /// </summary>
        protected internal virtual void CaptureClock(List<float> remaining, List<float> untilTick)
        {
            remaining.Add(NextDurationRemaining);
            untilTick.Add(TimeUntilPeriodTick);
        }

        /// <summary>
        /// Sets the clock as a save left it: the duration and the time left, then the period and the time to the next tick.
        /// An infinite effect's duration, and the period of one that doesn't tick, stay the float.MaxValue its spec gave
        /// it as it was made (FEAT-9).
        /// </summary>
        protected internal virtual void RestoreClock(float total, float period, int stacks, IReadOnlyList<float> remaining, IReadOnlyList<float> untilTick)
        {
            var duration = Spec.Base.DurationSpecification;
            if (duration.DurationPolicy == EEffectDurationPolicy.Durational)
            {
                totalDuration = total;
                if (remaining.Count > 0) SetDurationRemaining(remaining[0]);
            }
            if (duration.EnablePeriodicTicks)
            {
                periodDuration = period;
                if (untilTick.Count > 0) SetTimeUntilPeriodTick(untilTick[0]);
            }
        }

        public virtual void OnRemove()
        {
            ContainerIsActive = false;

            ReverseImpact();

            // Onto the target, where the OnApply and OnTick ones land. These went to the source (GAS-8)
            foreach (var carrier in Spec.GetContainedCarriers(EApplyTickRemove.OnRemove))
            {
                Spec.Target.ApplyCarrier(carrier);
            }
        }

        /// <summary>
        /// Reverse this container's spec contribution: unregister its Continuous entries
        /// through the attribute pipeline (workers, callbacks and the regulation refresh
        /// all observe the removal). Called by <see cref="OnRemove"/> and by the
        /// ReplaceOldContainer re-application path.
        ///
        /// <para>Commits (OnApply / Periodic entries) are permanent by design — there is
        /// no rollback path in the two-kind impact model.</para>
        /// </summary>
        public void ReverseImpact()
        {
            AttributeSystemComponent attrSystem = null;
            if (!(Spec.Target?.FindAttributeSystem(out attrSystem) ?? false) || attrSystem == null) return;

            foreach (var attribute in Spec.GetImpactedAttributes(EImpactDelivery.Continuous))
            {
                attrSystem.UnregisterModifier(attribute, new SourcedModifiedAttributeValue(Spec, 0f, 0f));
            }
        }
        public override string ToString()
        {
            return Spec.Base.ToString();
        }
        public IEnumerable<Tag> GetGrantedTags()
        {
            // Its own asset tag, as an ability or an item grants. This gave its source's, so a target carried the tag
            // of whoever had affected it (GAS-14)
            yield return Spec.Base.GetAssetTag();
            foreach (var t in Spec.Base.GetGrantedTags()) yield return t;
        }

    }

}
