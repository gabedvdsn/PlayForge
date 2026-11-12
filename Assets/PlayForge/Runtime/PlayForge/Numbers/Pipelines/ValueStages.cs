using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One stage of a <see cref="ValuePipeline"/>: it takes the value and gives what goes on. A project writes its own by
    /// deriving from it; each built-in one reads its numbers in the hit's context, as any number field is read (TW-2).
    /// </summary>
    [Serializable]
    public abstract class AbstractValueStage
    {
        [Tooltip("What the stage is, as a breakdown names it: Amplification, Armor, Critical. Empty, it says what it does")]
        public string Label;

        [Tooltip("Off, the value passes it by")]
        public bool Enabled = true;

        /// <summary>The value after this stage. As it came, unless a stage says otherwise.</summary>
        public virtual float Apply(float value, in ValueRun run) => value;

        /// <summary>What the stage does, in a line.</summary>
        public virtual string Describe() => GetType().Name;

        /// <summary>Its label, or what it does.</summary>
        public string Name() => string.IsNullOrEmpty(Label) ? Describe() : Label;
    }

    /// <summary>
    /// The value times a number read for the hit: a bonus, × Caster's Damage Amplification read as 1 + x; a reduction, ×
    /// Target's Damage Resistance read as what's left; an armor curve; a bonus that reads the target, as Crowbar's above 90%.
    /// </summary>
    [Serializable]
    public class MultiplyStage : AbstractValueStage
    {
        [Tooltip("What the value is multiplied by, read for the hit")]
        public ScalableFloat By = 1f;

        public override float Apply(float value, in ValueRun run) => value * (By?.Evaluate(in run.Context, this) ?? 1f);

        public override string Describe() => $"× {NumberText.Of(By)}";
    }

    /// <summary>
    /// A number added to the value's size, read for the hit: a flat bonus of 10 makes a blow of −100 a blow of −110 and a
    /// heal of 50 one of 60, as the value's sign has it.
    /// </summary>
    [Serializable]
    public class AddStage : AbstractValueStage
    {
        [Tooltip("What's added to the value's size, read for the hit: 10 makes a blow of -100 one of -110")]
        public ScalableFloat Amount = 0f;

        public override float Apply(float value, in ValueRun run)
        {
            float amount = Amount?.Evaluate(in run.Context, this) ?? 0f;
            return value < 0f ? value - amount : value + amount;
        }

        public override string Describe() => $"+ {NumberText.Of(Amount)}";
    }

    /// <summary>
    /// On a chance, the value times a number: a critical hit. It rolls once for the hit, as it lands, and its step in the
    /// breakdown says so (TW-8).
    /// </summary>
    [Serializable]
    public class ChanceStage : AbstractValueStage
    {
        [Tooltip("The chance, from 0 to 1, read for the hit: a critical strike chance, as Caster's Crit Chance")]
        public ScalableFloat Chance = 0.1f;

        [Tooltip("What the value is multiplied by when it rolls under the chance: 2 for a double")]
        public ScalableFloat Multiplier = 2f;

        [Tooltip("What the breakdown notes when it rolls: critical")]
        public string Note = "critical";

        [Tooltip("How it rolls: pseudo-random, and its luck (TW-8)")]
        public ChanceRoll Roll = new();

        public override float Apply(float value, in ValueRun run)
        {
            float chance = Chance?.Evaluate(in run.Context, this) ?? 0f;
            if (!ForgeChance.Roll(chance, in run.Context, Roll, this)) return value;
            run.Note(Note);
            return value * (Multiplier?.Evaluate(in run.Context, this) ?? 1f);
        }

        public override string Describe() => $"× {NumberText.Of(Multiplier)} on a {NumberText.Of(Chance)} chance";
    }

    /// <summary>The value's size kept between two numbers read for the hit, its sign as it was.</summary>
    [Serializable]
    public class ClampStage : AbstractValueStage
    {
        [Tooltip("The least its size can be, read for the hit")]
        public ScalableFloat Least = 0f;

        [Tooltip("The most its size can be, read for the hit")]
        public ScalableFloat Most = 9999f;

        public override float Apply(float value, in ValueRun run)
        {
            float least = Least?.Evaluate(in run.Context, this) ?? 0f;
            float most = Most?.Evaluate(in run.Context, this) ?? float.PositiveInfinity;
            float size = Mathf.Clamp(Mathf.Abs(value), least, Mathf.Max(least, most));
            return value < 0f ? -size : size;
        }

        public override string Describe() => $"between {NumberText.Of(Least)} and {NumberText.Of(Most)}";
    }

    /// <summary>
    /// A loss taken first from another attribute of the one hit, as a barrier or a shield takes a blow before health: what
    /// it soaks comes off that attribute, unworkable so no worker reads it again, at the frame's end, and the rest goes on
    /// (TW-12). A gain passes it by.
    /// </summary>
    [Serializable]
    public class AbsorbStage : AbstractValueStage
    {
        [Tooltip("The attribute that takes the loss first: a barrier, a shield")]
        public Attribute Into;

        [Tooltip("Whose: the one hit, or the caster")]
        public NumberSide On = NumberSide.Target;

        public override float Apply(float value, in ValueRun run)
        {
            if (value >= 0f || Into == null || !run.Context.TryGetAttribute(On, Into, out var pool)) return value;
            float soaked = Mathf.Min(pool.CurrentValue, -value);
            if (soaked <= 0f) return value;
            var system = run.Context.SystemOf(On);
            if (system == null) return value;
            system.GetActionQueue()?.Enqueue(new ModifyAttributeAction(system, Into,
                SourcedModifiedAttributeValue.GenerateUnworkable(system, Into, -soaked, 0f)));
            run.Note($"{soaked:0.##} to {Into.GetName()}");
            return value + soaked;
        }

        public override string Describe() => $"{(Into != null ? Into.GetName() : "an attribute")} takes it first";
    }
}
