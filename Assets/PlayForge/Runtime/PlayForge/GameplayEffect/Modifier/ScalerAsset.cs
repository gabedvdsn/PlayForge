using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A scaler kept as an asset of its own, which scaler fields reference through a <see cref="SharedScaler"/>. Editing
    /// it changes every field that uses it, so nothing propagates (ED-33, Decision 28); a field adjusts it only through
    /// its own Magnitude, as in Fire Duration × 1.5. It was the Scaler Template, whose link code had no callers, so reuse
    /// meant copying a scaler into each field and propagating edits by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "New Scaler", menuName = "PlayForge/Scaler", order = 100)]
    public class ScalerAsset : ScriptableObject
    {
        [Tooltip("What the scaler is for")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("A group for the import window's list")]
        public string Category = "General";

        [Tooltip("The scaler each field that uses this asset reads")]
        [SerializeReference]
        public AbstractScaler Scaler;

        /// <summary>The asset's name in lists: its scaler's Name, or the file name.</summary>
        public string DisplayName => !string.IsNullOrEmpty(Scaler?.Name) ? Scaler.Name : name;

        /// <summary>Copies the scaler's values into another scaler, as Import Values does.</summary>
        public void CopyTo(AbstractScaler target)
        {
            if (Scaler == null || target == null) return;

            target.Name = Scaler.Name;
            target.Configuration = Scaler.Configuration;
            target.MaxLevel = Scaler.MaxLevel;
            target.Interpolation = Scaler.Interpolation;
            target.StretchOverLevels = Scaler.StretchOverLevels;

            if (Scaler.LevelValues != null)
            {
                target.LevelValues = new float[Scaler.LevelValues.Length];
                Array.Copy(Scaler.LevelValues, target.LevelValues, Scaler.LevelValues.Length);
            }

            if (Scaler.Scaling != null)
            {
                target.Scaling = new AnimationCurve(Scaler.Scaling.keys);
            }

            // Behaviours are not copied, as they may hold references of the asset's own
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // A stretched scaler reads its curve, which follows its values
            if (Scaler != null)
            {
                Scaler.RegenerateCurve();
            }
        }
#endif
    }
}
