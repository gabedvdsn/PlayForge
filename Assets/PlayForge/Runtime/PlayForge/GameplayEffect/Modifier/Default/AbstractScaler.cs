using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Base class for all magnitude scalers.
    /// Scalers determine how a value changes based on level (relative level = (level-1)/(maxLevel-1)).
    /// 
    /// Level Configuration Modes:
    /// - LockToSource: Automatically uses the owning Ability/Entity's level range. No extra setup needed.
    ///   The source is determined at runtime from the IAttributeImpactDerivation spec.
    /// - Unlocked: Uses its own MaxLevel setting, independent of the source.
    /// - Partitioned: Uses the minimum of MaxLevel and the source's current level.
    /// </summary>
    [Serializable]
    public abstract class AbstractScaler : IScaler
    {
        [Tooltip("Optional name for this scaler (helps identify when importing or viewing)")]
        public string Name;
        
        [Header("Template Link")]
        [Tooltip("Optional: Link to a shared ScalerTemplate. When linked, this scaler can sync its values from the template.")]
        public ScalerTemplate SourceTemplate;
        
        [Tooltip("When enabled, this scaler's values will be kept in sync with the linked template.\n" +
                 "Disable to make local modifications while keeping the template reference.")]
        public bool SyncWithTemplate;
        
        [Header("Configuration")]
        [Tooltip("How this scaler determines its level range.\n\n" +
                 "LockToSource: Uses the owning Ability/Entity's level range automatically.\n" +
                 "Unlocked: Uses its own MaxLevel setting.\n" +
                 "Partitioned: Uses min(MaxLevel, source's current level).")]
        public ELevelConfig Configuration = ELevelConfig.LockToLevelProvider;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Name Properties
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if this scaler has a custom name set.
        /// </summary>
        public bool HasName => !string.IsNullOrEmpty(Name);
        
        /// <summary>
        /// Gets the display name - returns Name if set, empty string otherwise.
        /// </summary>
        public string GetDisplayName() => HasName ? Name : "";
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Template Properties
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if this scaler is linked to a template.
        /// </summary>
        public bool IsLinkedToTemplate => SourceTemplate != null;
        
        /// <summary>
        /// Returns true if this scaler should sync from its template.
        /// </summary>
        public bool ShouldSyncFromTemplate => IsLinkedToTemplate && SyncWithTemplate;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Template Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Syncs values from the linked template if SyncWithTemplate is enabled.
        /// Call this in OnValidate or when needed.
        /// </summary>
        public void TrySyncFromTemplate()
        {
            if (ShouldSyncFromTemplate)
            {
                SourceTemplate.CopyTo(this);
            }
        }
        
        /// <summary>
        /// Links this scaler to a template and optionally syncs immediately.
        /// </summary>
        public void LinkToTemplate(ScalerTemplate template, bool syncImmediately = true)
        {
            SourceTemplate = template;
            SyncWithTemplate = true;
            if (syncImmediately && template != null)
            {
                template.CopyTo(this);
            }
        }
        
        /// <summary>
        /// Unlinks from the current template, keeping current values.
        /// </summary>
        public void UnlinkFromTemplate()
        {
            SourceTemplate = null;
            SyncWithTemplate = false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Scaler Fields
        // ═══════════════════════════════════════════════════════════════════════════
        
        [Tooltip("Maximum level for Unlocked/Partitioned modes.\n" +
                 "Ignored when using LockToSource (uses source's max level instead).")]
        [Min(1)]
        public int MaxLevel = 20;
        
        [Tooltip("Value at each level (index 0 = level 1)")]
        public float[] LevelValues = new float[] { 1f };
        
        [Tooltip("Interpolation between level values")]
        public EScalerInterpolation Interpolation = EScalerInterpolation.Linear;
        
        [Tooltip("Generated curve from level values (auto-updated)")]
        public AnimationCurve Scaling = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [SerializeReference] public List<AbstractScalerBehaviour> Behaviours = new();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        public abstract void Initialize(IAttributeImpactDerivation spec);
        public abstract float Evaluate(IAttributeImpactDerivation spec);

        public virtual void Initialize(AbstractStackingEffectContainer container) => Initialize(container.Spec);
        public virtual float Evaluate(AbstractStackingEffectContainer container) => Evaluate(container.Spec);

        public virtual bool UseScalingOptions() => true;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Level Value Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Gets the value for a specific level (1-indexed).
        /// </summary>
        public float GetValueAtLevel(int level)
        {
            if (LevelValues == null || LevelValues.Length == 0)
                return 1f;
            
            int index = Mathf.Clamp(level - 1, 0, LevelValues.Length - 1);
            return LevelValues[index];
        }
        
        /// <summary>
        /// Sets the value for a specific level (1-indexed).
        /// </summary>
        public void SetValueAtLevel(int level, float value)
        {
            if (LevelValues == null)
                LevelValues = new float[MaxLevel];
            
            int index = level - 1;
            if (index >= 0 && index < LevelValues.Length)
            {
                LevelValues[index] = value;
            }
        }
        
        /// <summary>
        /// Resizes the level values array to match MaxLevel.
        /// Preserves existing values where possible.
        /// </summary>
        public void ResizeLevelValues()
        {
            int targetSize = Mathf.Max(1, MaxLevel);
            
            if (LevelValues == null)
            {
                LevelValues = new float[targetSize];
                for (int i = 0; i < targetSize; i++)
                    LevelValues[i] = 1f;
                return;
            }
            
            if (LevelValues.Length == targetSize)
                return;
            
            float[] newValues = new float[targetSize];
            
            // Copy existing values
            int copyCount = Mathf.Min(LevelValues.Length, targetSize);
            Array.Copy(LevelValues, newValues, copyCount);
            
            // Fill new entries with last value or 1
            float fillValue = LevelValues.Length > 0 ? LevelValues[LevelValues.Length - 1] : 1f;
            for (int i = copyCount; i < targetSize; i++)
            {
                newValues[i] = fillValue;
            }
            
            LevelValues = newValues;
        }
        
        /// <summary>
        /// Regenerates the AnimationCurve from LevelValues array.
        /// </summary>
        public void RegenerateCurve()
        {
            if (LevelValues == null || LevelValues.Length == 0)
            {
                Scaling = AnimationCurve.Linear(0f, 1f, 1f, 1f);
                return;
            }
            
            Scaling = new AnimationCurve();
            
            for (int i = 0; i < LevelValues.Length; i++)
            {
                // Convert level index to relative level (0 to 1)
                float t = LevelValues.Length > 1 ? (float)i / (LevelValues.Length - 1) : 0f;
                float value = LevelValues[i];
                
                Keyframe keyframe = new Keyframe(t, value);
                
                // Set tangent mode based on interpolation
                switch (Interpolation)
                {
                    case EScalerInterpolation.Constant:
                        keyframe.inTangent = 0f;
                        keyframe.outTangent = 0f;
                        keyframe.inWeight = 0f;
                        keyframe.outWeight = 0f;
                        break;
                    case EScalerInterpolation.Linear:
                        // Unity will auto-calculate linear tangents
                        break;
                    case EScalerInterpolation.Smooth:
                        // Auto-smooth tangents (handled after adding keys)
                        break;
                }
                
                Scaling.AddKey(keyframe);
            }
            
            // Apply tangent modes
            for (int i = 0; i < Scaling.length; i++)
            {
                switch (Interpolation)
                {
                    case EScalerInterpolation.Constant:
                        AnimationUtility.SetKeyLeftTangentMode(Scaling, i, UnityEditor.AnimationUtility.TangentMode.Constant);
                        AnimationUtility.SetKeyRightTangentMode(Scaling, i, UnityEditor.AnimationUtility.TangentMode.Constant);
                        break;
                    case EScalerInterpolation.Linear:
                        AnimationUtility.SetKeyLeftTangentMode(Scaling, i, UnityEditor.AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(Scaling, i, UnityEditor.AnimationUtility.TangentMode.Linear);
                        break;
                    case EScalerInterpolation.Smooth:
                        AnimationUtility.SetKeyLeftTangentMode(Scaling, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(Scaling, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Populates LevelValues from the existing AnimationCurve.
        /// </summary>
        public void PopulateFromCurve()
        {
            if (Scaling == null || Scaling.length == 0)
                return;
            
            ResizeLevelValues();
            
            for (int i = 0; i < LevelValues.Length; i++)
            {
                float t = LevelValues.Length > 1 ? (float)i / (LevelValues.Length - 1) : 0f;
                LevelValues[i] = Scaling.Evaluate(t);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Quick Fill Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Fills all level values with a constant.
        /// </summary>
        public void FillConstant(float value)
        {
            ResizeLevelValues();
            for (int i = 0; i < LevelValues.Length; i++)
                LevelValues[i] = value;
            RegenerateCurve();
        }
        
        /// <summary>
        /// Fills level values with linear interpolation from start to end.
        /// </summary>
        public void FillLinear(float startValue, float endValue)
        {
            ResizeLevelValues();
            for (int i = 0; i < LevelValues.Length; i++)
            {
                float t = LevelValues.Length > 1 ? (float)i / (LevelValues.Length - 1) : 0f;
                LevelValues[i] = Mathf.Lerp(startValue, endValue, t);
            }
            RegenerateCurve();
        }
        
        /// <summary>
        /// Fills level values with exponential curve.
        /// </summary>
        public void FillExponential(float startValue, float endValue, float exponent = 2f)
        {
            ResizeLevelValues();
            for (int i = 0; i < LevelValues.Length; i++)
            {
                float t = LevelValues.Length > 1 ? (float)i / (LevelValues.Length - 1) : 0f;
                float curved = Mathf.Pow(t, exponent);
                LevelValues[i] = Mathf.Lerp(startValue, endValue, curved);
            }
            RegenerateCurve();
        }
        
        /// <summary>
        /// Fills level values with logarithmic curve (fast start, slow end).
        /// </summary>
        public void FillLogarithmic(float startValue, float endValue)
        {
            ResizeLevelValues();
            for (int i = 0; i < LevelValues.Length; i++)
            {
                float t = LevelValues.Length > 1 ? (float)i / (LevelValues.Length - 1) : 0f;
                // Log curve: sqrt gives a nice diminishing returns feel
                float curved = Mathf.Sqrt(t);
                LevelValues[i] = Mathf.Lerp(startValue, endValue, curved);
            }
            RegenerateCurve();
        }
        
        /// <summary>
        /// Fills level values with step function (discrete jumps).
        /// </summary>
        public void FillSteps(float startValue, float endValue, int stepCount)
        {
            ResizeLevelValues();
            stepCount = Mathf.Max(1, stepCount);
            
            for (int i = 0; i < LevelValues.Length; i++)
            {
                float t = LevelValues.Length > 1 ? (float)i / (LevelValues.Length - 1) : 0f;
                int step = Mathf.FloorToInt(t * stepCount);
                float stepT = (float)step / stepCount;
                LevelValues[i] = Mathf.Lerp(startValue, endValue, stepT);
            }
            RegenerateCurve();
        }
        
        /// <summary>
        /// Fills level values with additive increments per level.
        /// </summary>
        public void FillAdditive(float baseValue, float incrementPerLevel)
        {
            ResizeLevelValues();
            for (int i = 0; i < LevelValues.Length; i++)
            {
                LevelValues[i] = baseValue + (incrementPerLevel * i);
            }
            RegenerateCurve();
        }
        
        /// <summary>
        /// Fills level values with multiplicative scaling per level.
        /// </summary>
        public void FillMultiplicative(float baseValue, float multiplierPerLevel)
        {
            ResizeLevelValues();
            for (int i = 0; i < LevelValues.Length; i++)
            {
                LevelValues[i] = baseValue * Mathf.Pow(multiplierPerLevel, i);
            }
            RegenerateCurve();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Evaluation Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Gets the effective max level based on configuration.
        /// </summary>
        protected int GetEffectiveMaxLevel(IAttributeImpactDerivation spec)
        {
            return Configuration switch
            {
                ELevelConfig.LockToLevelProvider => spec.GetSource().GetMaxLevel(),
                ELevelConfig.Unlocked => MaxLevel,
                ELevelConfig.Partitioned => Mathf.Min(MaxLevel, spec.GetSource().GetLevel()),
                _ => MaxLevel
            };
        }
        
        /// <summary>
        /// Evaluates the scaling curve at the given relative level.
        /// </summary>
        protected float EvaluateAtRelativeLevel(float relativeLevel)
        {
            return Scaling.Evaluate(Mathf.Clamp01(relativeLevel));
        }
        
        /// <summary>
        /// Evaluates using the spec's relative level.
        /// </summary>
        protected float EvaluateFromSpec(IAttributeImpactDerivation spec)
        {
            float relativeLevel = spec.GetEffectDerivation().GetRelativeLevel();
            return ApplyBehaviourEvaluation(spec, EvaluateAtRelativeLevel(relativeLevel));

        }

        protected float ApplyBehaviourEvaluation(IAttributeImpactDerivation spec, float magnitude)
        {
            float m = magnitude;
            
            foreach (var behaviour in Behaviours)
            {
                behaviour.Initialize(spec);
                m = behaviour.Evaluate(m, spec);
            }

            return m;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // Enums
    // ═══════════════════════════════════════════════════════════════════════════
    
    public enum ECalculationOperation
    {
        Add,
        Multiply,
        Override
    }

    public enum ELevelConfig
    {
        [Tooltip("Uses the owning Ability/Entity's level range automatically.\n" +
                 "Level values array should match the source's max level.\n" +
                 "The provider is determined at runtime from the effect's derivation context.")]
        LockToLevelProvider,
        
        [Tooltip("Uses its own MaxLevel setting, independent of the source.\n" +
                 "Useful for Scalers that need a fixed level range.")]
        Unlocked,
        
        [Tooltip("Uses min(MaxLevel, provider's current level).\n" +
                 "Useful for effects that scale up to the provider's current level,\n" +
                 "but cap at a maximum even if provider exceeds it.")]
        Partitioned
    }
    
    public enum EScalerInterpolation
    {
        [Tooltip("No interpolation - jumps between values")]
        Constant,
        
        [Tooltip("Linear interpolation between values")]
        Linear,
        
        [Tooltip("Smooth curve through values")]
        Smooth
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // Animation Utility (Editor-safe wrapper)
    // ═══════════════════════════════════════════════════════════════════════════
    
    public static class AnimationUtility
    {
        public static void SetKeyLeftTangentMode(AnimationCurve curve, int index, UnityEditor.AnimationUtility.TangentMode mode)
        {
#if UNITY_EDITOR
            UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, index, 
                (UnityEditor.AnimationUtility.TangentMode)mode);
#endif
        }
        
        public static void SetKeyRightTangentMode(AnimationCurve curve, int index, UnityEditor.AnimationUtility.TangentMode mode)
        {
#if UNITY_EDITOR
            UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, index, 
                (UnityEditor.AnimationUtility.TangentMode)mode);
#endif
        }
    }
}