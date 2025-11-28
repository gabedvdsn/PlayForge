using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class GameplayEffectSpec : IAttributeImpactDerivation
    {
        public GameplayEffect Base;

        public IEffectOrigin Origin;
        public ISource Source;
        public GASComponent Target;

        public Dictionary<IMagnitudeModifier, AttributeValue?> SourceCapturedAttributes;

        public GameplayEffectSpec(GameplayEffect GameplayEffect, IEffectOrigin origin, GASComponent target)
        {
            Base = GameplayEffect;
            Origin = origin;
            
            Source = Origin.GetOwner();
            Target = target;
            
            SourceCapturedAttributes = new Dictionary<IMagnitudeModifier, AttributeValue?>();
        }
        
        public SourcedModifiedAttributeValue SourcedImpact(AttributeValue attributeValue)
        {
            AttributeValue impactValue = AttributeImpact(attributeValue);
            return new SourcedModifiedAttributeValue(
                this,
                impactValue.CurrentValue,
                impactValue.BaseValue
            );
        }

        public SourcedModifiedAttributeValue SourcedImpact(IAttributeImpactDerivation baseDerivation, AttributeValue attributeValue)
        {
            AttributeValue impactValue = AttributeImpact(attributeValue);
            return new SourcedModifiedAttributeValue(
                this,
                baseDerivation,
                impactValue.CurrentValue,
                impactValue.BaseValue
            );
        }
        
        private AttributeValue AttributeImpact(AttributeValue attributeValue)
        {
            float magnitude = Base.ImpactSpecification.GetMagnitude(this);
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
            return Tags.RETENTION_IGNORE;
        }
        public void TrackImpact(AbilityImpactData impactData)
        {
            // Specs do not track their own impact (tracked in effect containers)
        }
        public bool TryGetTrackedImpact(out AttributeValue impactValue)
        {
            impactValue = default;
            return false;
        }
        public bool TryGetLastTrackedImpact(out AttributeValue impactValue)
        {
            impactValue = default;
            return false;
        }
        public List<Tag> GetContextTags()
        {
            return Origin.GetContextTags();
        }
        public void RunEffectApplicationWorkers()
        {
            foreach (AbstractEffectWorker worker in Base.Workers) worker.OnEffectApplication(this);
        }
        public void RunEffectTickWorkers()
        {
            // Specs never run this method (because non-durational specs, i.e. without containers, are never ticked)
        }
        public void RunEffectRemovalWorkers()
        {
            foreach (AbstractEffectWorker worker in Base.Workers) worker.OnEffectRemoval(this);
        }
        public void RunEffectImpactWorkers(AbilityImpactData impactData)
        {
            foreach (AbstractEffectWorker worker in Base.Workers) worker.OnEffectImpact(impactData);
        }
        public Dictionary<IMagnitudeModifier, AttributeValue?> GetSourcedCapturedAttributes()
        {
            return SourceCapturedAttributes;
        }
    }
}
