using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A reusable scaler configuration that can be shared across multiple assets.
    /// 
    /// Usage:
    /// 1. Create a ScalerTemplate asset (Create > PlayForge > Templates > Scaler Template)
    /// 2. Configure the scaler values in the template
    /// 3. Reference this template from any scaler field using the "Link Template" button
    /// 4. Changes to the template will propagate to all linked scalers when they sync
    /// 
    /// Note: Linked scalers can "break" the link to make local modifications.
    /// </summary>
    [CreateAssetMenu(fileName = "New Scaler Template", menuName = "PlayForge/Templates/Scaler Template", order = 100)]
    public class ScalerTemplate : ScriptableObject
    {
        [Tooltip("Description of what this scaler template is used for")]
        [TextArea(2, 4)]
        public string Description;
        
        [Tooltip("Category for organizing templates in the import window")]
        public string Category = "General";
        
        [Tooltip("The scaler configuration stored in this template")]
        [SerializeReference]
        public AbstractScaler Scaler;
        
        /// <summary>
        /// Gets a display name for this template.
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(Scaler?.Name) ? Scaler.Name : name;
        
        /// <summary>
        /// Copies values from this template to a target scaler.
        /// </summary>
        public void CopyTo(AbstractScaler target)
        {
            if (Scaler == null || target == null) return;
            
            target.Name = Scaler.Name;
            target.Configuration = Scaler.Configuration;
            target.MaxLevel = Scaler.MaxLevel;
            target.Interpolation = Scaler.Interpolation;
            
            // Deep copy level values
            if (Scaler.LevelValues != null)
            {
                target.LevelValues = new float[Scaler.LevelValues.Length];
                Array.Copy(Scaler.LevelValues, target.LevelValues, Scaler.LevelValues.Length);
            }
            
            // Deep copy scaling curve
            if (Scaler.Scaling != null)
            {
                target.Scaling = new AnimationCurve(Scaler.Scaling.keys);
            }
            
            // Note: Behaviours are not copied as they may have asset-specific references
        }
        
        /// <summary>
        /// Checks if a scaler's core values match this template.
        /// </summary>
        public bool MatchesTemplate(AbstractScaler other)
        {
            if (Scaler == null || other == null) return false;
            
            if (Scaler.Configuration != other.Configuration) return false;
            if (Scaler.MaxLevel != other.MaxLevel) return false;
            if (Scaler.Interpolation != other.Interpolation) return false;
            
            if (Scaler.LevelValues == null && other.LevelValues == null) return true;
            if (Scaler.LevelValues == null || other.LevelValues == null) return false;
            if (Scaler.LevelValues.Length != other.LevelValues.Length) return false;
            
            for (int i = 0; i < Scaler.LevelValues.Length; i++)
            {
                if (Math.Abs(Scaler.LevelValues[i] - other.LevelValues[i]) > 0.0001f)
                    return false;
            }
            
            return true;
        }
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure the scaler's curve is regenerated when template is modified
            if (Scaler != null)
            {
                Scaler.RegenerateCurve();
            }
        }
#endif
    }
}