using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

        /// <summary>
        /// Off, levels map exactly: level 4 reads the 4th value, and a level past the end reads the last (Decision 28).
        /// On, the values spread over the level range <see cref="Configuration"/> names, as every scaler's did. It is on
        /// for the scalers saved before it existed, so they keep how they read; the inspector makes new scalers with it
        /// off.
        /// </summary>
        [Tooltip("Off: level 4 reads the 4th value, and a level past the end reads the last. On: the values spread over " +
                 "the level range, and a level between two reads between them")]
        public bool StretchOverLevels = true;
        
        [Tooltip("Generated curve from level values (auto-updated)")]
        public AnimationCurve Scaling = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [SerializeReference] public List<AbstractScalerBehaviour> Behaviours = new();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        public abstract void Initialize(IAttributeImpactCarrier deriv);
        public abstract float Evaluate(IAttributeImpactCarrier deriv);

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
        public void RegenerateCurve() => Scaling = CurveFor(LevelValues, Interpolation);

        /// <summary>
        /// The curve a stretched scaler reads: its values spread from 0 to 1, joined as the interpolation says. The
        /// drawer writes it through the asset's SerializedObject, so an edit and its curve are one Undo step.
        /// </summary>
        public static AnimationCurve CurveFor(float[] levelValues, EScalerInterpolation interpolation)
        {
            if (levelValues == null || levelValues.Length == 0)
            {
                return AnimationCurve.Linear(0f, 1f, 1f, 1f);
            }
            
            var curve = new AnimationCurve();
            
            for (int i = 0; i < levelValues.Length; i++)
            {
                // Convert level index to relative level (0 to 1)
                float t = levelValues.Length > 1 ? (float)i / (levelValues.Length - 1) : 0f;
                float value = levelValues[i];
                
                Keyframe keyframe = new Keyframe(t, value);
                
                // Set tangent mode based on interpolation
                switch (interpolation)
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
                
                curve.AddKey(keyframe);
            }
            
            // Apply tangent modes
            for (int i = 0; i < curve.length; i++)
            {
                switch (interpolation)
                {
                    case EScalerInterpolation.Constant:
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, ECurveTangentMode.Constant);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, ECurveTangentMode.Constant);
                        break;
                    case EScalerInterpolation.Linear:
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, ECurveTangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, ECurveTangentMode.Linear);
                        break;
                    case EScalerInterpolation.Smooth:
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, ECurveTangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, ECurveTangentMode.ClampedAuto);
                        break;
                }
            }
            return curve;
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
        /// Gets the effective max level based on configuration. Falls back to the
        /// origin's own level pair when there is no live source — synthetic leveler
        /// carriers (tooltip/analysis evaluation via <see cref="LevelerEffectOrigin"/>)
        /// carry their min/max on the origin itself.
        /// </summary>
        protected int GetEffectiveMaxLevel(IAttributeImpactCarrier spec)
        {
            var origin = spec.GetOrigin();
            var source = origin?.GetSource();

            switch (Configuration)
            {
                case ELevelConfig.LockToLevelProvider:
                    if (source is not null) return source.GetLevel().MaxValue;
                    return origin is not null ? origin.GetLevel().MaxValue : MaxLevel;

                case ELevelConfig.Unlocked:
                    return MaxLevel;

                case ELevelConfig.Clamped:
                    int current = source is not null ? source.GetLevel().CurrentValue
                        : origin is not null ? origin.GetLevel().CurrentValue
                        : MaxLevel;
                    return Mathf.Min(MaxLevel, current);

                default:
                    return MaxLevel;
            }
        }

        protected float GetEffectiveRelativeLevel(IAttributeImpactCarrier deriv)
        {
            var level = deriv.GetOrigin().GetLevel();
            int maxLevel = GetEffectiveMaxLevel(deriv);
            float relativeLevel = Mathf.InverseLerp(level.MinValue, maxLevel, level.CurrentValue);
            return Mathf.Clamp01(relativeLevel);
        }
        
        /// <summary>
        /// Evaluates the scaling curve at the given relative level.
        /// </summary>
        protected float EvaluateScalingAtRelativeLevel(float relativeLevel)
        {
            return Scaling.Evaluate(Mathf.Clamp01(relativeLevel));
        }
        
        /// <summary>
        /// The scaler's value at a level: the level's own value, and past the end the last, unless the scaler stretches
        /// its values over the level range, where it reads its curve at the level's place in the range (Decision 28).
        /// </summary>
        protected float EvaluateAtLevel(int level, float relativeLevel) =>
            StretchOverLevels ? EvaluateScalingAtRelativeLevel(relativeLevel) : GetValueAtLevel(level);

        /// <summary>
        /// Evaluates at the spec's level, exactly or stretched, then through the scaler's behaviours.
        /// </summary>
        protected float EvaluateFromSpec(IAttributeImpactCarrier spec)
        {
            float value = StretchOverLevels
                ? EvaluateScalingAtRelativeLevel(GetEffectiveRelativeLevel(spec))
                : GetValueAtLevel(spec.GetOrigin().GetLevel().CurrentValue);
            return ApplyBehaviourEvaluation(spec, value);
        }

        protected float ApplyBehaviourEvaluation(IAttributeImpactCarrier spec, float magnitude)
        {
            float m = magnitude;
            
            foreach (var behaviour in Behaviours)
            {
                // An empty slot, which a list's "+" can leave, changes nothing; it threw on every evaluation (ED-12)
                if (behaviour == null) continue;
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
        Override,
        FlatBonus
    }

    public enum EScalerRelativeOperation
    {
        Add,
        Multiply,
        UseAttributeValue,
        UseScalerValue
    }

    public enum ELevelConfig
    {
        [Tooltip("Uses the provider's level range automatically.\n" +
                 "Level values array should match the source's max level.\n" +
                 "The provider is determined at runtime from the effect's derivation context.")]
        [InspectorName("Provider's Levels")] LockToLevelProvider,
        
        [Tooltip("Uses its own MaxLevel setting, independent of the source.\n" +
                 "Useful for Scalers that need a fixed level range.")]
        [InspectorName("Own Max Level")] Unlocked,
        
        [Tooltip("Uses min(MaxLevel, provider's current level).\n" +
                 "Useful for effects that scale up to the provider's current level,\n" +
                 "but cap at a maximum even if provider exceeds it.\n" +
                 "Useful for scalers that need a fixed level range.")]
        [InspectorName("Clamped to Provider")] Clamped
    }
    
    public enum EScalerInterpolation
    {
        [Tooltip("No interpolation - jumps between values")]
        [InspectorName("Steps")] Constant,
        
        [Tooltip("Linear interpolation between values")]
        [InspectorName("Straight Line")] Linear,
        
        [Tooltip("Smooth curve through values")]
        [InspectorName("Smooth Curve")] Smooth
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // Animation Utility (editor-safe wrapper)
    //
    // Player builds have no UnityEditor — the signatures use a runtime-safe enum and
    // tangent application becomes a no-op outside the editor (curve keys keep Unity's
    // default tangents; LevelValues remain the authoritative data either way).
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Runtime-safe mirror of the editor tangent modes used by scaler curves.</summary>
    public enum ECurveTangentMode
    {
        Constant,
        Linear,
        ClampedAuto,
    }

    public static class AnimationUtility
    {
        public static void SetKeyLeftTangentMode(AnimationCurve curve, int index, ECurveTangentMode mode)
        {
#if UNITY_EDITOR
            UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, index, Convert(mode));
#endif
        }

        public static void SetKeyRightTangentMode(AnimationCurve curve, int index, ECurveTangentMode mode)
        {
#if UNITY_EDITOR
            UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, index, Convert(mode));
#endif
        }

#if UNITY_EDITOR
        private static UnityEditor.AnimationUtility.TangentMode Convert(ECurveTangentMode mode) => mode switch
        {
            ECurveTangentMode.Constant => UnityEditor.AnimationUtility.TangentMode.Constant,
            ECurveTangentMode.Linear => UnityEditor.AnimationUtility.TangentMode.Linear,
            _ => UnityEditor.AnimationUtility.TangentMode.ClampedAuto,
        };
#endif
    }
}