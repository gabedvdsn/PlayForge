using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A magnitude the caller sets under a tag as it applies the effect, as Unreal's SetByCaller is: on the spec, or on an
    /// ability activation's data, which each effect the ability applies takes as it's made. Where none is set, it reads its
    /// Default. An effect's amount came only from its asset and its scalers, so code that knew the amount as it applied
    /// the effect couldn't pass it in (FEAT-2, PF-C01).
    /// </summary>
    [Serializable]
    public class SetByCallerScaler : AbstractScaler
    {
        [Tooltip("The tag the caller sets the magnitude under")]
        public Tag Key;

        [Tooltip("What the scaler reads where the caller set nothing under the tag")]
        public float Default;

        // It reads what the caller set, or its default (ED-51)
        public override EScalerParts Parts => EScalerParts.None;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
        }

        public override float Evaluate(IAttributeImpactCarrier deriv) =>
            deriv is GameplayEffectSpec spec && spec.TryGetSetByCaller(Key, out float magnitude) ? magnitude : Default;
    }
}
