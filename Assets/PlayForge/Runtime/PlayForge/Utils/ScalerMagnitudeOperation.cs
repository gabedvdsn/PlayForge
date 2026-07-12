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

        public float Evaluate(IAttributeImpactCarrier spec)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(spec) ?? 0f, RealMagnitude
            );
        }

        /// <summary>
        /// Container-aware evaluation — scalers that read container state (stack count)
        /// see the live container instead of just the spec. Scalers that don't care fall
        /// back to spec-based evaluation via <see cref="AbstractScaler.Evaluate(AbstractStackingEffectContainer)"/>.
        /// </summary>
        public float Evaluate(AbstractStackingEffectContainer container)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(container) ?? 0f, RealMagnitude
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

        public int Evaluate(IAttributeImpactCarrier spec)
        {
            return ForgeHelper.MagnitudeAndScalerOperation(
                Magnitude, Scaler?.Evaluate(spec) ?? 0f, RealMagnitude, Rounding
            );
        }
    }
}
