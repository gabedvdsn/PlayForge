using System;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Returns values based on level thresholds.
    /// Useful for tier-based progression (e.g., Bronze/Silver/Gold ranks).
    /// Each tier's value and its default are numbers of any kind, as a share of the caster's attribute (FEAT-29). A tier
    /// reads its number where the scaler is read, and the default with the scaler as its owner.
    /// </summary>
    [Serializable]
    [NumberKind("Threshold", Category = "Scaler", Reads = NumberReads.Level)]
    public class ThresholdScaler : NumberKind, ISerializationCallbackReceiver
    {
        [Tooltip("Level thresholds and their corresponding values")]
        public LevelThreshold[] Thresholds = new LevelThreshold[]
        {
            new LevelThreshold { Level = 1, Value = 1f },
            new LevelThreshold { Level = 10, Value = 2f },
            new LevelThreshold { Level = 20, Value = 3f }
        };

        [Tooltip("Value to use if below all thresholds")]
        public ScalableFloat DefaultValue = 1f;

        protected override float Evaluate(in NumberContext context)
        {
            int matched = Tier(context.Level.CurrentValue);
            return matched >= 0
                ? Thresholds[matched].Value?.Evaluate(in context) ?? 0f
                : DefaultValue?.Evaluate(in context, this) ?? 0f;
        }

        // The highest threshold at or below the level, or -1 where it's below them all
        private int Tier(int currentLevel)
        {
            int matched = -1;
            int highestMatchedLevel = 0;
            if (Thresholds == null) return matched;
            for (int i = 0; i < Thresholds.Length; i++)
            {
                if (currentLevel >= Thresholds[i].Level && Thresholds[i].Level > highestMatchedLevel)
                {
                    highestMatchedLevel = Thresholds[i].Level;
                    matched = i;
                }
            }
            return matched;
        }

        // The tier the level is in, or the default below them all (FEAT-32)
        public override void Explain(in NumberContext context, NumberTrace trace)
        {
            int matched = Tier(context.Level.CurrentValue);
            if (matched >= 0) trace.Add(Thresholds[matched].Value, context, null, $"From level {Thresholds[matched].Level}");
            else trace.Add(DefaultValue, context, this, "Default");
        }

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            DefaultValue?.Initialize(deriv);
            if (Thresholds == null) return;
            for (int i = 0; i < Thresholds.Length; i++) Thresholds[i].Value?.Initialize(deriv);
        }

        // "1 from level 1, 2 from level 10, 3 from level 20", or past three tiers "5 tiers by level"
        public override string Describe()
        {
            if (Thresholds == null || Thresholds.Length == 0) return NumberText.Of(DefaultValue);
            if (Thresholds.Length > 3) return $"{Thresholds.Length} tiers by level";
            var text = new StringBuilder();
            for (int i = 0; i < Thresholds.Length; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(Thresholds[i].Value != null ? NumberText.Of(Thresholds[i].Value) : NumberText.Number(0f));
                text.Append(" from level ").Append(Thresholds[i].Level);
            }
            return text.ToString();
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

        // ═══════════════════════════════════════════════════════════════════════════
        // Saved before its values were numbers (FEAT-29)
        // ═══════════════════════════════════════════════════════════════════════════

        // Its default and each tier's value were floats. A saved default says the tiers are old too, as a tier, a struct,
        // can't hold a float that says it's empty, and they move into the numbers as the asset loads.
        [FormerlySerializedAs("DefaultValue"), SerializeField, HideInInspector] private float legacyDefaultValue = LegacyNumber.None;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (!LegacyNumber.Move(ref legacyDefaultValue, ref DefaultValue) || Thresholds == null) return;
            for (int i = 0; i < Thresholds.Length; i++)
            {
                Thresholds[i].Value = new ScalableFloat(Thresholds[i].legacyValue);
                Thresholds[i].legacyValue = 0f;
            }
        }
    }

    [Serializable]
    public struct LevelThreshold
    {
        [Tooltip("Minimum level for this threshold")]
        public int Level;

        [Tooltip("Value when at or above this level")]
        public ScalableFloat Value;

        [Tooltip("Optional name for this tier (e.g., 'Bronze', 'Silver')")]
        public string TierName;

        // Its value was a float, which its scaler moves as the asset loads (FEAT-29)
        [FormerlySerializedAs("Value"), SerializeField, HideInInspector] internal float legacyValue;
    }
}
