using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One number where a condition holds, and another where it doesn't (FEAT-29): "−0.1 × Target's Health (base) if
    /// Target's Health &lt; 0.3 × Target's Health (base), else 0". The condition is a class read against the number's
    /// context, so a project adds its own (<see cref="GameplayCondition"/>, Decision 43), and each branch is a number of
    /// any kind, read with the scaler as its owner. With no condition, it reads its False branch.
    /// </summary>
    [Serializable]
    [NumberKind("Conditional", Category = "Scaler")]
    public class ConditionalScaler : NumberKind, ISerializationCallbackReceiver
    {
        [Tooltip("The condition that chooses the branch")]
        [SerializeReference]
        public GameplayCondition When = new TagCondition();

        [Tooltip("What it reads where the condition holds")]
        public ScalableFloat TrueValue = 1f;

        [Tooltip("What it reads where the condition doesn't hold")]
        public ScalableFloat FalseValue = 0f;

        protected override float Evaluate(in NumberContext context)
        {
            var branch = When != null && When.Holds(in context) ? TrueValue : FalseValue;
            return branch?.Evaluate(in context, this) ?? 0f;
        }

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            When?.Initialize(deriv);
            TrueValue?.Initialize(deriv);
            FalseValue?.Initialize(deriv);
        }

        public override string Describe() =>
            $"{NumberText.Of(TrueValue)} if {NumberText.Of(When)}, else {NumberText.Of(FalseValue)}";

        // ═══════════════════════════════════════════════════════════════════════════
        // Saved before its condition was a class (FEAT-29)
        // ═══════════════════════════════════════════════════════════════════════════

        // The condition was one of nine, by number, with a tag query, an attribute, a threshold and a comparison beside it;
        // each branch was a scaler, and with no True scaler a met condition read the scaler's own values per level. A
        // saved threshold says the rest is old, and it moves into the condition and the branches as the asset loads.
        [FormerlySerializedAs("Condition"), SerializeField, HideInInspector] private int legacyCondition = -1;
        [FormerlySerializedAs("TagCondition"), SerializeField, HideInInspector] private TagQuery legacyTagQuery;
        [FormerlySerializedAs("CheckAttribute"), SerializeField, HideInInspector] private Attribute legacyAttribute;
        [FormerlySerializedAs("ThresholdValue"), SerializeField, HideInInspector] private float legacyThreshold = LegacyNumber.None;
        [FormerlySerializedAs("Comparison"), SerializeField, HideInInspector] private EComparisonOperator legacyComparison;
        [SerializeReference, HideInInspector] private AbstractScaler TrueScaler;
        [SerializeReference, HideInInspector] private AbstractScaler FalseScaler;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (float.IsNaN(legacyThreshold)) return;

            When = LegacyCondition(legacyCondition, legacyTagQuery, legacyAttribute, legacyThreshold, legacyComparison) ?? When;
            TrueValue = TrueScaler != null ? new ScalableFloat(TrueScaler, 1f, EMagnitudeOperation.UseScaler) : LegacyNumber.TakeTable(this);
            FalseValue = FalseScaler != null ? new ScalableFloat(FalseScaler, 1f, EMagnitudeOperation.UseScaler) : new ScalableFloat(0f);

            legacyCondition = -1;
            legacyTagQuery = null;
            legacyAttribute = null;
            legacyThreshold = LegacyNumber.None;
            legacyComparison = default;
            TrueScaler = null;
            FalseScaler = null;
        }

        // The condition the old one by its number is: a tag on the source or the target, an attribute on either compared
        // with the threshold, the level above or below it, the relative level above it, always, or never
        private static GameplayCondition LegacyCondition(int condition, TagQuery query, Attribute attribute, float threshold,
            EComparisonOperator comparison) => condition switch
        {
            0 => new TagCondition { On = NumberSide.Caster, Query = query ?? new TagQuery() },
            1 => new TagCondition { On = NumberSide.Target, Query = query ?? new TagQuery() },
            2 => new AttributeCondition { On = NumberSide.Caster, Attribute = attribute, Comparison = comparison, Value = threshold },
            3 => new AttributeCondition { On = NumberSide.Target, Attribute = attribute, Comparison = comparison, Value = threshold },
            4 => new LevelCondition { Comparison = EComparisonOperator.GreaterThan, Value = threshold },
            5 => new LevelCondition { Comparison = EComparisonOperator.LessThan, Value = threshold },
            6 => new LevelCondition { Relative = true, Comparison = EComparisonOperator.GreaterThan, Value = threshold },
            7 => new AlwaysCondition(),
            8 => new NeverCondition(),
            _ => null,
        };
    }
}
