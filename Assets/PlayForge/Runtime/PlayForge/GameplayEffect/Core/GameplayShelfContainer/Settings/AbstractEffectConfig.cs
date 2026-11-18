using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of an effect's stack behaviours: a Name, and a Priority that orders the behaviours as they apply,
    /// lowest first, so the highest has the last word. A kind can fix its own with <c>ConfigPriority</c>, as Stack
    /// Bounds applies last and Starting Stacks first.
    /// </summary>
    [Serializable]
    public abstract class GameplayPipelineConfiguration
    {
        public string Name;
        public int Priority;
        public virtual int ConfigPriority => Priority;

        [ForgeTagContext(ForgeContext.Granted)]
        public List<Tag> AdditionalGrantedTags;
    }
    
    /// <summary>
    /// A behaviour of an effect's containers of type <c>T</c>: <c>GetCorrectContainer</c> picks, among the target's
    /// containers of the effect, the one an application goes to, the last behaviour by priority having its way. An
    /// effect's stack behaviours derive from it through <c>AbstractStackingConfig</c>.
    /// </summary>
    [Serializable]
    public abstract class AbstractEffectConfig<T> : GameplayPipelineConfiguration where T : AbstractEffectContainer
    {
        public EffectTagRequirements SourceRequirements;
        public EffectTagRequirements TargetRequirements;
        
        
        public abstract T GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, T[] containers);
    }

    // [Serializable] on each, as their base is generic: without it Unity dropped a [SerializeReference] list of them
    // whole, so an effect's Stack Behaviours never saved
    /// <summary>
    /// The base of an effect's Stack Behaviours, the rules its stacking containers follow: Stack Bounds, Scaled Stack
    /// Amount and Starting Stacks. A behaviour of your own derives from it and overrides <c>InitializeContainer</c>, to
    /// set a new container up, and <c>GetStacks</c>, to settle how many stacks an application adds from the amount the
    /// behaviours before it settled on; <c>GetCorrectContainer</c> picks the container an application stacks into, the
    /// first by default.
    /// </summary>
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

    /// <summary>
    /// What Stack Bounds does as an application finds the effect at its Max: Do Nothing, the default, keeps to the one
    /// effect, held at its Max; Append New Container stacks it into a container of the effect under its Max, or else a
    /// new one, which stacks again.
    /// </summary>
    public enum EStackBoundsOnMaxPolicy
    {
        DoNothing,
        AppendNewContainer
    }

    /// <summary>
    /// The stack behaviour Stack Bounds: a Min and a Max of stacks, each read at the effect's level. A new container is
    /// given the Min as it's made, and no application takes the effect past the Max, more where a modifier raises it:
    /// one that would overflows, its Overflow Effects landing from its origin on the target or the caster, as Overflow
    /// Lands On says, with Deny Refresh On Overflow and Clear Stacks On Overflow saying what becomes of the effect's
    /// time and stacks. It applies after the other behaviours, so its bounds have the last word.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "The fewest and the most stacks the effect holds, and what happens at the most.")]
    public class StackBoundsConfig : AbstractStackingConfig
    {
        [Tooltip("The fewest stacks the effect holds; a new one starts with these. Read at the effect's level")]
        [NumberMin(0f)] public ScalableInt Min = 0;

        [Tooltip("The most stacks the effect holds; an application past them overflows. Read at the effect's level")]
        [NumberMin(0f)] public ScalableInt Max = 0;

        [Tooltip("At the most: do nothing more, or start a new effect of its own that stacks again")]
        public EStackBoundsOnMaxPolicy OnMaxPolicy = EStackBoundsOnMaxPolicy.DoNothing;

        /// <summary>
        /// The effects that land, from the application's origin, as a stack would take the effect past <see cref="Max"/>,
        /// as a bleed's fourth stack hemorrhages. A stack past the max did nothing.
        /// </summary>
        [Tooltip("Effects that land, from the application's origin, as an application would take the effect past its most")]
        public List<GameplayEffect> OverflowEffects = new();

        [Tooltip("Whom its overflow effects land on: the target, as they always did, or the caster")]
        public EEffectRecipient OverflowLandsOn = EEffectRecipient.Target;

        /// <summary>Whether an application that overflows leaves the effect's duration alone, where it would refresh or extend it.</summary>
        [Tooltip("An application that overflows leaves the effect's time as it is, where it would refresh or extend it")]
        public bool DenyRefreshOnOverflow;

        /// <summary>Whether the effect's stacks come off as it overflows, so it starts again from none.</summary>
        [Tooltip("The stacks come off as it overflows, so the effect starts again from none")]
        public bool ClearStacksOnOverflow;

        /// <summary>Whether an amount reaching the bound would take the container past its max.</summary>
        public bool Overflows(AbstractStackingEffectContainer container, int amount) => amount > 0 && container.Stacks + amount > MaxFor(container.Spec);

        /// <summary>The most stacks an application's effect holds: the bound's, and more where its modifiers say.</summary>
        public int MaxFor(GameplayEffectSpec spec) => (Max?.Evaluate(spec) ?? 0) + (spec?.Changes?.MostStacks ?? 0);

        public override int ConfigPriority => 999;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            container.Stack(Min?.Evaluate(container.Spec) ?? 0);
        }
        
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            int total = Mathf.Min(MaxFor(container.Spec), Mathf.Max(Min?.Evaluate(container.Spec) ?? 0, container.Stacks + amount));
            return Mathf.Max(0, total - container.Stacks);
        }
        

        public override AbstractStackingEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            return OnMaxPolicy == EStackBoundsOnMaxPolicy.DoNothing 
                ? base.GetCorrectContainer(spec, ongoing, containers) 
                : containers.FirstOrDefault(c => c.Stacks < MaxFor(c.Spec));
        }
    }

    /// <summary>
    /// The stack behaviour Scaled Stack Amount: the stacks each application adds, the amount the behaviours before it
    /// settled on, from the effect's Stack Amount, combined with its Scaler, read against the effect, as its Operation
    /// says, and made whole by its Rounding. With no scaler, the amount stands.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "How many stacks an application adds.")]
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

    // A part of a stack made whole, as a behaviour's rounding says. Round, a half away from zero, came with number fields,
    // and threw where the behaviours made their stacks whole
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

    /// <summary>
    /// The stack behaviour Starting Stacks: the stacks a new container of the effect is given as it's made, a number
    /// read at the effect's level; the application that makes it adds its own amount after. It applies first of the
    /// behaviours, and leaves the amount an application adds as it is.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "The stacks a new effect starts with.")]
    public class InitializationOperation : AbstractStackingConfig
    {
        /// <summary>
        /// The stacks a new effect starts with: a number of any kind, read at the effect's level, as its other numbers are.
        /// Its own number, scaler, combine and rounding were a Scalable whole number written out.
        /// </summary>
        [Tooltip("The stacks a new effect starts with, read at the effect's level")]
        [NumberMin(0f)] public ScalableInt InitializationStacks = 0;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            container.Stack(InitializationStacks?.Evaluate(container.Spec) ?? 0);
        }

        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            return amount;
        }
        public override int ConfigPriority => -1;
    }

    // The other intrinsic behaviours...

}
