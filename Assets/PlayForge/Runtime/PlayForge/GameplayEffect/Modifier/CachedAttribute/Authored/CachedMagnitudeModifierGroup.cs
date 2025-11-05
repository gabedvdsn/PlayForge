using System;
using System.Linq;

namespace FarEmerald.PlayForge
{
    public class CachedMagnitudeModifierGroup : AbstractCachedMagnitudeModifier
    {
        public CachedMagnitudeModifierGroupMember[] Calculations;
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
        public override void Regulate(Attribute attribute, AttributeModificationRule rules)
        {
            foreach (var member in Calculations) member.Calculation.Regulate(attribute, rules); 
        }
    }

    public struct CachedMagnitudeModifierGroupMember
    {
        public AbstractCachedMagnitudeModifier Calculation;
        public ECalculationOperation RelativeOperation;
    }
}
