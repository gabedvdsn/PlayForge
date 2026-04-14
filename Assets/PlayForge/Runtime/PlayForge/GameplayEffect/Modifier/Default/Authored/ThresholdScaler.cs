using System;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Returns values based on level thresholds.
    /// Useful for tier-based progression (e.g., Bronze/Silver/Gold ranks).
    /// </summary>
    public class ThresholdScaler : AbstractScaler
    {
        [Tooltip("Level thresholds and their corresponding values")]
        public LevelThreshold[] Thresholds = new LevelThreshold[]
        {
            new LevelThreshold { Level = 1, Value = 1f },
            new LevelThreshold { Level = 10, Value = 2f },
            new LevelThreshold { Level = 20, Value = 3f }
        };
        
        [Tooltip("Value to use if below all thresholds")]
        public float DefaultValue = 1f;
        
        public override void Initialize(IAttributeImpactDerivation spec)
        {
            // No initialization needed
        }
        
        public override float Evaluate(IAttributeImpactDerivation spec)
        {
            if (Thresholds == null || Thresholds.Length == 0)
                return DefaultValue;
            
            int currentLevel = spec.GetEffectDerivation().GetLevel();
            
            // Find the highest threshold that is <= current level
            float result = DefaultValue;
            int highestMatchedLevel = 0;
            
            foreach (var threshold in Thresholds)
            {
                if (currentLevel >= threshold.Level && threshold.Level > highestMatchedLevel)
                {
                    highestMatchedLevel = threshold.Level;
                    result = threshold.Value;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Sets up evenly distributed thresholds.
        /// </summary>
        public void SetupEvenThresholds(int tierCount, float startValue, float endValue)
        {
            if (tierCount < 2) tierCount = 2;
            
            Thresholds = new LevelThreshold[tierCount];
            int levelsPerTier = MaxLevel / tierCount;
            
            for (int i = 0; i < tierCount; i++)
            {
                float t = (float)i / (tierCount - 1);
                Thresholds[i] = new LevelThreshold
                {
                    Level = 1 + (i * levelsPerTier),
                    Value = Mathf.Lerp(startValue, endValue, t)
                };
            }
        }
    }
    
    [Serializable]
    public struct LevelThreshold
    {
        [Tooltip("Minimum level for this threshold")]
        public int Level;
        
        [Tooltip("Value when at or above this level")]
        public float Value;
        
        [Tooltip("Optional name for this tier (e.g., 'Bronze', 'Silver')")]
        public string TierName;
    }
}