using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractEffectContainer : IAttributeImpactDerivation, ITagSource
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
        
        private TrackedImpact TrackedImpact;
        private AttributeValue LastTrackedImpact;

        private List<AbstractEffectWorker> Workers;

        protected AbstractEffectContainer(GameplayEffectSpec spec, bool ongoing)
        {
            Spec = spec;
            Ongoing = ongoing;
            
            Workers = spec.Base.Workers;
            TrackedImpact = new TrackedImpact();

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
                AttributeValue negatedImpact = TrackedImpact.Total.Negate();
                Spec.Source.GetActionQueue().Enqueue(new ModifyAttributeAction(
                    Spec.Source, Spec.Base.ImpactSpecification.AttributeTarget,
                    new SourcedModifiedAttributeValue(this, Spec,
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

        public Attribute GetAttribute()
        {
            return Spec.Base.ImpactSpecification.AttributeTarget;
        }
        public IEffectOrigin GetEffectDerivation()
        {
            return Spec.GetEffectDerivation();
        }
        public ISource GetSource()
        {
            return Spec.GetSource();
        }
        public ITarget GetTarget()
        {
            return Spec.Target;
        }
        public List<Tag> GetImpactTypes()
        {
            return Spec.GetImpactTypes();
        }
        public Tag AttributeRetention()
        {
            return Spec.Base.Definition.ImpactRetentionGroup;
        }
        public void TrackImpact(ImpactData impactData)
        {
            TrackedImpact.Add(impactData.RealImpact);
            LastTrackedImpact = impactData.RealImpact;
        }
        public TrackedImpact GetTrackedImpact()
        {
            return TrackedImpact;
        }
        public AttributeValue GetLastTrackedImpact()
        {
            return TrackedImpact.Last;
        }
        public List<Tag> GetContextTags()
        {
            return Spec.Origin.GetContextTags();
        }
        public void RunWorkerApplication(EffectWorkerContext ctx)
        {
            foreach (var worker in Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectApplication(ctx));
        }
        public void RunWorkerTick(EffectWorkerContext ctx)
        {
            foreach (var worker in Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectTick(ctx));
        }
        public void RunWorkerRemoval(EffectWorkerContext ctx)
        {
            foreach (var worker in Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectRemoval(ctx));
        }
        public void RunWorkerImpact(EffectWorkerContext ctx)
        {
            foreach (var worker in Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectImpact(ctx));
        }
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return new();
        }
        public IAttributeImpactDerivation GetImpactDerivation()
        {
            return Spec;
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
