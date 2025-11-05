using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class MagnitudeModifierGroup : AbstractMagnitudeModifier
    {
        public MagnitudeModifierGroupMember[] Calculations;
        public EValueCollisionPolicy OverrideMemberCollisionPolicy;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            foreach (var member in Calculations) member.Calculation.Initialize(spec);
        }
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (Calculations.Any(m => m.RelativeOperation == ECalculationOperation.Override))
            {
                return OverrideMemberCollisionPolicy switch
                {
                    EValueCollisionPolicy.UseMaximum => Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Override)
                        .Max(m => m.Calculation.Evaluate(spec)),
                    EValueCollisionPolicy.UseMinimum => Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Override)
                        .Min(m => m.Calculation.Evaluate(spec)),
                    EValueCollisionPolicy.UseAverage => Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Override)
                        .Average(m => m.Calculation.Evaluate(spec)),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
                
            float value = Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Add).Sum(member => member.Calculation.Evaluate(spec));
            return Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Multiply).Aggregate(value, (current, member) => current * member.Calculation.Evaluate(spec));
        }
    }
    
    public struct MagnitudeModifierGroupMember
    {
        public AbstractMagnitudeModifier Calculation;
        public ECalculationOperation RelativeOperation;
    }
}
