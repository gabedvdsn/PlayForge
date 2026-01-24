using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class GameplayEffectDurationSpecification
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Policy
        // ═══════════════════════════════════════════════════════════════════════════
        
        public EEffectDurationPolicy DurationPolicy;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Duration
        // ═══════════════════════════════════════════════════════════════════════════

        public float Duration;
        [SerializeReference] public AbstractScaler DurationScaler;
        public EMagnitudeOperation RealDuration = EMagnitudeOperation.AddScaler;

        public EMagnitudeOperation RealDeltaTime = EMagnitudeOperation.AddScaler;
        [SerializeReference] public AbstractScaler DeltaTimeScaler;

        // ═══════════════════════════════════════════════════════════════════════════
        // Ticks
        // ═══════════════════════════════════════════════════════════════════════════

        [Tooltip("When disabled, the effect will not tick periodically. TickOnApplication is still respected.")]
        public bool EnablePeriodicTicks = true;
        
        [Tooltip("Execute effect impact immediately on application (before any periodic ticks).")]
        public bool TickOnApplication;

        public int Ticks;
        public float TickInterval;
        
        [SerializeReference] public AbstractScaler TickScaler;
        public EMagnitudeOperation RealTicks = EMagnitudeOperation.AddScaler;
        public ERoundingOperation Rounding = ERoundingOperation.Ceil;

        // ═══════════════════════════════════════════════════════════════════════════
        // Execute Ticks
        // ═══════════════════════════════════════════════════════════════════════════

        public int AdditionalExecuteTicks = 0;
        [SerializeReference] public AbstractScaler ExecuteTicksScaler;
        public EMagnitudeOperation RealExecuteTicks = EMagnitudeOperation.UseMagnitude;
        public ERoundingOperation ExecuteTicksRounding = ERoundingOperation.Ceil;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Re-Application
        // ═══════════════════════════════════════════════════════════════════════════

        public EEffectReApplicationPolicy ReApplicationPolicy;
        public EEffectInteractionPolicy ReApplicationInteraction = EEffectInteractionPolicy.DoNothing;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Stacking
        // ═══════════════════════════════════════════════════════════════════════════

        public EEffectStackPolicy StackPolicy;
        
        [SerializeReference] public List<AbstractStackingBehaviour> StackingBehaviours = new();

        [Min(0)] public int StackAmount = 1;
        [SerializeReference] public AbstractScaler StackAmountScaler;
        public EMagnitudeOperation RealStackAmount = EMagnitudeOperation.UseMagnitude;
        public ERoundingOperation StackAmountRounding = ERoundingOperation.Ceil;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════
        
        public GameplayEffectDurationSpecification() { }
        
        public GameplayEffectDurationSpecification(GameplayEffectDurationSpecification o)
        {
            DurationPolicy = o.DurationPolicy;
            
            Duration = o.Duration;
            DurationScaler = o.DurationScaler;
            RealDuration = o.RealDuration;
            RealDeltaTime = o.RealDeltaTime;
            DeltaTimeScaler = o.DeltaTimeScaler;
            
            EnablePeriodicTicks = o.EnablePeriodicTicks;
            TickOnApplication = o.TickOnApplication;
            Ticks = o.Ticks;
            TickInterval = o.TickInterval;
            TickScaler = o.TickScaler;
            RealTicks = o.RealTicks;
            Rounding = o.Rounding;
            
            AdditionalExecuteTicks = o.AdditionalExecuteTicks;
            ExecuteTicksScaler = o.ExecuteTicksScaler;
            RealExecuteTicks = o.RealExecuteTicks;
            ExecuteTicksRounding = o.ExecuteTicksRounding;
            
            ReApplicationPolicy = o.ReApplicationPolicy;
            ReApplicationInteraction = o.ReApplicationInteraction;
            
            StackPolicy = o.StackPolicy;
            StackingBehaviours = o.StackingBehaviours;
            StackAmount = o.StackAmount;
            StackAmountScaler = o.StackAmountScaler;
            RealStackAmount = o.RealStackAmount;
            StackAmountRounding = o.StackAmountRounding;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Application
        // ═══════════════════════════════════════════════════════════════════════════

        public void ApplyDurationSpecifications(AbstractEffectContainer container)
        {
            if (DurationPolicy == EEffectDurationPolicy.Durational) ApplyDurationalSpecs(container);
            else ApplyInfiniteSpecs(container);
            
            if (DeltaTimeScaler is not null) DeltaTimeScaler.Initialize(container.Spec);
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
            
            if (EnablePeriodicTicks && TickInterval > 0)
            {
                container.SetPeriodDuration(TickInterval);
                container.SetTimeUntilPeriodTick(TickInterval);
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
            
            if (DurationScaler is null) return Duration;
            
            DurationScaler.Initialize(spec);

            return ForgeHelper.MagnitudeAndScalerOperation(Duration, DurationScaler.Evaluate(spec), RealDuration);
        }
        
        public float GetTotalDuration(AbstractStackingEffectContainer container)
        {
            if (DurationPolicy == EEffectDurationPolicy.Infinite) return float.MaxValue;
            
            if (DurationScaler is null) return Duration;
            
            DurationScaler.Initialize(container);

            return ForgeHelper.MagnitudeAndScalerOperation(Duration, DurationScaler.Evaluate(container), RealDuration);
        }

        public int GetDurationalTicks(GameplayEffectSpec spec)
        {
            if (DurationPolicy == EEffectDurationPolicy.Infinite) return 0;
            if (!EnablePeriodicTicks) return 0;
            
            if (TickScaler is null)
            {
                return Ticks;
            }
            
            TickScaler.Initialize(spec);
            
            float floatTicks = RealTicks switch
            {
                EMagnitudeOperation.MultiplyWithScaler => Ticks * TickScaler.Evaluate(spec),
                EMagnitudeOperation.AddScaler => Ticks + TickScaler.Evaluate(spec),
                EMagnitudeOperation.UseMagnitude => Ticks,
                EMagnitudeOperation.UseScaler => TickScaler.Evaluate(spec),
                _ => throw new ArgumentOutOfRangeException()
            };
            int numTicks = Rounding switch
            {
                ERoundingOperation.Floor => Mathf.FloorToInt(floatTicks),
                ERoundingOperation.Ceil => Mathf.CeilToInt(floatTicks),
                _ => throw new ArgumentOutOfRangeException()
            };

            return numTicks;
        }

        public int GetExecuteTicks(GameplayEffectSpec spec, int ticks)
        {
            if (ExecuteTicksScaler is null)
            {
                return ticks + AdditionalExecuteTicks;
            }

            ExecuteTicksScaler.Initialize(spec);

            return ForgeHelper.MagnitudeAndScalerOperation(ticks + AdditionalExecuteTicks, ExecuteTicksScaler.Evaluate(spec), RealExecuteTicks, ExecuteTicksRounding);
        }

        public int GetStackAmount(GameplayEffectSpec spec)
        {
            if (StackAmountScaler is null) return StackAmount;

            StackAmountScaler.Initialize(spec);

            return ForgeHelper.MagnitudeAndScalerOperation(StackAmount, StackAmountScaler.Evaluate(spec), RealStackAmount, StackAmountRounding);
        }
        
        public int GetStackAmount(AbstractStackingEffectContainer container)
        {
            if (StackAmountScaler is null) return StackAmount;

            StackAmountScaler.Initialize(container);

            return ForgeHelper.MagnitudeAndScalerOperation(StackAmount, StackAmountScaler.Evaluate(container), RealStackAmount, StackAmountRounding);
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
            var inOrder = StackingBehaviours.OrderBy(sb => sb.Order);
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