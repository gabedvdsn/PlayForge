using System;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Combines multiple scalers with mathematical operations.
    /// Supports Add, Multiply, and Override operations between members.
    /// </summary>
    public class ScalerGroup : AbstractScaler
    {
        [Tooltip("Group members with their operations")]
        public MagnitudeModifierGroupMember[] Calculations = Array.Empty<MagnitudeModifierGroupMember>();
        
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

        public override bool UseScalingOptions()
        {
            return false;
        }

        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (Calculations == null || Calculations.Length == 0)
                return EvaluateFromSpec(spec);
            
            // Check for override members first
            var overrides = Calculations.Where(m => m is { RelativeOperation: ECalculationOperation.Override, Calculation: not null }).ToArray();
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
            foreach (var member in Calculations.Where(m => m is { RelativeOperation: ECalculationOperation.Multiply, Calculation: not null }))
            {
                value *= member.Calculation.Evaluate(spec);
            }
            
            return value;
        }
        
        /// <summary>
        /// Adds a new calculation member to the group.
        /// </summary>
        public void AddMember(AbstractScaler scaler, ECalculationOperation operation)
        {
            var newArray = new MagnitudeModifierGroupMember[Calculations.Length + 1];
            Array.Copy(Calculations, newArray, Calculations.Length);
            newArray[Calculations.Length] = new MagnitudeModifierGroupMember
            {
                Calculation = scaler,
                RelativeOperation = operation
            };
            Calculations = newArray;
        }
    }
    
    [Serializable]
    public struct MagnitudeModifierGroupMember
    {
        [SerializeReference] 
        [Tooltip("The scaler calculation")]
        public AbstractScaler Calculation;
        
        [Tooltip("How this member's result combines with others")]
        public ECalculationOperation RelativeOperation;
    }
    
    public enum EValueCollisionPolicy
    {
        [Tooltip("Use the highest value among conflicting members")]
        UseMaximum,
        
        [Tooltip("Use the lowest value among conflicting members")]
        UseMinimum,
        
        [Tooltip("Average all conflicting member values")]
        UseAverage,
        
        [Tooltip("Use the first matching member")]
        UseFirst,
        
        [Tooltip("Use the last matching member")]
        UseLast
    }
}