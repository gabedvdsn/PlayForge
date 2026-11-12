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
        public ScalableFloat MagnitudeOperation;
    
        /// <summary>Optional condition - modifier only applies if tag requirements are met.</summary>
        [SerializeReference]
        public AvoidRequireTagGroup Condition;

        public void Initialize(IAttributeImpactCarrier spec)
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
        
        public float Evaluate(float magnitude, IAttributeImpactCarrier spec)
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
        public ScalableInt MagnitudeOperation;
    
        /// <summary>Optional condition - modifier only applies if tag requirements are met.</summary>
        [SerializeReference]
        public AvoidRequireTagGroup Condition;

        public void Initialize(IAttributeImpactCarrier spec)
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
        
        public float Evaluate(float magnitude, IAttributeImpactCarrier spec)
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
    
    [Serializable]
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

    [Serializable]
    public class EffectImpactConfig : AbstractEffectConfig<AbstractEffectContainer>
    {
        public Attribute AttributeTarget;
        public EAttributeTarget TargetImpact;
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
    
    [Serializable]
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
    
    // [Serializable] on each, as their base is generic: without it Unity dropped a [SerializeReference] list of them
    // whole, so an effect's Stack Behaviours never saved (GAS-70)
    [Serializable]
    public abstract class AbstractStackingConfig : AbstractEffectConfig<AbstractStackingEffectContainer>
    {
        public EEffectStackPolicy? StackPolicy;
        public int? MaxStacks;
        
        public abstract void InitializeContainer(AbstractStackingEffectContainer container);

        /// <summary>
        /// How many stacks to add to the container, given the amount the configs before this one settled on.
        /// </summary>
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

    [Serializable]
    public class StackBoundsConfig : AbstractStackingConfig
    {
        [Tooltip("The fewest stacks the effect holds; a new one starts with these")]
        public int Min;

        [Tooltip("The most stacks the effect holds; an application past them overflows")]
        public int Max;

        [Tooltip("At the most: do nothing more, or start a new effect of its own that stacks again")]
        public EStackBoundsOnMaxPolicy OnMaxPolicy = EStackBoundsOnMaxPolicy.DoNothing;

        /// <summary>
        /// The effects that land, from the application's origin, as a stack would take the effect past <see cref="Max"/>,
        /// as a bleed's fourth stack hemorrhages. A stack past the max did nothing (FEAT-8, Decision 30).
        /// </summary>
        [Tooltip("Effects that land, from the application's origin, as an application would take the effect past its most")]
        public List<GameplayEffect> OverflowEffects = new();

        [Tooltip("Whom its overflow effects land on: the target, as they always did, or the caster (TW-14)")]
        public EEffectRecipient OverflowLandsOn = EEffectRecipient.Target;

        /// <summary>Whether an application that overflows leaves the effect's duration alone, where it would refresh or extend it.</summary>
        [Tooltip("An application that overflows leaves the effect's time as it is, where it would refresh or extend it")]
        public bool DenyRefreshOnOverflow;

        /// <summary>Whether the effect's stacks come off as it overflows, so it starts again from none.</summary>
        [Tooltip("The stacks come off as it overflows, so the effect starts again from none")]
        public bool ClearStacksOnOverflow;

        /// <summary>Whether an amount reaching the bound would take the container past its max.</summary>
        public bool Overflows(AbstractStackingEffectContainer container, int amount) => amount > 0 && container.Stacks + amount > MaxFor(container.Spec);

        /// <summary>The most stacks an application's effect holds: the bound's, and more where its modifiers say (TW-7).</summary>
        public int MaxFor(GameplayEffectSpec spec) => Max + (spec?.Changes?.MostStacks ?? 0);

        public override int ConfigPriority => 999;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            container.Stack(Min);
        }
        
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            int total = Mathf.Min(MaxFor(container.Spec), Mathf.Max(Min, container.Stacks + amount));
            return Mathf.Max(0, total - container.Stacks);
        }
        

        public override AbstractStackingEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            return OnMaxPolicy == EStackBoundsOnMaxPolicy.DoNothing 
                ? base.GetCorrectContainer(spec, ongoing, containers) 
                : containers.FirstOrDefault(c => c.Stacks < MaxFor(c.Spec));
        }
    }

    [Serializable]
    public class StackAmountOperation : AbstractStackingConfig
    {
        [Tooltip("How the scaler sets the stacks an application adds: times the amount, plus it, or in its place")]
        public EMagnitudeOperation Operation;

        [Tooltip("The scaler, read against the effect")]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] [SerializeReference]
        public AbstractScaler Scaler;

        [Tooltip("How a part of a stack becomes a whole one")]
        public ERoundingOperation Rounding;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            
        }
        
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            if (AbstractScaler.IsNone(Scaler)) return amount;
            
            float stacks = Operation switch
            {
                EMagnitudeOperation.MultiplyWithScaler => amount * Scaler.Evaluate(container.Spec),
                EMagnitudeOperation.AddScaler => amount + Scaler.Evaluate(container.Spec),
                EMagnitudeOperation.UseMagnitude => amount,
                EMagnitudeOperation.UseScaler => Scaler.Evaluate(container.Spec),
                _ => throw new ArgumentOutOfRangeException()
            };

            return StackRounding.Whole(stacks, Rounding);
        } 
    }

    // A part of a stack made whole, as a behaviour's rounding says. Round, a half away from zero, came with number fields
    // (NUM-1), and threw where the behaviours made their stacks whole (ED-75)
    internal static class StackRounding
    {
        public static int Whole(float stacks, ERoundingOperation rounding) => rounding switch
        {
            ERoundingOperation.Floor => Mathf.FloorToInt(stacks),
            ERoundingOperation.Ceil => Mathf.CeilToInt(stacks),
            ERoundingOperation.Round => (int)Math.Round(stacks, MidpointRounding.AwayFromZero),
            _ => Mathf.FloorToInt(stacks),
        };
    }

    [Serializable]
    public class InitializationOperation : AbstractStackingConfig
    {
        [Tooltip("The stacks a new effect starts with")]
        public int InitializationStacks = 0;

        [Tooltip("How the scaler sets them: times these, plus them, or in their place")]
        public EMagnitudeOperation Operation;

        [Tooltip("The scaler, read against the effect")]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] [SerializeReference]
        public AbstractScaler Scaler;

        [Tooltip("How a part of a stack becomes a whole one")]
        public ERoundingOperation Rounding;
        
        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            if (AbstractScaler.IsNone(Scaler))
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

            var intStacks = StackRounding.Whole(stacks, Rounding);
            
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
