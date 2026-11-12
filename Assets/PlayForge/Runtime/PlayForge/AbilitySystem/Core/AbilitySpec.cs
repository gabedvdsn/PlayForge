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
        /// Applies the ability's cooldown and cost to its caster, each with the magnitudes the caller set on the
        /// activation's data, so a caller can size them as it sizes the ability's other effects (FEAT-2).
        /// </summary>
        public void ApplyUsageEffects(ProcessDataPacket data = null)
        {
            // Apply cost and cooldown effects
            if (Base.Cooldown is not null) Source.ApplyCarrier(Usage(Base.Cooldown, data));

            if (Base.Cost is not null) Source.ApplyCarrier(Usage(Base.Cost, data));

            // Or its Mana Cost, in the project's Mana, where it has no Cost effect (TW-28)
            else PayManaCost(data);
        }

        /// <summary>
        /// What its Mana Cost comes to for an activation, at its level, read in the activation's data where it's given; 0
        /// where it has a Cost effect, which is paid in its place, or no Mana Cost (TW-28).
        /// </summary>
        public float ManaCostOf(ProcessDataPacket data = null)
        {
            if (Base.Cost is not null || Base.ManaCost == null) return 0f;
            var context = data is AbilityDataPacket activation && ReferenceEquals(activation.EffectOrigin, this)
                ? NumberContext.Of(data)
                : new NumberContext(Source, null, GetLevel(), true, this, null, data?.SetByCallerMagnitudes, null, data);
            return Mathf.Max(0f, Base.ManaCost.Evaluate(context));
        }

        /// <summary>
        /// Whether its caster can pay its Mana Cost: it costs nothing, or its caster has as much of the project's Mana. With
        /// no Mana of the project's, or none of the caster's, a cost can't be paid (TW-28).
        /// </summary>
        public bool CanPayManaCost(ProcessDataPacket data = null)
        {
            float cost = ManaCostOf(data);
            if (cost <= 0f) return true;
            var mana = ForgeAttributes.Mana;
            return mana != null && Source.TryGetAttributeValue(mana, out var value) && value.CurrentValue >= cost;
        }

        // Its Mana Cost, spent from its caster's Mana as a change of the caster's own
        private void PayManaCost(ProcessDataPacket data)
        {
            float cost = ManaCostOf(data);
            var mana = ForgeAttributes.Mana;
            if (cost <= 0f || mana == null || Source is not ITarget caster) return;
            caster.TryModifyAttribute(mana, SourcedModifiedAttributeValue.GenerateSimple(Source, mana, -cost, 0f));
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
