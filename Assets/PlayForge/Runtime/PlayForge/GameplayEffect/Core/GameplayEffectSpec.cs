using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class GameplayEffectSpec : IAttributeImpactDerivation
    {
        public GameplayEffect Base;

        public IEffectOrigin Origin;
        public ISource Source;
        public ITarget Target;

        public Dictionary<IScaler, AttributeValue?> SourceCapturedAttributes;

        public GameplayEffectSpec(GameplayEffect GameplayEffect, IEffectOrigin origin, IGameplayAbilitySystem target)
        {
            Base = GameplayEffect;
            Origin = origin;
            
            Source = Origin.GetOwner();
            Target = target;
            
            SourceCapturedAttributes = new Dictionary<IScaler, AttributeValue?>();
        }
        
        public SourcedModifiedAttributeValue SourcedImpact(AttributeValue attributeValue)
        {
            float magnitude = Base.ImpactSpecification.GetMagnitude(this);
            var impactValue = AttributeImpact(magnitude, attributeValue);
            return new SourcedModifiedAttributeValue(
                this,
                impactValue.CurrentValue,
                impactValue.BaseValue
            );
        }

        public SourcedModifiedAttributeValue SourcedImpact(AbstractEffectContainer container, AttributeValue attributeValue)
        {
            float magnitude = container is AbstractStackingEffectContainer _container 
                ? Base.ImpactSpecification.GetMagnitude(_container) 
                : Base.ImpactSpecification.GetMagnitude(this);
            var impactValue = AttributeImpact(magnitude, attributeValue);
            return new SourcedModifiedAttributeValue(
                this,
                container,
                impactValue.CurrentValue,
                impactValue.BaseValue
            );
        }
        
        private AttributeValue AttributeImpact(float magnitude, AttributeValue attributeValue)
        {
            float currValue = attributeValue.CurrentValue;
            float baseValue = attributeValue.BaseValue;
            
            switch (Base.ImpactSpecification.ImpactOperation)
            {
                case ECalculationOperation.Add:
                    switch (Base.ImpactSpecification.TargetImpact)
                    {
                        case EEffectImpactTarget.Current:
                            currValue += magnitude;
                            break;
                        case EEffectImpactTarget.Base:
                            baseValue += magnitude;
                            break;
                        case EEffectImpactTarget.CurrentAndBase:
                            currValue += magnitude;
                            baseValue += magnitude;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                case ECalculationOperation.Multiply:
                    switch (Base.ImpactSpecification.TargetImpact)
                    {
                        case EEffectImpactTarget.Current:
                            currValue *= magnitude;
                            break;
                        case EEffectImpactTarget.Base:
                            baseValue *= magnitude;
                            break;
                        case EEffectImpactTarget.CurrentAndBase:
                            currValue *= magnitude;
                            baseValue *= magnitude;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                case ECalculationOperation.Override:
                    switch (Base.ImpactSpecification.TargetImpact)
                    {
                        case EEffectImpactTarget.Current:
                            currValue = magnitude;
                            break;
                        case EEffectImpactTarget.Base:
                            baseValue = magnitude;
                            break;
                        case EEffectImpactTarget.CurrentAndBase:
                            currValue = magnitude;
                            baseValue = magnitude;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new AttributeValue(
                currValue - attributeValue.CurrentValue,
                baseValue - attributeValue.BaseValue
            );
        }

        public Attribute GetAttribute()
        {
            return Base.ImpactSpecification.AttributeTarget;
        }
        public IEffectOrigin GetEffectDerivation()
        {
            return Origin;
        }
        public ISource GetSource()
        {
            return Source;
        }
        public ITarget GetTarget()
        {
            return Target;
        }
        public List<Tag> GetImpactTypes()
        {
            return Base.ImpactSpecification.ImpactTypes;
        }
        public Tag AttributeRetention()
        {
            return Base.Definition.ImpactRetentionGroup;
        }
        public void TrackImpact(ImpactData impactData)
        {
            // Specs do not track their own impact (tracked in effect containers)
        }
        public TrackedImpact GetTrackedImpact()
        {
            return new TrackedImpact();
        }
        public AttributeValue GetLastTrackedImpact()
        {
            return default;
        }
        public List<Tag> GetContextTags()
        {
            return Origin.GetContextTags();
        }
        public void RunWorkerApplication(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectApplication(ctx));
        }
        public void RunWorkerTick(EffectWorkerContext ctx)
        {
            // Specs never run this method (because non-durational specs, i.e. without containers, are never ticked)
        }
        public void RunWorkerRemoval(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectRemoval(ctx));
        }
        public void RunWorkerImpact(EffectWorkerContext ctx)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) ctx.ActionQueue.EnqueueRange(worker.OnEffectImpact(ctx));
        }
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return SourceCapturedAttributes;
        }
        public IAttributeImpactDerivation GetImpactDerivation()
        {
            return null;
        }
    }
}
