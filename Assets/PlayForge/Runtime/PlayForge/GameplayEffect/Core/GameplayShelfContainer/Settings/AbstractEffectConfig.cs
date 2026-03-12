using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A single numeric modifier that can be applied to a value.
    /// </summary>
    [Serializable]
    public struct ValueModifier
    {
        public ECalculationOperation Operation;
        public ScalerMagnitudeOperation MagnitudeOperation;
    
        /// <summary>Optional condition - modifier only applies if tag requirements are met.</summary>
        [SerializeReference]
        public AvoidRequireTagGroup Condition;

        public void Initialize(IAttributeImpactDerivation spec)
        {
            MagnitudeOperation?.Scaler?.Initialize(spec);
        }
        
        public bool Validate(List<Tag> appliedTags)
        {
            return ForgeHelper.ValidateAvoidRequireQueries(
                appliedTags, 
                Condition.RequireTags, 
                Condition.AvoidTags);
        }
        
        /// <summary>
        /// Validates this group against a TagCache.
        /// ALL RequireTags must pass AND NONE of AvoidTags can pass.
        /// </summary>
        public bool Validate(TagCache tagCache)
        {
            return ForgeHelper.ValidateAvoidRequireQueries(
                tagCache, 
                Condition.RequireTags, 
                Condition.AvoidTags);
        }
        
        /// <summary>
        /// Validates this group against any enumerable of tags.
        /// </summary>
        public bool Validate(IEnumerable<Tag> appliedTags)
        {
            return Validate(appliedTags?.ToList() ?? new List<Tag>());
        }
        
        public float Evaluate(float magnitude, IAttributeImpactDerivation spec)
        {
            float m = MagnitudeOperation?.Evaluate(spec) ?? 0f;
            return Operation switch
            {

                ECalculationOperation.Add => magnitude + m,
                ECalculationOperation.Multiply => magnitude * m,
                ECalculationOperation.Override => m,
                ECalculationOperation.FlatBonus => magnitude + m,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        /*public static ValueModifier Add(float value) => new() { Operation = ECalculationOperation.Add, Value = value };
        public static ValueModifier Multiply(float multiplier) => new() { Operation = ECalculationOperation.Multiply, Value = multiplier };
        public static ValueModifier Override(float value) => new() { Operation = ECalculationOperation.Override, Value = value };*/
    }
    
    [Serializable]
    public struct ValueIntModifier
    {
        public ECalculationOperation Operation;
        public ScalerIntegerMagnitudeOperation MagnitudeOperation;
    
        /// <summary>Optional condition - modifier only applies if tag requirements are met.</summary>
        [SerializeReference]
        public AvoidRequireTagGroup Condition;

        public void Initialize(IAttributeImpactDerivation spec)
        {
            MagnitudeOperation?.Scaler?.Initialize(spec);
        }
        
        public bool Validate(List<Tag> appliedTags)
        {
            return ForgeHelper.ValidateAvoidRequireQueries(
                appliedTags, 
                Condition.RequireTags, 
                Condition.AvoidTags);
        }
        
        /// <summary>
        /// Validates this group against a TagCache.
        /// ALL RequireTags must pass AND NONE of AvoidTags can pass.
        /// </summary>
        public bool Validate(TagCache tagCache)
        {
            return ForgeHelper.ValidateAvoidRequireQueries(
                tagCache, 
                Condition.RequireTags, 
                Condition.AvoidTags);
        }
        
        /// <summary>
        /// Validates this group against any enumerable of tags.
        /// </summary>
        public bool Validate(IEnumerable<Tag> appliedTags)
        {
            return Validate(appliedTags?.ToList() ?? new List<Tag>());
        }
        
        public float Evaluate(float magnitude, IAttributeImpactDerivation spec)
        {
            int m = MagnitudeOperation?.Evaluate(spec) ?? 0;
            return Operation switch
            {

                ECalculationOperation.Add => magnitude + m,
                ECalculationOperation.Multiply => magnitude * m,
                ECalculationOperation.Override => m,
                ECalculationOperation.FlatBonus => magnitude + m,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        /*public static ValueModifier Add(float value) => new() { Operation = ECalculationOperation.Add, Value = value };
        public static ValueModifier Multiply(float multiplier) => new() { Operation = ECalculationOperation.Multiply, Value = multiplier };
        public static ValueModifier Override(float value) => new() { Operation = ECalculationOperation.Override, Value = value };*/
    }
    
    public abstract class GameplayPipelineConfiguration
    {
        public string Name;
        public int Priority;
        public virtual int ConfigPriority => Priority;

        public List<Tag> AdditionalGrantedTags;
    }
    
    [Serializable]
    public abstract class AbstractEffectConfig<T> : GameplayPipelineConfiguration where T : AbstractEffectContainer
    {
        public EffectTagRequirements SourceRequirements;
        public EffectTagRequirements TargetRequirements;
        
        
        public abstract T GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, T[] containers);
    }

    public enum EInclusionPolicy
    {
        UseDefined,
        UseOverride,
        UseBoth,
        UseNeither
    }

    public class EffectImpactConfig : AbstractEffectConfig<AbstractEffectContainer>
    {
        public Attribute AttributeTarget;
        public EEffectImpactTarget TargetImpact;
        public ECalculationOperation ImpactOperation;
        
        public List<ValueModifier> MagnitudeModifiers;
        public EInclusionPolicy MagnitudeInclusion = EInclusionPolicy.UseBoth;
        
        public EAffiliationPolicy? AffiliationPolicy;
        public EAnyAllPolicy? AffiliationComparison;
        public List<Tag> AffiliationList;
        public EInclusionPolicy AffiliationListInclusion = EInclusionPolicy.UseBoth;

        public List<Tag> ImpactTypes;
        public EInclusionPolicy ImpactTypeInclusion = EInclusionPolicy.UseBoth; 
        public bool? ReverseImpactOnRemoval;
        
        public List<ContainedEffectPacket> ContainedEffects;
        public EInclusionPolicy ContainedEffectsInclusion = EInclusionPolicy.UseBoth;
        
        public override AbstractEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractEffectContainer[] containers)
        {
            return containers.FirstOrDefault();
        }
    }
    
    public abstract class AbstractEffectDurationConfig : AbstractEffectConfig<AbstractEffectContainer>
    {
        public EEffectDurationPolicy? DurationPolicy;
        
        public List<ValueModifier> DurationModifiers;
        public EInclusionPolicy DurationInclusion = EInclusionPolicy.UseBoth;
        public EMagnitudeOperation DurationOperation = EMagnitudeOperation.AddScaler;

        public EMagnitudeOperation RealDeltaTime = EMagnitudeOperation.UseMagnitude;
        [SerializeReference] public AbstractScaler DeltaTimeScaler;

        public bool? EnablePeriodicTicks;
        public bool? TickOnApplication;

        public List<ValueIntModifier> TicksModifiers;
        public EInclusionPolicy TicksInclusion = EInclusionPolicy.UseBoth;
        
        public List<ValueModifier> TickIntervalModifiers;
        public EInclusionPolicy TickIntervalInclusion = EInclusionPolicy.UseBoth;
        
        public List<ValueIntModifier> AdditionalExecuteTicksModifiers;
        public EInclusionPolicy AdditionalExecuteTicksInclusion = EInclusionPolicy.UseBoth;

        public EEffectReApplicationPolicy? ReApplicationPolicy;
        public EEffectInteractionPolicy? ReApplicationInteraction;

        public EEffectStackPolicy? StackPolicy;
        [SerializeReference] public List<AbstractStackingConfig> StackingConfigs;
        
        public List<ValueIntModifier> StackAmountModifiers;
        public EInclusionPolicy StackAmountInclusion = EInclusionPolicy.UseBoth;
        
        public override AbstractEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractEffectContainer[] containers)
        {
            return containers.FirstOrDefault();
        }
    }
    
    public abstract class AbstractStackingConfig : AbstractEffectConfig<AbstractStackingEffectContainer>
    {
        public EEffectStackPolicy? StackPolicy;
        public int? MaxStacks;
        
        public abstract void InitializeContainer(AbstractStackingEffectContainer container);
        public abstract int GetStacks(AbstractStackingEffectContainer container, int amount = 1);
        
        public override AbstractStackingEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            return containers.FirstOrDefault();
        }
}

    public enum EStackBoundsOnMaxPolicy
    {
        DoNothing,
        AppendNewContainer
    }

    public class StackBoundsConfig : AbstractStackingConfig
    {
        public int Min;
        public int Max;
        public EStackBoundsOnMaxPolicy OnMaxPolicy = EStackBoundsOnMaxPolicy.DoNothing;

        public override int ConfigPriority => 999;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            container.Stack(Min);
        }
        
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            return Mathf.Min(Max, Mathf.Max(Min, container.Stacks + amount));
        }
        

        public override AbstractStackingEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            return OnMaxPolicy == EStackBoundsOnMaxPolicy.DoNothing 
                ? base.GetCorrectContainer(spec, ongoing, containers) 
                : containers.FirstOrDefault(c => c.Stacks < Max);
        }
    }

    public class StackAmountOperation : AbstractStackingConfig
    {
        public EMagnitudeOperation Operation;
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] [SerializeReference] 
        public AbstractScaler Scaler;
        public ERoundingOperation Rounding;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            
        }
        
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            if (Scaler is null) return amount;
            
            float stacks = Operation switch
            {
                EMagnitudeOperation.MultiplyWithScaler => amount * Scaler.Evaluate(container.Spec),
                EMagnitudeOperation.AddScaler => amount + Scaler.Evaluate(container.Spec),
                EMagnitudeOperation.UseMagnitude => amount,
                EMagnitudeOperation.UseScaler => Scaler.Evaluate(container.Spec),
                _ => throw new ArgumentOutOfRangeException()
            };

            return Rounding switch
            {

                ERoundingOperation.Floor => Mathf.FloorToInt(stacks),
                ERoundingOperation.Ceil => Mathf.CeilToInt(stacks),
                _ => throw new ArgumentOutOfRangeException()
            };
        } 
    }

    public class InitializationOperation : AbstractStackingConfig
    {
        public int InitializationStacks = 0;
        public EMagnitudeOperation Operation;
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] [SerializeReference] 
        public AbstractScaler Scaler;
        public ERoundingOperation Rounding;
        
        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            if (Scaler is null)
            {
                container.Stack(InitializationStacks);
                return;
            }
            
            float stacks = Operation switch
            {
                EMagnitudeOperation.MultiplyWithScaler => InitializationStacks * Scaler.Evaluate(container.Spec),
                EMagnitudeOperation.AddScaler => InitializationStacks + Scaler.Evaluate(container.Spec),
                EMagnitudeOperation.UseMagnitude => InitializationStacks,
                EMagnitudeOperation.UseScaler => Scaler.Evaluate(container.Spec),
                _ => throw new ArgumentOutOfRangeException()
            };

            var intStacks = Rounding switch
            {
                ERoundingOperation.Floor => Mathf.FloorToInt(stacks),
                ERoundingOperation.Ceil => Mathf.CeilToInt(stacks),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            container.Stack(intStacks);
        }
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            return amount;
        }
        public override int ConfigPriority => -1;
    }

    // The other intrinsic behaviours...

}
