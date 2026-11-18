using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    [ValidationSide(EValidationSide.Caster)]
    [ForgeMenu(Description = "Activates only where the caster can pay the cost.")]
    public class CostValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = null;
            if (data?.EffectOrigin is not AbilitySpec ability)
            {
                error = ValidationText.NoActivation;
                return false;
            }

            // A component that waives the cost asks nothing of it, as a toggle turning off pays nothing
            if (ability.Waives(EUsageCheck.Cost, data)) return true;

            // Its attribute costs, each paid beside its Cost effect
            if (!ability.CanPayAttributeCosts(data))
            {
                error = "Its caster can't pay its attribute costs";
                return false;
            }
            if (ability.Base.Cost is null) return true;

            // Generating the spec rebuilds the cost's impacts, and each attribute it spends is checked: a cost can spend
            // more than one, and only the first was. The caster must have each attribute
            var spec = ability.Source.GenerateEffectSpec(ability, ability.Base.Cost);
            var impacts = ability.Base.Cost.ImpactSpecification.Entries;
            if (impacts.Count == 0)
            {
                error = $"Its cost, {ValidationText.NameOf(ability.Base.Cost)}, spends nothing";
                return false;
            }
            foreach (var impact in impacts)
            {
                if (impact == null) continue;
                if (!ability.Source.TryGetAttributeValue(impact.AttributeTarget, out var attrVal))
                {
                    error = $"Its caster has no {ValidationText.NameOf(impact.AttributeTarget)} to pay with";
                    return false;
                }

                // A cost's magnitude is the signed change it makes, so what matters is what that leaves. Comparing
                // the value with the magnitude itself passed every cost, as a cost is negative
                float change = impact.Magnitude?.Evaluate(spec) ?? 0f;
                if (attrVal.CurrentValue + change < 0f)
                {
                    error = $"Its caster's {ValidationText.NameOf(impact.AttributeTarget)}, {attrVal.CurrentValue:0.##}, can't pay {-change:0.##}";
                    return false;
                }
            }
            return true;
        }

        public string GetName() => "Cost";

        /// <summary>Told as Cost.</summary>
        public EActivationRefusal RefusedAs => EActivationRefusal.Cost;
    }

    [Serializable]
    [ValidationSide(EValidationSide.Caster)]
    [ForgeMenu(Description = "Activates only where the ability isn't cooling down, or with its charges counted, has one in hand.")]
    public class CooldownValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = null;
            if (data?.EffectOrigin is not AbilitySpec ability)
            {
                error = ValidationText.NoActivation;
                return false;
            }

            // A component that waives the cooldown passes it, as a toggle can always turn off, its cooldown starting as it
            // does
            if (ability.Waives(EUsageCheck.Cooldown, data)) return true;

            // Refused only with no use in hand, as its cooldown model says: with its charges counted, none in hand,
            // whatever else carries its cooldown's tag; with the single cooldown, while it's on
            var model = ability.CooldownModel;
            if (model.InHand(ability) > 0) return true;
            error = model.NoneInHand(ability);
            return false;
        }

        public string GetName() => "Cooldown";

        /// <summary>Told as Cooldown.</summary>
        public EActivationRefusal RefusedAs => EActivationRefusal.Cooldown;
    }

    [Serializable]
    [ForgeMenu(Description = "Activates only while the checked entity lives: the caster among the Caster Checks, the target among the Target Checks.")]
    public class IsAliveValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            // A missing entity is refused: reading it threw
            var subject = getSource?.Invoke();
            if (subject == null)
            {
                error = ValidationText.NoOne;
                return false;
            }
            error = subject.IsDead ? $"{ValidationText.NameOf(subject)} is dead" : null;
            return error == null;
        }

        public string GetName() => "Is Alive";

        /// <summary>Told as Dead, where it refuses its caster.</summary>
        public EActivationRefusal RefusedAs => EActivationRefusal.Dead;
    }

    [Serializable]
    [ForgeMenu(Description = "Activates only where an attribute of the checked entity, or its share of its base, compares with a value.")]
    public class AttributeValidation : AbstractAttributeValidationRule
    {
        public EComparisonOperator Comparison;

        [Tooltip("The value it compares with, read at the ability's level")]
        public ScalableFloat Value = 0f;

        /// <summary>
        /// Compares the attribute's share of its base, from 0 to 1, in place of its value: "a target below half its
        /// health" whatever its health. It compared the value alone, right for one entity and wrong for the next.
        /// </summary>
        [Tooltip("Compare the attribute's share of its base, from 0 to 1, in place of its value")]
        public bool ShareOfBase;

        public override bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            var subject = getSource?.Invoke();
            if (subject is null)
            {
                error = ValidationText.NoOne;
                return false;
            }

            if (!subject.TryGetAttributeValue(Attribute, out var attrVal))
            {
                error = $"{ValidationText.NameOf(subject)} has no {ValidationText.NameOf(Attribute)}";
                return false;
            }

            // Its share of its base, where the rule asks for one
            float value = !ShareOfBase ? attrVal.CurrentValue
                : attrVal.BaseValue > 0f ? attrVal.CurrentValue / attrVal.BaseValue : 0f;
            float compared = Value?.Evaluate(NumberContext.Of(data)) ?? 0f;
            if (NumberComparison.Holds(value, Comparison, compared))
            {
                error = null;
                return true;
            }
            error = $"{ValidationText.NameOf(subject)}'s {ValidationText.NameOf(Attribute)}{(ShareOfBase ? " share" : "")}, {value:0.##}, " +
                    $"isn't {NumberComparison.Symbol(Comparison)} {compared:0.##}";
            return false;
        }

        public override string GetName() => "Attribute";
    }

    /// <summary>
    /// A rule that reads a tag, as Tag Weight does: its weight on the entity it checks, the caster among the Caster Checks
    /// and the target among the Target Checks, or, where that entity holds none of it, the ability's Local Data under it.
    /// </summary>
    [Serializable]
    public abstract class AbstractTagValidationRule : IAbilityValidationRule
    {
        [ForgeTagContext(ForgeContext.Granted, ForgeContext.AssetTag, ForgeContext.LocalDataKey)]
        public Tag Tag;

        /// <summary>The tag's weight on an entity, 0 where it holds none of it.</summary>
        protected int WeightOn(ITarget subject) => subject == null ? 0 : Math.Max(0, subject.GetTagWeight(Tag));

        /// <summary>
        /// The tag's value for a check: its weight on the entity checked, or, where that entity holds none of it, the
        /// ability's Local Data under it; null with neither.
        /// </summary>
        public virtual DataWrapper GetValue(AbilityDataPacket data, ITarget subject)
        {
            int weight = subject?.GetTagWeight(Tag) ?? -1;
            if (weight >= 0) return new DataWrapper(Tag, new LocalInt { Value = weight });
            return data?.EffectOrigin is AbilitySpec ability && ability.Base.TryGetLocalData(Tag, out var local) ? local : null;
        }

        public abstract bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error);
        public abstract string GetName();
    }

    [Serializable]
    [ForgeMenu(Name = "Tag Weight", Description = "Activates only where the checked entity holds a tag at a weight that compares with a number, as combo points or stacks.")]
    public class TagWeightValidation : AbstractTagValidationRule
    {
        [Tooltip("How the tag's weight compares with the number")]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterOrEqual;

        [Tooltip("The weight it compares with, read at the ability's level; an entity without the tag holds it at 0")]
        public ScalableInt Weight = 1;

        public override bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            var subject = getSource?.Invoke();
            if (subject == null)
            {
                error = ValidationText.NoOne;
                return false;
            }
            int weight = WeightOn(subject);
            float compared = Weight?.Evaluate(NumberContext.Of(data)) ?? 0;
            if (NumberComparison.Holds(weight, Comparison, compared))
            {
                error = null;
                return true;
            }
            error = $"{ValidationText.NameOf(subject)} holds {Tag} at {weight}, not {NumberComparison.Symbol(Comparison)} {compared:0.##}";
            return false;
        }

        public override string GetName() => "Tag Weight";
    }

    [Serializable]
    [ForgeMenu(Name = "Has Effect", Description = "Activates only where the checked entity has an effect on, by asset or by tag; or, with Without, where it hasn't.")]
    public class HasEffectValidation : IAbilityValidationRule
    {
        [Tooltip("The effect it looks for, or none to look by Tagged alone")]
        public GameplayEffect Effect;

        [Tooltip("Or any effect tagged with this or one under it, by its asset tag, a context tag or a tag it grants, as a cleanse names them")]
        [ForgeTagContext(ForgeContext.AssetTag, ForgeContext.ContextTag, ForgeContext.Granted)]
        public Tag Tagged;

        [Tooltip("Passes where the entity has none of it, in place of where it has")]
        public bool Without;

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = null;
            // Naming no effect, it asks nothing
            if (Effect == null && !Tagged.IsValid) return true;

            var subject = getSource?.Invoke();
            if (subject == null)
            {
                error = ValidationText.NoOne;
                return false;
            }
            var effects = (subject as IGameplayAbilitySystem)?.GetEffectSystem();
            bool has = effects != null && ((Effect != null && effects.HasEffect(Effect)) || (Tagged.IsValid && effects.HasEffectTagged(Tagged)));
            if (has != Without) return true;

            var what = Effect != null ? ValidationText.NameOf(Effect) : $"an effect tagged {Tagged}";
            error = $"{ValidationText.NameOf(subject)} {(Without ? "has" : "hasn't")} {what} on";
            return false;
        }

        public string GetName() => "Has Effect";
    }

    [Serializable]
    [ForgeMenu(Name = "Holds Item", Description = "Activates only where the checked entity holds an item; or, with Equipped, wears it.")]
    public class HoldsItemValidation : IAbilityValidationRule
    {
        [Tooltip("The item it must hold")]
        public Item Item;

        [Tooltip("It must wear the item, not only hold it")]
        public bool Equipped;

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = null;
            // Naming no item, it asks nothing
            if (Item == null) return true;

            var subject = getSource?.Invoke();
            if (subject == null)
            {
                error = ValidationText.NoOne;
                return false;
            }
            var items = (subject as IGameplayAbilitySystem)?.GetItemSystem();
            if (items != null && (Equipped ? items.IsItemEquipped(Item) : items.HasItem(Item))) return true;
            error = $"{ValidationText.NameOf(subject)} doesn't {(Equipped ? "wear" : "hold")} {ValidationText.NameOf(Item)}";
            return false;
        }

        public string GetName() => "Holds Item";
    }

    [Serializable]
    [ValidationSide(EValidationSide.Caster)]
    [ForgeMenu(Name = "Ability Level", Description = "Activates only where the ability's level compares with a number, as a form it unlocks at a rank.")]
    public class AbilityLevelValidation : IAbilityValidationRule
    {
        [Tooltip("How the ability's level compares with the number")]
        public EComparisonOperator Comparison = EComparisonOperator.GreaterOrEqual;

        [Tooltip("The level it compares with")]
        [Min(0)] public int Level = 2;

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            if (data?.EffectOrigin is not AbilitySpec ability)
            {
                error = ValidationText.NoActivation;
                return false;
            }
            int level = ability.GetLevel().CurrentValue;
            if (NumberComparison.Holds(level, Comparison, Level))
            {
                error = null;
                return true;
            }
            error = $"Its level, {level}, isn't {NumberComparison.Symbol(Comparison)} {Level}";
            return false;
        }

        public string GetName() => "Ability Level";
    }

    /// <summary>
    /// A rule that holds while its condition does, as an effect's requirements and an On Hit read theirs: a tag, an
    /// attribute, a level, a comparison of numbers, or any condition of your own. It's read against the activation, its
    /// caster and its ability's level and Local Data, with the entity its list hands it as the target: the target among the
    /// Target Checks, and the caster itself among the Caster Checks. With no condition, it holds.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Activates only while its condition holds: a tag, an attribute, a level, or any other condition.")]
    public class ConditionValidation : IAbilityValidationRule
    {
        [Tooltip("What must hold, read against the activation, with the entity this list checks as its target")]
        [SerializeReference] public GameplayCondition When;

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = null;
            if (When == null) return true;
            var subject = getSource?.Invoke();
            var context = NumberContext.Of(data);
            if (subject != null) context = context.WithTarget(subject);
            if (When.Holds(in context)) return true;
            error = $"{NumberText.Of(When)} doesn't hold{(subject != null ? $" for {ValidationText.NameOf(subject)}" : "")}";
            return false;
        }

        public string GetName() => "Condition";
    }

    /// <summary>How a rule names what it checks as it says why it refused.</summary>
    internal static class ValidationText
    {
        /// <summary>Why a rule handed no entity refuses.</summary>
        public const string NoOne = "There's no one to check";

        /// <summary>Why a rule that asks of an ability's activation refuses outside one.</summary>
        public const string NoActivation = "It isn't asked of an ability's activation";

        /// <summary>A thing's name as a refusal says it: its PlayForge name, its object's, or "nothing".</summary>
        public static string NameOf(object thing) => thing switch
        {
            null => "nothing",
            IHasReadableDefinition named when !string.IsNullOrEmpty(named.GetName()) => named.GetName(),
            UnityEngine.Object unity when unity => unity.name,
            _ => thing.ToString()
        };
    }
}
