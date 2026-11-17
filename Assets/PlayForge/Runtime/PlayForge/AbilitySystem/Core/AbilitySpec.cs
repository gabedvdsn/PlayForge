using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySpec : BasicEffectOrigin
    {
        public Ability Base;

        public AbilitySpec(ISource source, Ability ability, int level) : base(source, new IntValuePairClamped(level, ability.StartingLevel, ability.MaxLevel), ability.Tags.AssetTag)
        {
            Base = ability;
        }

        /// <summary>
        /// Its ability's first component that is a <typeparamref name="T"/>, its kind or a capability it implements, as its
        /// reach, <see cref="IAbilityReach"/>; null where none is.
        /// </summary>
        public T Find<T>() where T : class => Base?.Find<T>();

        private ComponentStates componentStates;

        /// <summary>What its ability's components keep for this entity's ability.</summary>
        public ComponentStates ComponentStates => componentStates ??= new ComponentStates();

        // Its ability's components, each hearing a moment of it
        internal void HearGranted() => Hear(component => component.Granted(new AbilityComponentContext(this)));
        internal void HearRemoved() => Hear(component => component.Removed(new AbilityComponentContext(this)));
        internal void HearStart(ProcessDataPacket data)
        {
            committed = false;
            Hear(component => component.Start(new AbilityComponentContext(this, data)));
        }

        // An activation that paid what it costs is, as it ends, a use of the item that lends the ability
        internal void HearEnd(ProcessDataPacket data, EAbilityEnd how)
        {
            Hear(component => component.End(new AbilityComponentContext(this, data), how));
            if (!committed) return;
            committed = false;
            LentBy?.HearUsed();
        }

        private void HearCommit(ProcessDataPacket data)
        {
            committed = true;
            Hear(component => component.Commit(new AbilityComponentContext(this, data)));
        }

        /// <summary>The item that lends it, while one does, whose components hear each use of it.</summary>
        internal ItemSpecContainer LentBy { get; set; }

        // Whether the activation running paid what it costs
        private bool committed;

        private void Hear(Action<AbstractAbilityComponent> moment)
        {
            if (Base.Components == null) return;
            foreach (var component in Base.Components)
                if (component != null) moment(component);
        }

        /// <summary>
        /// Applies the ability's cooldown and cost to its caster, each with the magnitudes the caller set on the
        /// activation's data, so a caller can size them as it sizes the ability's other effects. A component may apply them
        /// in their place, as a Toggle turns its effect on or off: the first whose
        /// <see cref="AbstractAbilityComponent.ApplyUsage"/> says it did. Its components hear the commit either way.
        /// </summary>
        public void ApplyUsageEffects(ProcessDataPacket data = null)
        {
            if (!ComponentAppliesUsage(data))
            {
                SpendCooldown(data);
                PayCosts(data);
            }
            HearCommit(data);
        }

        // Whether one of its components applied its usage in its place
        private bool ComponentAppliesUsage(ProcessDataPacket data)
        {
            if (Base.Components == null) return false;
            foreach (var component in Base.Components)
                if (component != null && component.ApplyUsage(new AbilityComponentContext(this, data))) return true;
            return false;
        }

        /// <summary>
        /// How its uses come back: its first component that gives a cooldown model, as Charges, or else the single
        /// cooldown, one use, back as its Cooldown ends. A HUD reads what's in hand through it, whatever the ability's
        /// components are.
        /// </summary>
        public ICooldownModel CooldownModel => Find<ICooldownModel>() ?? SingleCooldown.Instance;

        /// <summary>
        /// Spends a use through its cooldown model, with the activation's magnitudes: a charge, as its Charges say, or else
        /// its Cooldown applied.
        /// </summary>
        public void SpendCooldown(ProcessDataPacket data = null) => CooldownModel.Spend(this, data);

        /// <summary>Pays what it costs, with the activation's magnitudes: its Cost effect, and beside it each attribute cost.</summary>
        public void PayCosts(ProcessDataPacket data = null)
        {
            if (Base.Cost is not null) Source.ApplyCarrier(GenerateUsageEffect(Base.Cost, data));
            PayAttributeCosts(data);
        }

        /// <summary>
        /// Whether any of its components waives one of <paramref name="checks"/> for an activation, as a Toggle that's on
        /// waives its cost and its cooldown, which the Cost and Cooldown checks then pass.
        /// </summary>
        public bool Waives(EUsageCheck checks, ProcessDataPacket data = null)
        {
            if (Base.Components == null || checks == EUsageCheck.None) return false;
            foreach (var component in Base.Components)
                if (component != null && (component.Waives(new AbilityComponentContext(this, data)) & checks) != 0) return true;
            return false;
        }

        /// <summary>
        /// The categories its components claim an effect it put on is of, for a purge, as a Toggle claims the effect it
        /// toggles; None where none claims it.
        /// </summary>
        public EEffectCategory CategoryOf(GameplayEffectSpec effect)
        {
            var categories = EEffectCategory.None;
            if (Base.Components == null || effect == null) return categories;
            foreach (var component in Base.Components)
                if (component != null) categories |= component.CategoryOf(new AbilityComponentContext(this), effect);
            return categories;
        }

        /// <summary>
        /// Applies its Cooldown to its caster, with the activation's magnitudes; as a container of its own where a spent
        /// charge's runs alone, whatever the effect does when it's applied again. Nothing, where it has no Cooldown.
        /// </summary>
        public void ApplyCooldown(ProcessDataPacket data, bool ownContainer)
        {
            if (Base.Cooldown is null) return;
            var spec = GenerateUsageEffect(Base.Cooldown, data);
            if (spec == null) return;
            spec.OwnContainer = ownContainer;
            Source.ApplyCarrier(spec);
        }

        /// <summary>Whether its caster carries its Cooldown's asset tag, as the single cooldown's check asks.</summary>
        public bool OnCooldown => Base.Cooldown is { } cooldown && Source.GetTagCache().TryGetWeight(cooldown.Tags.AssetTag, out _);

        /// <summary>
        /// What an attribute cost comes to for an activation, at the ability's level, read in the activation's data where
        /// it's given; never under 0.
        /// </summary>
        public float AmountOf(AttributeCost cost, ProcessDataPacket data = null) =>
            cost?.Amount == null ? 0f : Mathf.Max(0f, Read(cost.Amount, data));

        /// <summary>
        /// What a number of the ability comes to, at its level, read in an activation's data where it's given, as its
        /// Cast Range and its attribute costs read; 0 for none.
        /// </summary>
        public float Read(ScalableFloat number, ProcessDataPacket data = null)
        {
            if (number == null) return 0f;
            var context = data is AbilityDataPacket activation && ReferenceEquals(activation.EffectOrigin, this)
                ? NumberContext.Of(data)
                : new NumberContext(Source, null, GetLevel(), true, this, null, data?.SetByCallerMagnitudes, null, data);
            return number.Evaluate(context);
        }

        /// <summary>
        /// Whether its caster can pay its attribute costs: one that comes to 0 asks nothing, and the rest need their
        /// attribute found, their own or their role's in the project, with as much of it on the caster as all of them that
        /// spend it come to.
        /// </summary>
        public bool CanPayAttributeCosts(ProcessDataPacket data = null)
        {
            var costs = Base.AttributeCosts;
            if (costs == null || costs.Count == 0) return true;
            for (int i = 0; i < costs.Count; i++)
            {
                if (costs[i] == null || AmountOf(costs[i], data) <= 0f) continue;
                var attribute = costs[i].Spends;
                if (attribute == null || !Source.TryGetAttributeValue(attribute, out var value)) return false;

                // Every cost that spends this attribute, together
                float total = 0f;
                for (int j = 0; j < costs.Count; j++)
                    if (costs[j] != null && costs[j].Spends == attribute) total += AmountOf(costs[j], data);
                if (value.CurrentValue < total) return false;
            }
            return true;
        }

        // Each attribute cost, spent from its caster's attribute as a change of the caster's own
        private void PayAttributeCosts(ProcessDataPacket data)
        {
            var costs = Base.AttributeCosts;
            if (costs == null || costs.Count == 0 || Source is not ITarget caster) return;
            for (int i = 0; i < costs.Count; i++)
            {
                if (costs[i] == null) continue;
                float amount = AmountOf(costs[i], data);
                var attribute = costs[i].Spends;
                if (amount <= 0f || attribute == null) continue;
                caster.TryModifyAttribute(attribute, SourcedModifiedAttributeValue.GenerateSimple(Source, attribute, -amount, 0f));
            }
        }

        /// <summary>
        /// An effect of the ability's use, from it onto its caster, with the activation's set-by-caller magnitudes, as its
        /// Cost and Cooldown are made, and a Toggle's effect.
        /// </summary>
        public GameplayEffectSpec GenerateUsageEffect(GameplayEffect effect, ProcessDataPacket data = null)
        {
            var spec = Source.GenerateEffectSpec(this, effect);
            if (data != null) spec?.TakeSetByCaller(data.SetByCallerMagnitudes);
            return spec;
        }
            
        public bool ValidateSourceActivationRequirements(AbilityDataPacket data) => SourceRefusal(data, out _, out _) == EActivationRefusal.None;

        /// <summary>
        /// What of the caster refuses the activation, or None: its tags against the ability's source requirements, then
        /// each caster check, as the kind it says it's refused as, a Rule by its name; then its components, each as the
        /// kind it says, by its reason; and why, in the words of the check or the component that refused it.
        /// </summary>
        internal EActivationRefusal SourceRefusal(AbilityDataPacket data, out string rule, out string detail)
        {
            rule = null;
            detail = null;
            var owner = data.EffectOrigin.GetSource();

            // The ability's own source tag requirements ("can't use while stunned") are part of
            // activation, alongside the validation rules. Missing groups mean no requirement.
            if (!(Base.Tags?.TagRequirements?.CheckSourceRequirements(owner.GetAppliedTags()) ?? true)) return EActivationRefusal.Requirements;

            // The crowd control its caster is under, as a stun stops every ability and Silenced a spell; the refusal
            // names the rule's tag
            if (owner is IGameplayAbilitySystem gas && gas.GetCrowdControl() is { } crowdControl
                && crowdControl.Stopping(Base, owner.GetTagCache()) is { } stopping)
            {
                rule = stopping.Tag.ToString();
                return EActivationRefusal.CrowdControl;
            }

            // An empty rule slot, which a list's "+" can leave, asks nothing; it threw. Each is refused as the kind it says,
            // a cooldown's, a cost's and a death's naming none, and any other by its name
            foreach (var check in Base.SourceActivationRules)
            {
                if (check == null || check.Validate(data, () => owner, out var error)) continue;
                detail = error;
                var kind = KindOf(check.RefusedAs);
                if (kind == EActivationRefusal.Rule) rule = check.GetName();
                return kind;
            }

            // Its components, any of which may refuse it, each as the kind it says, named by its reason
            if (Base.Components != null)
            {
                foreach (var component in Base.Components)
                {
                    if (component?.CanStart(new AbilityComponentContext(this, data)) is not { } reason) continue;
                    rule = detail = reason;
                    return KindOf(component.RefusedAs);
                }
            }
            return EActivationRefusal.None;
        }

        // A refusal's kind as its check or component says it; one that says None is a Rule, as it did refuse
        private static EActivationRefusal KindOf(EActivationRefusal said) => said == EActivationRefusal.None ? EActivationRefusal.Rule : said;

        public bool ValidateAllActivationRequirements(ITarget target, AbilityDataPacket data) =>
            TargetRefusal(target, data, out _, out _) == EActivationRefusal.None;

        /// <summary>
        /// What refuses the activation once its targeting has found its target, or None: its caster, again, as
        /// <see cref="SourceRefusal"/> says it, then each of its Target Checks on the target, then the target's tags against
        /// its target requirements, each <see cref="EActivationRefusal.Target"/>, naming the check where one refused it; and
        /// why, in the check's words.
        /// </summary>
        internal EActivationRefusal TargetRefusal(ITarget target, AbilityDataPacket data, out string rule, out string detail)
        {
            var source = SourceRefusal(data, out rule, out detail);
            if (source != EActivationRefusal.None) return source;

            // An empty rule slot asks nothing
            if (Base.TargetActivationRules != null)
            {
                foreach (var check in Base.TargetActivationRules)
                {
                    if (check == null || check.Validate(data, () => target, out var error)) continue;
                    rule = check.GetName();
                    detail = error;
                    return EActivationRefusal.Target;
                }
            }
            if (Base.Tags.ValidateTargetRequirements(target)) return EActivationRefusal.None;
            detail = "Its target fails its target tag requirements";
            return EActivationRefusal.Target;
        }

        public override ISource GetSource() => Source;
        public override IHasReadableDefinition GetReadableDefinition()
        {
            return Base;
        }
        public override List<Tag> GetContextTags()
        {
            return Base.Tags.ContextTags;
        }
        public override Tag GetAssetTag()
        {
            return Base.Tags.AssetTag;
        }
        public override IntValuePairClamped GetLevel() => Source.GetLevel(GetAssetTag());
        public override List<Tag> GetAffiliation()
        {
            return Source.GetAffiliation();
        }
        public override bool IsActive()
        {
            return Source.FindAbilitySystem(out var ab) && ab.TryGetAbilityContainer(Base, out var container) && container.IsActive;
        }
        public bool RetainEffectImpact()
        {
            return false;
        }

        public override string ToString()
        {
            return Base.ToString();
        }
    }
}
