using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Charges: how many uses an ability holds, each spending a charge that comes back as its Recharge says.
    /// Without one, an ability has one charge, an ordinary cooldown. An ability holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "How many uses the ability holds, each coming back as it recharges.")]
    public sealed class ChargesAbilityComponent : AbstractAbilityComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"{NumberText.Of(Charges)} charges, {(Recharge is AllAtOnceRecharge ? "all recharging at once" : "each recharging on its own")}";

        [Tooltip("How many uses it holds, each spending a charge that comes back as its Recharge says; 1 is an ordinary " +
                 "cooldown")]
        [NumberMin(2)]
        public ScalableInt Charges = 2;

        [SerializeReference]
        [Tooltip("How its spent charges come back: each on a cooldown of its own, or all at once as one cooldown ends once " +
                 "they're all spent, or as a recharge of your own says")]
        public AbstractChargeRecharge Recharge = new EachChargeRecharge();

        public override bool AllowsSeveral => false;
    }

    /// <summary>
    /// Toggle: an effect an ability turns on its caster with one use, paying its cost, and off with the next, starting its
    /// cooldown. As its holder forgets the ability, the effect comes off, where it stayed on. An ability
    /// holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Turns an effect on with one use and off with the next.")]
    public sealed class ToggleAbilityComponent : AbstractAbilityComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"Toggles {NameOf(Effect)}";

        [Tooltip("The effect it toggles on its caster: used, it puts the effect on, paying its cost; used again, it takes " +
                 "the effect off, paying and asking nothing, and starts its cooldown")]
        public GameplayEffect Effect;

        public override bool AllowsSeveral => false;

        public override void Removed(AbilityComponentContext ctx) => ctx.Spec?.ToggleOff();
    }

    /// <summary>
    /// Cast Range: how far from its caster an ability's target may be, the one range it has, read at its level: the
    /// ability's reach, an <see cref="IAbilityReach"/>. An activation handed a target or a point beyond it is refused as
    /// Range, and targeting that picks one beyond it ends the activation there; a targeting task with a range of its own
    /// searches within it, from the caster; and the lab's bots cast it only once their foe is within it, each asking the
    /// ability's reach through <see cref="ForgeReach"/>. 0 reaches anywhere. An ability holds one at most. Its ability's
    /// Target Checks show its Check Range while it holds the component, which this enforces.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "How far from the caster the ability's target may be.")]
    public sealed class CastRangeAbilityComponent : AbstractAbilityComponent, IAbilityReach
    {
        /// <summary>Why an activation handed a target beyond its range, or whose targeting picked one, is refused.</summary>
        public const string OutOfRange = "Its target is beyond its Cast Range";

        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"Range {NumberText.Of(Range)}";

        // Its Check Range, made as it's first asked for and kept with it
        [NonSerialized] private CheckRangeValidation check;

        /// <summary>Check Range, its place among its ability's Target Checks: the range this enforces.</summary>
        public override IAbilityValidationRule TargetCheck => check ??= new CheckRangeValidation(this);

        [Tooltip("How far from its caster its target or point may be, read at the ability's level; 0 reaches anywhere")]
        public ScalableFloat Range = 6f;

        [Tooltip("Measures along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        public override bool AllowsSeveral => false;

        /// <summary>Its range, read at the ability's level, and whether along the ground; false where it reaches anywhere.</summary>
        public bool TryReadReach(AbilitySpec spec, ProcessDataPacket data, out float range, out bool flat)
        {
            range = spec == null ? 0f : spec.Read(Range, data);
            flat = Flat;
            return range > 0f;
        }

        /// <summary>Its refusal is Range, so an AI can walk closer and ask again.</summary>
        public override EActivationRefusal RefusedAs => EActivationRefusal.Range;

        // Handed a target or a point beyond it, as an AI's activation hands it, the activation doesn't start, so nothing of
        // it is paid
        public override string CanStart(AbilityComponentContext ctx) =>
            ctx.Data is AbilityDataPacket data && ForgeReach.HandedBeyond(this, data) ? OutOfRange : null;

        // Its targeting picked a target beyond it, whichever task picked it: the activation ends there
        public override string CanTakeTarget(AbilityComponentContext ctx) =>
            ctx.Data is AbilityDataPacket data && ForgeReach.PickedBeyond(this, data) ? OutOfRange : null;
    }

    /// <summary>
    /// Check Range: the Cast Range component's check, its target within the component's range of its caster, among the
    /// ability's Target Checks while the ability holds the component. The component enforces it, refusing a target or a
    /// point handed beyond it and ending targeting that picks one, so this passes: it's the range's place among the
    /// checks, and comes and goes with the component. It read its range from a tag's weight or the ability's Local Data
    /// before the component held it.
    /// </summary>
    [ValidationSide(EValidationSide.Target)]
    [ForgeMenu(Name = "Check Range", Description = "The Cast Range component's check: its target within the component's range of its caster. It comes and goes with the component.")]
    public sealed class CheckRangeValidation : AbstractComponentValidationRule<CastRangeAbilityComponent>
    {
        public CheckRangeValidation(CastRangeAbilityComponent component) : base(component) { }

        /// <summary>What it checks, with its component's range: "Its target within 6 of its caster, along the ground".</summary>
        public override string Describe()
        {
            if (Component?.Range == null) return null;
            if (Component.Range.IsConstant && Component.Range.Constant <= 0f) return "Its target anywhere: its Cast Range of 0 reaches anywhere";
            return $"Its target within {NumberText.Of(Component.Range)} of its caster{(Component.Flat ? ", along the ground" : "")}";
        }
    }
}
