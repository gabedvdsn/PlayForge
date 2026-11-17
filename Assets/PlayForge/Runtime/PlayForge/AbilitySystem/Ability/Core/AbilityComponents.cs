using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Charges: how many uses an ability holds, each spending a charge that comes back as its Recharge says: its ability's
    /// cooldown model, an <see cref="ICooldownModel"/>, which the Cooldown check asks and the ability spends through.
    /// Without one, an ability has one charge, its single cooldown. An ability holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "How many uses the ability holds, each coming back as it recharges.")]
    public sealed class ChargesAbilityComponent : AbstractAbilityComponent, ICooldownModel
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

        /// <summary>
        /// Whether its charges may be counted, as far as the asset says: more than 1, a number that scales, or a recharge
        /// that counts a single charge. With 1 and a built-in recharge, it's an ordinary cooldown.
        /// </summary>
        public bool CountsCharges =>
            Charges != null && (!Charges.IsConstant || Charges.Constant > 1) || (Recharge?.CountsASingleCharge ?? false);

        /// <summary>Its charges, as the Activation line counts them, "3 charges", where they're counted.</summary>
        public override string DescribeUsage()
        {
            if (!CountsCharges || Charges == null) return null;
            if (!Charges.IsConstant) return NumberText.Of(Charges, "charges");
            int count = Charges.Constant;
            return count == 0 ? "None" : count == 1 ? "1 charge" : $"{count} charges";
        }

        /// <summary>Its charges on an entity's ability, kept with the ability as its entity holds it, made as they're first asked for.</summary>
        public AbilityCharges ChargesOf(AbilitySpec spec)
        {
            if (spec == null) return null;
            if (!spec.ComponentStates.TryGet(this, out AbilityCharges charges)) spec.ComponentStates.Keep(this, charges = new AbilityCharges(spec));
            return charges;
        }

        int ICooldownModel.Max(AbilitySpec spec) => ChargesOf(spec)?.Max ?? 1;

        int ICooldownModel.InHand(AbilitySpec spec) => ChargesOf(spec)?.InHand ?? 0;

        // A charge spent, as its Recharge says, where its charges are counted; or else its single cooldown
        void ICooldownModel.Spend(AbilitySpec spec, ProcessDataPacket data)
        {
            if (ChargesOf(spec) is not { } charges) return;
            if (charges.Counted) charges.Spend(data);
            else SingleCooldown.Instance.Spend(spec, data);
        }

        string ICooldownModel.NoneInHand(AbilitySpec spec) =>
            ChargesOf(spec) is { Counted: true } ? "It has no charges in hand" : SingleCooldown.Instance.NoneInHand(spec);
    }

    /// <summary>
    /// The Charges component's shortcuts: an ability's charges and their recharge, as its Charges component says them, and
    /// an entity's ability's charges.
    /// </summary>
    public static class ForgeCharges
    {
        private static readonly ScalableInt OneCharge = 1;
        private static readonly AbstractChargeRecharge EachCharge = new EachChargeRecharge();

        /// <summary>Its charges: its Charges component's, or one, an ordinary cooldown.</summary>
        public static ScalableInt Charges(this Ability ability) =>
            ability != null && ability.Find<ChargesAbilityComponent>() is { Charges: { } charges } ? charges : OneCharge;

        /// <summary>How its spent charges come back: its Charges component's, or each on a cooldown of its own.</summary>
        public static AbstractChargeRecharge Recharge(this Ability ability) =>
            ability != null && ability.Find<ChargesAbilityComponent>() is { Recharge: { } recharge } ? recharge : EachCharge;

        /// <summary>
        /// Its charges on its caster: how many it holds, how many are in hand, and what its recharge counts; null where the
        /// ability has no Charges component. What's in hand of any ability is its <see cref="AbilitySpec.CooldownModel"/>'s.
        /// </summary>
        public static AbilityCharges Charges(this AbilitySpec spec) => spec?.Find<ChargesAbilityComponent>()?.ChargesOf(spec);
    }

    /// <summary>
    /// Toggle: an effect an ability turns on its caster with one use, paying its cost, and off with the next, starting its
    /// cooldown. It applies its ability's usage in the ability's place, waives the cost and the cooldown while its effect is
    /// on, so it can always turn off, and claims its effect as a Toggle's for a purge. As its holder forgets the ability,
    /// the effect comes off, where it stayed on. An ability holds one at most.
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

        /// <summary>Whether its effect is on its ability's caster, put there by this ability; one another put there leaves it off.</summary>
        public bool IsOn(AbilitySpec spec) =>
            Effect != null && spec?.Source?.ToGAS()?.GetEffectSystem() is { } effects && effects.CountFrom(Effect, spec) > 0;

        /// <summary>Takes its effect off its ability's caster, where it's on.</summary>
        public void TurnOff(AbilitySpec spec)
        {
            if (IsOn(spec)) spec.Source.ToGAS()?.GetEffectSystem()?.RemoveGameplayEffect(Effect, spec);
        }

        // As its holder forgets the ability, its effect comes off
        public override void Removed(AbilityComponentContext ctx) => TurnOff(ctx.Spec);

        // On, with the activation's magnitudes, paying its cost; or off, paying nothing and spending its cooldown, through
        // whatever cooldown model the ability has
        public override bool ApplyUsage(AbilityComponentContext ctx)
        {
            var spec = ctx.Spec;
            if (Effect == null || spec == null) return false;
            if (IsOn(spec))
            {
                spec.Source.ToGAS()?.GetEffectSystem()?.RemoveGameplayEffect(Effect, spec);
                spec.SpendCooldown(ctx.Data);
                return true;
            }
            spec.Source.ApplyCarrier(spec.GenerateUsageEffect(Effect, ctx.Data));
            spec.PayCosts(ctx.Data);
            return true;
        }

        // While it's on, neither its cost nor its cooldown stops it turning off
        public override EUsageCheck Waives(AbilityComponentContext ctx) =>
            IsOn(ctx.Spec) ? EUsageCheck.Cost | EUsageCheck.Cooldown : EUsageCheck.None;

        // Its effect is a toggle's, for a purge
        public override EEffectCategory CategoryOf(AbilityComponentContext ctx, GameplayEffectSpec effect) =>
            Effect != null && ReferenceEquals(effect?.Base, Effect) ? EEffectCategory.Toggle : EEffectCategory.None;
    }

    /// <summary>The Toggle component's shortcuts: the effect an ability toggles, and whether an entity's ability has it on.</summary>
    public static class ForgeToggles
    {
        /// <summary>The effect it toggles on its caster: its Toggle component's, or none, where it doesn't toggle.</summary>
        public static GameplayEffect Toggles(this Ability ability) => ability?.Find<ToggleAbilityComponent>()?.Effect;

        /// <summary>Whether the effect it toggles is on its caster, put there by it.</summary>
        public static bool IsToggledOn(this AbilitySpec spec) => spec?.Find<ToggleAbilityComponent>() is { } toggle && toggle.IsOn(spec);
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
