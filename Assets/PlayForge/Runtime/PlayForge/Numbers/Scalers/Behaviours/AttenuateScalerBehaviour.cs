using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A scaler behaviour that turns the value into a factor, as its <c>Policy</c> says: as it is, one plus it, or one
    /// minus it, so a 30% bonus reads 1.3 and a 30% reduction 0.7.
    /// </summary>
    [ForgeMenu(Description = "Turns the number into a factor: as it is, one plus it, or one minus it.")]
    public class AttenuateScalerBehaviour : AbstractScalerBehaviour
    {
        public EMathApplicationPolicy Policy;
        
        public override void Initialize(IAttributeImpactCarrier spec)
        {
            
        }
        public override float Evaluate(float magnitude, IAttributeImpactCarrier spec)
        {
            return Policy switch
            {

                EMathApplicationPolicy.AsIs => magnitude,
                EMathApplicationPolicy.OnePlus => 1 + magnitude,
                EMathApplicationPolicy.OneMinus => 1 - magnitude,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
