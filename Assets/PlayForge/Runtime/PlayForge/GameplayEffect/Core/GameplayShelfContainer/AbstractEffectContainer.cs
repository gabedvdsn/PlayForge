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
            
            Spec.Base.ApplyDurationSpecifications(this);
        }

        public virtual void ReplaceValuesWith(AbstractEffectContainer container)
        {
            totalDuration = container.totalDuration;
            periodDuration = container.periodDuration;
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
            if (Spec.Base.ImpactSpecification.ReverseImpactOnRemoval)
            {
                AttributeValue negatedImpact = Spec.TrackedImpact.Total.Negate();
                Spec.Source.GetActionQueue().Enqueue(new ModifyAttributeAction(
                    Spec.Source, Spec.Base.ImpactSpecification.AttributeTarget,
                    new SourcedModifiedAttributeValue(Spec,
                        negatedImpact.CurrentValue, negatedImpact.BaseValue,
                        false),
                    false
                ));
            }
            
            foreach (var containedEffect in Spec.Base.ImpactSpecification.GetContainedEffects(EApplyTickRemove.OnRemove))
            {
                Spec.Source.ApplyGameplayEffect(Spec.Source.GenerateEffectSpec(Spec.Origin, containedEffect));
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
