using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class ScalerMagnitudeOperation
    {
        public float Magnitude;
        public EMagnitudeOperation RealMagnitude = EMagnitudeOperation.AddScaler;
        [SerializeReference] 
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] 
        [DeriveScalerName]
        public AbstractScaler Scaler;

        public float Evaluate(IAttributeImpactDerivation spec)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(spec) ?? 0f, RealMagnitude
            );
        }
    }
    
    [Serializable]
    public class ScalerIntegerMagnitudeOperation
    {
        [Min(0)] public int Magnitude;
        public EMagnitudeOperation RealMagnitude = EMagnitudeOperation.AddScaler;
        [SerializeReference] 
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))] 
        [DeriveScalerName]
        public AbstractScaler Scaler;
        public ERoundingOperation Rounding;

        public int Evaluate(IAttributeImpactDerivation spec)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(spec) ?? 0f, RealMagnitude, Rounding
            );
        }
    }
}
