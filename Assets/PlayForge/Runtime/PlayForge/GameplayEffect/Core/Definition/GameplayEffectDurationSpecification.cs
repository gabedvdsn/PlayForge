using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class GameplayEffectDurationSpecification
    {
        public EEffectDurationPolicy DurationPolicy;
        public EStackableType StackableType;
        [Tooltip("Naturally increases number of Ticks by 1")]
        public bool TickOnApplication;

        [Space] 
        
        public float Duration;
        [SerializeReference] public AbstractScaler DurationScaler;
        public EMagnitudeOperation RealDuration;
        [ForgeTagContext(ForgeContext.DeltaTimeSource)] public Tag DeltaTimeSource = Tags.DELTA_TIME_DEFAULT;

        [Space] 
        
        public int Ticks;
        public float TickInterval;
        [SerializeReference] public AbstractScaler TickScaler;
        public EMagnitudeOperation RealTicks;
        public ETickCalculationRounding Rounding;

        public GameplayEffectDurationSpecification(GameplayEffectDurationSpecification o)
        {
            DurationPolicy = o.DurationPolicy;
            StackableType = o.StackableType;
            TickOnApplication = o.TickOnApplication;

            Duration = o.Duration;
            DurationScaler = o.DurationScaler;
        }

        public void ApplyDurationSpecifications(AbstractGameplayEffectShelfContainer container)
        {
            if (DurationPolicy == EEffectDurationPolicy.Instant) return;
            
            // Apply duration
            container.SetTotalDuration(GetTotalDuration(container.Spec));
            container.SetDurationRemaining(container.TotalDuration);
            
            // Apply period
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

        public float GetTotalDuration(GameplayEffectSpec spec)
        {
            if (DurationScaler is null)
            {
                return spec.Base.DurationSpecification.Duration;
            }
            
            DurationScaler.Initialize(spec);
            return RealDuration switch
            {
                EMagnitudeOperation.MultiplyWithScaler => Duration * DurationScaler.Evaluate(spec),
                EMagnitudeOperation.AddScaler => Duration + DurationScaler.Evaluate(spec),
                EMagnitudeOperation.UseMagnitude => Duration,
                EMagnitudeOperation.UseScaler => DurationScaler.Evaluate(spec),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public int GetDurationalTicks(GameplayEffectSpec spec)
        {
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
                ETickCalculationRounding.Floor => Mathf.FloorToInt(floatTicks),
                ETickCalculationRounding.Ceil => Mathf.CeilToInt(floatTicks),
                _ => throw new ArgumentOutOfRangeException()
            };

            return numTicks;
        }
        
    }
    
    
    
    public enum EEffectDurationPolicy
    {
        Instant,
        Infinite,
        Durational
    }

    public enum ETickCalculationRounding
    {
        Floor,
        Ceil
    }
}
