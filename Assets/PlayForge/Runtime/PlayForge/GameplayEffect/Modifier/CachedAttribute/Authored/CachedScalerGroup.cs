using System;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Combines multiple cached scalers with mathematical operations.
    /// Propagates attribute regulation to all members.
    /// </summary>
    public class CachedScalerGroup : AbstractCachedScaler
    {
        [Tooltip("Group members with their operations")]
        public CachedMagnitudeModifierGroupMember[] Calculations = Array.Empty<CachedMagnitudeModifierGroupMember>();
        
        [Tooltip("How to handle multiple Override members")]
        public EValueCollisionPolicy OverrideMemberCollisionPolicy = EValueCollisionPolicy.UseMaximum;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            if (Calculations == null) return;
            
            foreach (var member in Calculations)
            {
                member.Calculation?.Initialize(spec);
            }
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (Calculations == null || Calculations.Length == 0)
                return EvaluateFromSpec(spec);
            
            // Check for override members first
            var overrides = Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Override && m.Calculation != null);
            if (overrides.Any())
            {
                return OverrideMemberCollisionPolicy switch
                {
                    EValueCollisionPolicy.UseMaximum => overrides.Max(m => m.Calculation.Evaluate(spec)),
                    EValueCollisionPolicy.UseMinimum => overrides.Min(m => m.Calculation.Evaluate(spec)),
                    EValueCollisionPolicy.UseAverage => overrides.Average(m => m.Calculation.Evaluate(spec)),
                    EValueCollisionPolicy.UseFirst => overrides.First().Calculation.Evaluate(spec),
                    EValueCollisionPolicy.UseLast => overrides.Last().Calculation.Evaluate(spec),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            
            // Calculate additive sum
            float value = Calculations
                .Where(m => m.RelativeOperation == ECalculationOperation.Add && m.Calculation != null)
                .Sum(member => member.Calculation.Evaluate(spec));
            
            // Apply multipliers
            foreach (var member in Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Multiply && m.Calculation != null))
            {
                value *= member.Calculation.Evaluate(spec);
            }
            
            return value;
        }
        
        public override void Regulate(Attribute attribute, AttributeModificationRule rules)
        {
            if (Calculations == null) return;
            
            foreach (var member in Calculations)
            {
                member.Calculation?.Regulate(attribute, rules);
            }
        }
    }

    [Serializable]
    public struct CachedMagnitudeModifierGroupMember
    {
        [SerializeReference]
        [Tooltip("The cached scaler calculation")]
        public AbstractCachedScaler Calculation;
        
        [Tooltip("How this member's result combines with others")]
        public ECalculationOperation RelativeOperation;
    }
}