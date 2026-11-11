using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A number kept between a floor and a ceiling (FEAT-27, FEAT-29): damage worked out from a target's missing health,
    /// capped. What it clamps and both bounds are numbers of any kind, read with the scaler as their owner, so its
    /// ceiling can be twice its floor, and its floor can scale by level.
    /// </summary>
    [Serializable]
    [NumberKind("Clamped", Category = "Scaler")]
    public class ClampedScaler : NumberKind, ISerializationCallbackReceiver
    {
        [Tooltip("The least it reads")]
        public ScalableFloat MinValue = 0f;

        [Tooltip("The most it reads")]
        public ScalableFloat MaxValue = 100f;

        [Tooltip("What it clamps")]
        public ScalableFloat Value = new(new SimpleScaler(), 1f, EMagnitudeOperation.UseScaler);

        protected override float Evaluate(in NumberContext context)
        {
            float value = Value?.Evaluate(in context, this) ?? 0f;
            float min = MinValue?.Evaluate(in context, this) ?? float.NegativeInfinity;
            float max = MaxValue?.Evaluate(in context, this) ?? float.PositiveInfinity;
            return Mathf.Clamp(value, min, max);
        }

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            Value?.Initialize(deriv);
            MinValue?.Initialize(deriv);
            MaxValue?.Initialize(deriv);
        }

        public override string Describe() =>
            $"{NumberText.Of(Value)}, between {NumberText.Of(MinValue)} and {NumberText.Of(MaxValue)}";

        // What it clamps, and its bounds (FEAT-32)
        public override void Explain(in NumberContext context, NumberTrace trace)
        {
            trace.Add(Value, context, this, "Value");
            trace.Add(MinValue, context, this, "Min");
            trace.Add(MaxValue, context, this, "Max");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Saved before its bounds were numbers (FEAT-29)
        // ═══════════════════════════════════════════════════════════════════════════

        // Its bounds were floats, and what it clamped a scaler, with none meaning its own values per level. A saved bound
        // says the rest is old, and they move into the numbers as the asset loads.
        [FormerlySerializedAs("MinValue"), SerializeField, HideInInspector] private float legacyMinValue = LegacyNumber.None;
        [FormerlySerializedAs("MaxValue"), SerializeField, HideInInspector] private float legacyMaxValue = LegacyNumber.None;
        [SerializeReference, HideInInspector] private AbstractScaler Inner;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            bool old = LegacyNumber.Move(ref legacyMinValue, ref MinValue);
            old |= LegacyNumber.Move(ref legacyMaxValue, ref MaxValue);
            if (!old) return;

            Value = Inner != null ? new ScalableFloat(Inner, 1f, EMagnitudeOperation.UseScaler) : LegacyNumber.TakeTable(this);
            Inner = null;
        }
    }
}
