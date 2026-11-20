using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    // A chance's checks, which read it as the runtime reads a Scalable number
    internal static partial class ForgeShowWhen
    {
        /// <summary>
        /// Whether a chance can't miss: a plain number of 1 or more, read as the runtime reads it, so its roll doesn't
        /// matter; null where a selection's values differ or there's no such field.
        /// </summary>
        public static bool? CantMiss(SerializedProperty chance)
        {
            if (chance == null || chance.hasMultipleDifferentValues) return null;
            var magnitude = chance.FindPropertyRelative(nameof(ScalableFloat.Magnitude));
            var combine = chance.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
            var scaler = chance.FindPropertyRelative(nameof(ScalableFloat.Scaler));
            if (magnitude == null || combine == null || scaler == null) return null;
            var number = new ScalableFloat
            {
                Magnitude = magnitude.floatValue,
                RealMagnitude = (EMagnitudeOperation)combine.intValue,
                Scaler = scaler.managedReferenceValue as AbstractScaler,
            };
            return number.IsConstant && number.Constant >= 1f;
        }

        /// <summary>Whether a chance's roll matters: it can miss. Null where that can't be told, which shows the roll.</summary>
        public static bool? Rolls(SerializedProperty chance) => CantMiss(chance) is { } sure ? !sure : null;
    }
}
