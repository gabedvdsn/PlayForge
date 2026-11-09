using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class GameplayEffectDuration
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Policy
        // ═══════════════════════════════════════════════════════════════════════════
        
        public EEffectDurationPolicy DurationPolicy;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Duration
        // ═══════════════════════════════════════════════════════════════════════════

        [ScalerOperationKeyword("Duration", Unit = "s")]
        public ScalableFloat DurationOperation;

        public EMagnitudeOperation RealDeltaTime = EMagnitudeOperation.AddScaler;
        [SerializeReference] public AbstractScaler DeltaTimeScaler;

        // ═══════════════════════════════════════════════════════════════════════════
        // Ticks
        // ═══════════════════════════════════════════════════════════════════════════

        [Tooltip("When disabled, the effect will not tick periodically. TickOnApplication is still respected.")]
        public bool EnablePeriodicTicks = false;
        
        [Tooltip("Execute effect impact immediately on application (before any periodic ticks). " +
                 "Enabling this makes a durational effect tick-delivered (Mode B): each tick commits " +
                 "permanently and does NOT auto-reverse on removal unless ReverseImpactOnRemoval is set. " +
                 "Leave disabled for sustained modifiers (auto-reversing buffs).")]
        public bool TickOnApplication = false;

        [ScalerOperationKeyword("Ticks")]
        public ScalableInt TicksOperation;
        
        [ScalerOperationKeyword("Tick Interval", Unit = "s")]
        public ScalableFloat TickIntervalOperation;

        // ═══════════════════════════════════════════════════════════════════════════
        // Execute Ticks
        // ═══════════════════════════════════════════════════════════════════════════

        [ScalerOperationKeyword("Additional Execute Ticks")]
        public ScalableInt AdditionalExecuteTicksOperation;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Re-Application
        // ═══════════════════════════════════════════════════════════════════════════

        public EEffectReApplicationPolicy ReApplicationPolicy = EEffectReApplicationPolicy.AppendNewContainer;
        public EEffectInteractionPolicy ReApplicationInteraction = EEffectInteractionPolicy.DoNothing;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Stacking
        // ═══════════════════════════════════════════════════════════════════════════

        public EEffectStackPolicy StackPolicy;
        
        [SerializeReference] public List<AbstractStackingConfig> StackingConfigs = new();

        [ScalerOperationKeyword("Stack Amount")]
        public ScalableInt StackAmountOperation = new() { Magnitude = 1 };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════
        
        public GameplayEffectDuration() { }
        
        public GameplayEffectDuration(GameplayEffectDuration o)
        {
            DurationPolicy = o.DurationPolicy;
            
            DurationOperation = o.DurationOperation;
            TicksOperation = o.TicksOperation;
            TickIntervalOperation = o.TickIntervalOperation;
            StackAmountOperation = o.StackAmountOperation;
            AdditionalExecuteTicksOperation = o.AdditionalExecuteTicksOperation;
            
            EnablePeriodicTicks = o.EnablePeriodicTicks;
            TickOnApplication = o.TickOnApplication;
            
            RealDeltaTime = o.RealDeltaTime;
            DeltaTimeScaler = o.DeltaTimeScaler;
            
            ReApplicationPolicy = o.ReApplicationPolicy;
            ReApplicationInteraction = o.ReApplicationInteraction;
            StackPolicy = o.StackPolicy;
            
            StackingConfigs = o.StackingConfigs;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Application
        // ═══════════════════════════════════════════════════════════════════════════

        public void ApplyDurationSpecifications(AbstractEffectContainer container)
        {
            // Scalers first: the duration and its ticks are read from them below, and a scaler reads nothing until
            // it's initialized, so an attribute-backed duration came out as 0. The ticks' scaler was never
            // initialized at all (GAS-22)
            DurationOperation?.Scaler?.Initialize(container.Spec);
            DeltaTimeScaler?.Initialize(container.Spec);
            TicksOperation?.Scaler?.Initialize(container.Spec);
            TickIntervalOperation?.Scaler?.Initialize(container.Spec);
            AdditionalExecuteTicksOperation?.Scaler?.Initialize(container.Spec);
            StackAmountOperation?.Scaler?.Initialize(container.Spec);

            if (DurationPolicy == EEffectDurationPolicy.Durational) ApplyDurationalSpecs(container);
            else ApplyInfiniteSpecs(container);

            if (container is AbstractStackingEffectContainer _container)
            {
                foreach (var behaviour in StackingConfigs) behaviour.InitializeContainer(_container);
            }
        }

        private void ApplyDurationalSpecs(AbstractEffectContainer container)
        {
            // Apply duration
            container.SetTotalDuration(GetTotalDuration(container.Spec));
            container.SetDurationRemaining(container.TotalDuration);
            
            // Apply period - only if periodic ticks are enabled
            if (EnablePeriodicTicks)
            {
                int numTicks = GetDurationalTicks(container.Spec);

                if (numTicks > 0)
                {
                    container.SetPeriodDuration(container.TotalDuration / numTicks);
                    container.SetTimeUntilPeriodTick(container.PeriodDuration);
                }
                else
                {
                    container.SetPeriodDuration(float.MaxValue);
                    container.SetTimeUntilPeriodTick(container.PeriodDuration);
                }
            }
            else
            {
                // No periodic ticks - set period to max so it never fires
                container.SetPeriodDuration(float.MaxValue);
                container.SetTimeUntilPeriodTick(float.MaxValue);
            }
        }
        
        private void ApplyInfiniteSpecs(AbstractEffectContainer container)
        {
            container.SetTotalDuration(float.MaxValue);
            container.SetDurationRemaining(container.TotalDuration);
            
            // Through its scaler, which this read past to the bare magnitude (GAS-24)
            float interval = GetTickInterval(container.Spec);
            if (EnablePeriodicTicks && interval > 0)
            {
                container.SetPeriodDuration(interval);
                container.SetTimeUntilPeriodTick(interval);
            }
            else
            {
                // No periodic ticks
                container.SetPeriodDuration(float.MaxValue);
                container.SetTimeUntilPeriodTick(float.MaxValue);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Getters
        // ═══════════════════════════════════════════════════════════════════════════

        public float GetDeltaTime(IAttributeImpactCarrier spec)
        {
            // The target's own time: a slowed entity's effects, its cooldowns among them, last longer (FEAT-7)
            float dt = ForgeTime.DeltaTimeFor(spec?.GetTarget());
            return DeltaTimeScaler is null 
                ? dt 
                : ForgeHelper.MagnitudeAndScalerOperation(dt, DeltaTimeScaler.Evaluate(spec), RealDeltaTime);
        }

        public float GetTotalDuration(IAttributeImpactCarrier spec)
        {
            if (DurationPolicy == EEffectDurationPolicy.Infinite) return float.MaxValue;
            
            return DurationOperation.Scaler is null 
                ? DurationOperation.Magnitude 
                : ForgeHelper.MagnitudeAndScalerOperation(DurationOperation.Magnitude, DurationOperation.Scaler.Evaluate(spec), DurationOperation.RealMagnitude);

        }
        
        public float GetTotalDuration(AbstractStackingEffectContainer container)
        {
            if (DurationPolicy == EEffectDurationPolicy.Infinite) return float.MaxValue;
            
            return DurationOperation.Scaler is null 
                ? DurationOperation.Magnitude 
                : ForgeHelper.MagnitudeAndScalerOperation(DurationOperation.Magnitude, DurationOperation.Scaler.Evaluate(container), DurationOperation.RealMagnitude);
        }

        public float GetTickInterval(IAttributeImpactCarrier spec)
        {
            if (TickIntervalOperation is null) return 0f;

            return TickIntervalOperation.Scaler is null
                ? TickIntervalOperation.Magnitude
                : ForgeHelper.MagnitudeAndScalerOperation(TickIntervalOperation.Magnitude, TickIntervalOperation.Scaler.Evaluate(spec), TickIntervalOperation.RealMagnitude);
        }

        public int GetDurationalTicks(GameplayEffectSpec spec)
        {
            if (DurationPolicy == EEffectDurationPolicy.Infinite) return 0;
            if (!EnablePeriodicTicks) return 0;
            
            if (TicksOperation.Scaler is null)
            {
                return TicksOperation.Magnitude;
            }
            
            float floatTicks = TicksOperation.RealMagnitude switch
            {
                EMagnitudeOperation.MultiplyWithScaler => TicksOperation.Magnitude * TicksOperation.Scaler.Evaluate(spec),
                EMagnitudeOperation.AddScaler => TicksOperation.Magnitude + TicksOperation.Scaler.Evaluate(spec),
                EMagnitudeOperation.UseMagnitude => TicksOperation.Magnitude,
                EMagnitudeOperation.UseScaler => TicksOperation.Scaler.Evaluate(spec),
                _ => throw new ArgumentOutOfRangeException()
            };
            int numTicks = TicksOperation.Rounding switch
            {
                ERoundingOperation.Floor => Mathf.FloorToInt(floatTicks),
                ERoundingOperation.Ceil => Mathf.CeilToInt(floatTicks),
                _ => throw new ArgumentOutOfRangeException()
            };

            return numTicks;
        }

        public int GetExecuteTicks(GameplayEffectSpec spec, int baseExecuteTicks)
        {
            if (AdditionalExecuteTicksOperation.Scaler is null)
            {
                return baseExecuteTicks + AdditionalExecuteTicksOperation.Magnitude;
            }
            
            return ForgeHelper.MagnitudeAndScalerOperation(
                baseExecuteTicks + AdditionalExecuteTicksOperation.Magnitude, 
                AdditionalExecuteTicksOperation.Scaler.Evaluate(spec), 
                AdditionalExecuteTicksOperation.RealMagnitude, 
                AdditionalExecuteTicksOperation.Rounding);
        }

        public int GetStackAmount(GameplayEffectSpec spec)
        {
            return StackAmountOperation.Scaler is null 
                ? StackAmountOperation.Magnitude 
                : ForgeHelper.MagnitudeAndScalerOperation(StackAmountOperation.Magnitude, StackAmountOperation.Scaler.Evaluate(spec), StackAmountOperation.RealMagnitude, StackAmountOperation.Rounding);

        }
        
        public int GetStackAmount(AbstractStackingEffectContainer container) => GetStackAmount(container, out _);

        /// <summary>
        /// The stacks an application adds to the container, and the first stack bound the amount would have taken past
        /// its max, which the bound holds it back from: the effect overflows there (FEAT-8).
        /// </summary>
        public int GetStackAmount(AbstractStackingEffectContainer container, out StackBoundsConfig overflowed)
        {
            overflowed = null;

            // An amount left at 0 means one stack, which is what every application added before amounts counted
            int stacks = StackAmountOperation.Scaler is null
                ? Mathf.Max(1, StackAmountOperation.Magnitude)
                : ForgeHelper.MagnitudeAndScalerOperation(StackAmountOperation.Magnitude, StackAmountOperation.Scaler.Evaluate(container), StackAmountOperation.RealMagnitude, StackAmountOperation.Rounding);

            foreach (var behaviour in ConfigsByPriority())
            {
                if (overflowed is null && behaviour is StackBoundsConfig bound && bound.Overflows(container, stacks)) overflowed = bound;
                stacks = behaviour.GetStacks(container, stacks);
            }

            return stacks;
        }

        public AbstractEffectContainer GenerateContainer(GameplayEffectSpec spec, bool ongoing)
        {
            if (ReApplicationPolicy == EEffectReApplicationPolicy.AppendNewContainer) return NonStackingEffectContainer.Generate(spec, ongoing);
            
            AbstractStackingEffectContainer container = StackPolicy switch
            {
                EEffectStackPolicy.StacksShareOneDuration => SingleDurationEffectContainer.Generate(spec, ongoing),
                EEffectStackPolicy.StacksHaveIndependentDurations => IndependentDurationsContainer.Generate(spec, ongoing),
                EEffectStackPolicy.DurationTakenFromOneStack => StackQueueEffectContainer.Generate(spec, ongoing),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            container.Stack(spec.Base.DurationSpecification.GetStackAmount(container));
            return container;
        }

        // Lowest first, so the highest priority has the last word: a stack bound outranks any amount or choice
        // made before it
        private IEnumerable<AbstractStackingConfig> ConfigsByPriority() => StackingConfigs.OrderBy(sb => sb.ConfigPriority);

        public AbstractStackingEffectContainer FindStackingContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            AbstractStackingEffectContainer container = containers.FirstOrDefault();
            foreach (var sb in ConfigsByPriority())
            {
                container = sb.GetCorrectContainer(spec, ongoing, containers);
            }

            return container;
        }
    }
    
    public enum EEffectDurationPolicy
    {
        Instant,
        Durational,
        Infinite
    }

    public enum ERoundingOperation
    {
        Floor,
        Ceil,

        /// <summary>To the nearest whole number, a half away from zero (FEAT-29).</summary>
        Round
    }
}