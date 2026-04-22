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

        [ScalerOperationKeyword("Duration")]
        public ScalerMagnitudeOperation DurationOperation;

        public EMagnitudeOperation RealDeltaTime = EMagnitudeOperation.AddScaler;
        [SerializeReference] public AbstractScaler DeltaTimeScaler;

        // ═══════════════════════════════════════════════════════════════════════════
        // Ticks
        // ═══════════════════════════════════════════════════════════════════════════

        [Tooltip("When disabled, the effect will not tick periodically. TickOnApplication is still respected.")]
        public bool EnablePeriodicTicks = true;
        
        [Tooltip("Execute effect impact immediately on application (before any periodic ticks).")]
        public bool TickOnApplication;

        [ScalerOperationKeyword("Ticks")]
        public ScalerIntegerMagnitudeOperation TicksOperation;
        
        [ScalerOperationKeyword("Tick Interval")]
        public ScalerMagnitudeOperation TickIntervalOperation;

        // ═══════════════════════════════════════════════════════════════════════════
        // Execute Ticks
        // ═══════════════════════════════════════════════════════════════════════════

        [ScalerOperationKeyword("Additional Execute Ticks")]
        public ScalerIntegerMagnitudeOperation AdditionalExecuteTicksOperation;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Re-Application
        // ═══════════════════════════════════════════════════════════════════════════

        public EEffectReApplicationPolicy ReApplicationPolicy;
        public EEffectInteractionPolicy ReApplicationInteraction = EEffectInteractionPolicy.DoNothing;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Stacking
        // ═══════════════════════════════════════════════════════════════════════════

        public EEffectStackPolicy StackPolicy;
        
        [SerializeReference] public List<AbstractStackingConfig> StackingConfigs = new();

        [ScalerOperationKeyword("Stack Amount")]
        public ScalerIntegerMagnitudeOperation StackAmountOperation;
        
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
            if (DurationPolicy == EEffectDurationPolicy.Durational) ApplyDurationalSpecs(container);
            else ApplyInfiniteSpecs(container);

            DurationOperation?.Scaler?.Initialize(container.Spec);
            DeltaTimeScaler?.Initialize(container.Spec);
            TickIntervalOperation?.Scaler?.Initialize(container.Spec);
            AdditionalExecuteTicksOperation?.Scaler?.Initialize(container.Spec);
            StackAmountOperation?.Scaler?.Initialize(container.Spec);

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
            
            if (EnablePeriodicTicks && TickIntervalOperation.Magnitude > 0)
            {
                container.SetPeriodDuration(TickIntervalOperation.Magnitude);
                container.SetTimeUntilPeriodTick(TickIntervalOperation.Magnitude);
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

        public float GetDeltaTime(IAttributeImpactDerivation spec)
        {
            float dt = Time.deltaTime;
            return DeltaTimeScaler is null 
                ? dt 
                : ForgeHelper.MagnitudeAndScalerOperation(dt, DeltaTimeScaler.Evaluate(spec), RealDeltaTime);
        }

        public float GetTotalDuration(IAttributeImpactDerivation spec)
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
        
        public int GetStackAmount(AbstractStackingEffectContainer container)
        {
            if (StackAmountOperation.Scaler is null) return StackAmountOperation.Magnitude;
            
            int stacks = ForgeHelper.MagnitudeAndScalerOperation(StackAmountOperation.Magnitude, StackAmountOperation.Scaler.Evaluate(container), StackAmountOperation.RealMagnitude, StackAmountOperation.Rounding);
            foreach (var behaviour in StackingConfigs)
            {
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

        public AbstractStackingEffectContainer FindStackingContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            AbstractStackingEffectContainer container = containers.FirstOrDefault();
            var inOrder = StackingConfigs.OrderBy(sb => sb.Priority);
            foreach (var sb in inOrder)
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
        Ceil
    }
}