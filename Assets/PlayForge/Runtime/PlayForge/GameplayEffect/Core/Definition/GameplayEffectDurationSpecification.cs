using System;
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
        public AbstractMagnitudeModifier DurationCalculation;
        public EMagnitudeOperation DurationCalculationOperation;
        public Tag DeltaTimeSource = Tags.DELTA_TIME_DEFAULT;

        [Space] 
        
        public int Ticks;
        public AbstractMagnitudeModifier TickCalculation;
        public EMagnitudeOperation TickCalculationOperation;
        public ETickCalculationRounding Rounding;

        public void ApplyDurationSpecifications(AbstractGameplayEffectShelfContainer container)
        {
            if (DurationPolicy == EEffectDurationPolicy.Instant) return;
            
            // Apply duration
            container.SetTotalDuration(GetTotalDuration(container.Spec));
            container.SetDurationRemaining(container.TotalDuration);
            
            // Apply period
            int numTicks = GetTicks(container.Spec);

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
            DurationCalculation.Initialize(spec);
            return DurationCalculationOperation switch
            {
                EMagnitudeOperation.Multiply => Duration * DurationCalculation.Evaluate(spec),
                EMagnitudeOperation.Add => Duration + DurationCalculation.Evaluate(spec),
                EMagnitudeOperation.UseMagnitude => Duration,
                EMagnitudeOperation.UseCalculation => DurationCalculation.Evaluate(spec),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public int GetTicks(GameplayEffectSpec spec)
        {
            TickCalculation.Initialize(spec);
            
            float floatTicks = TickCalculationOperation switch
            {
                EMagnitudeOperation.Multiply => Ticks * TickCalculation.Evaluate(spec),
                EMagnitudeOperation.Add => Ticks + TickCalculation.Evaluate(spec),
                EMagnitudeOperation.UseMagnitude => Ticks,
                EMagnitudeOperation.UseCalculation => TickCalculation.Evaluate(spec),
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
