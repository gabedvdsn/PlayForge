using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A field shown only where it applies, by what another field says, as Buffer Window under a Queue policy or Proc
    /// Coefficient under Own Proc Coefficient (FN-23): hidden where it doesn't, not disabled, and shown again as its object
    /// changes, an Undo among its changes. A selection whose values differ shows it, as does a condition it can't read.
    /// </summary>
    internal static class ForgeShowWhen
    {
        public const string Hidden = "pf-hidden";

        /// <param name="field">The field to show or hide.</param>
        /// <param name="serialized">The object whose fields say whether it applies.</param>
        /// <param name="applies">Whether it applies, read from the object as it is: null where that can't be told, which shows it.</param>
        public static void Track(VisualElement field, SerializedObject serialized, Func<SerializedObject, bool?> applies)
        {
            if (field == null || serialized == null || applies == null) return;
            void Show()
            {
                bool? shown;
                try
                {
                    if (serialized.targetObject == null) return;
                    shown = applies(serialized);
                }
                catch (Exception)
                {
                    return;
                }
                field.EnableInClassList(Hidden, shown == false);
            }
            field.TrackSerializedObjectValue(serialized, _ => Show());
            Show();
        }

        /// <summary>A toggle's value, or null where a selection's values differ or there's no such field.</summary>
        public static bool? IsOn(SerializedProperty toggle) =>
            toggle == null || toggle.hasMultipleDifferentValues ? null : toggle.boolValue;

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
