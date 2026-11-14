using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySpec : BasicEffectOrigin
    {
        public Ability Base;
        
        /// <summary>Its charges on its caster: how many it holds, how many are in hand, and what its recharge counts (TW-17).</summary>
        public AbilityCharges Charges { get; }

        public AbilitySpec(ISource source, Ability ability, int level) : base(source, new IntValuePairClamped(level, ability.StartingLevel, ability.MaxLevel), ability.Tags.AssetTag)
        {
            Base = ability;
            Charges = new AbilityCharges(this);
        }

        private ComponentStates componentStates;

        /// <summary>What its ability's components keep for this entity's ability (FN-14).</summary>
        public ComponentStates ComponentStates => componentStates ??= new ComponentStates();

        // Its ability's components, each hearing a moment of it (FN-14)
        internal void HearGranted() => Hear(component => component.Granted(new AbilityComponentContext(this)));
        internal void HearRemoved() => Hear(component => component.Removed(new AbilityComponentContext(this)));
        internal void HearStart(ProcessDataPacket data) => Hear(component => component.Start(new AbilityComponentContext(this, data)));
        internal void HearEnd(ProcessDataPacket data, EAbilityEnd how) => Hear(component => component.End(new AbilityComponentContext(this, data), how));
        private void HearCommit(ProcessDataPacket data) => Hear(component => component.Commit(new AbilityComponentContext(this, data)));

        private void Hear(Action<AbstractAbilityComponent> moment)
        {
            if (Base.Components == null) return;
            foreach (var component in Base.Components)
                if (component != null) moment(component);
        }

        /// <summary>
        /// Applies the ability's cooldown and cost to its caster, each with the magnitudes the caller set on the
        /// activation's data, so a caller can size them as it sizes the ability's other effects (FEAT-2).
        /// </summary>
        public void ApplyUsageEffects(ProcessDataPacket data = null)
        {
            // One that toggles an effect turns it on or off in their place (TW-20)
            if (Base.Toggles is not null)
            {
                Toggle(data);
                HearCommit(data);
                return;
            }

            SpendCooldown(data);
            PayCosts(data);
            HearCommit(data);
        }

        // A charge spent, as its Recharge says, where its charges are counted; or else its cooldown (TW-17)
        private void SpendCooldown(ProcessDataPacket data)
        {
            if (Charges.Counted) Charges.Spend(data);
            else ApplyCooldown(data, ownContainer: false);
        }

        // Its Cost effect, and beside it each of its attribute costs (TW-29)
        private void PayCosts(ProcessDataPacket data)
        {
            if (Base.Cost is not null) Source.ApplyCarrier(Usage(Base.Cost, data));
            PayAttributeCosts(data);
        }

        /// <summary>Whether the effect it toggles is on its caster, put there by it (TW-20).</summary>
        /// <summary>Takes its toggled effect off its caster, where it's on: as its holder forgets the ability (FN-14).</summary>
        internal void ToggleOff()
        {
            if (IsToggledOn) Source.ToGAS()?.GetEffectSystem()?.RemoveGameplayEffect(Base.Toggles, this);
        }

        public bool IsToggledOn =>
            Base.Toggles is { } toggles && Source?.ToGAS()?.GetEffectSystem() is { } effects && effects.CountFrom(toggles, this) > 0;

        // On, with the activation's magnitudes, paying its cost; or off, paying nothing and starting its cooldown (TW-20)
        private void Toggle(ProcessDataPacket data)
        {
            if (IsToggledOn)
            {
                Source.ToGAS()?.GetEffectSystem()?.RemoveGameplayEffect(Base.Toggles, this);
                SpendCooldown(data);
                return;
            }
            Source.ApplyCarrier(Usage(Base.Toggles, data));
            PayCosts(data);
        }

        /// <summary>
        /// Applies its Cooldown to its caster, with the activation's magnitudes; as a container of its own where a spent
        /// charge's runs alone (TW-17). Nothing, where it has no Cooldown.
        /// </summary>
        internal void ApplyCooldown(ProcessDataPacket data, bool ownContainer)
        {
            if (Base.Cooldown is null) return;
            var spec = Usage(Base.Cooldown, data);
            if (spec == null) return;
            spec.OwnContainer = ownContainer;
            Source.ApplyCarrier(spec);
        }

        /// <summary>Whether its caster carries its Cooldown's asset tag, as an ordinary cooldown's check asks.</summary>
        internal bool OnCooldown => Base.Cooldown is { } cooldown && Source.GetTagCache().TryGetWeight(cooldown.Tags.AssetTag, out _);

        /// <summary>
        /// What an attribute cost comes to for an activation, at the ability's level, read in the activation's data where
        /// it's given; never under 0 (TW-29).
        /// </summary>
        public float AmountOf(AttributeCost cost, ProcessDataPacket data = null)
        {
            if (cost?.Amount == null) return 0f;
            var context = data is AbilityDataPacket activation && ReferenceEquals(activation.EffectOrigin, this)
                ? NumberContext.Of(data)
                : new NumberContext(Source, null, GetLevel(), true, this, null, data?.SetByCallerMagnitudes, null, data);
            return Mathf.Max(0f, cost.Amount.Evaluate(context));
        }

        /// <summary>
        /// Whether its caster can pay its attribute costs: one that comes to 0 asks nothing, and the rest need their
        /// attribute found, their own or their role's in the project, with as much of it on the caster as all of them that
        /// spend it come to (TW-29).
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

        // Each attribute cost, spent from its caster's attribute as a change of the caster's own (TW-29)
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

        private GameplayEffectSpec Usage(GameplayEffect effect, ProcessDataPacket data)
        {
            var spec = Source.GenerateEffectSpec(this, effect);
            if (data != null) spec?.TakeSetByCaller(data.SetByCallerMagnitudes);
            return spec;
        }
            
        public bool ValidateSourceActivationRequirements(AbilityDataPacket data) => SourceRefusal(data, out _) == EActivationRefusal.None;

        /// <summary>
        /// What of the caster refuses the activation, or None: its tags against the ability's source requirements, then
        /// each caster check, which a cooldown, a cost or a death names, and any other by its name (FEAT-17).
        /// </summary>
        internal EActivationRefusal SourceRefusal(AbilityDataPacket data, out string rule)
        {
            rule = null;
            var owner = data.EffectOrigin.GetSource();

            // The ability's own source tag requirements ("can't use while stunned") are part of
            // activation, alongside the validation rules. Missing groups mean no requirement.
            if (!(Base.Tags?.TagRequirements?.CheckSourceRequirements(owner.GetAppliedTags()) ?? true)) return EActivationRefusal.Requirements;

            // The crowd control its caster is under, as a stun stops every ability and Silenced a spell; the refusal
            // names the rule's tag (TW-16)
            if (owner is IGameplayAbilitySystem gas && gas.GetCrowdControl() is { } crowdControl
                && crowdControl.Stopping(Base, owner.GetTagCache()) is { } stopping)
            {
                rule = stopping.Tag.ToString();
                return EActivationRefusal.CrowdControl;
            }

            // An empty rule slot, which a list's "+" can leave, asks nothing; it threw (ED-12)
            foreach (var check in Base.SourceActivationRules)
            {
                if (check == null || check.Validate(data, () => owner, out _)) continue;
                switch (check)
                {
                    case CooldownValidation: return EActivationRefusal.Cooldown;
                    case CostValidation: return EActivationRefusal.Cost;
                    case IsAliveValidation: return EActivationRefusal.Dead;
                    default:
                        rule = check.GetName();
                        return EActivationRefusal.Rule;
                }
            }

            // Its components, any of which may refuse it, named by its reason (FN-14)
            if (Base.Components != null)
            {
                foreach (var component in Base.Components)
                {
                    if (component?.CanStart(new AbilityComponentContext(this, data)) is not { } reason) continue;
                    rule = reason;
                    return EActivationRefusal.Rule;
                }
            }
            return EActivationRefusal.None;
        }

        public bool ValidateAllActivationRequirements(ITarget target, AbilityDataPacket data)
        {
            return ValidateSourceActivationRequirements(data)
                   && Base.TargetActivationRules.All(rule => rule == null || rule.Validate(data, () => target, out _))
                   && Base.Tags.ValidateTargetRequirements(target) ;
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
