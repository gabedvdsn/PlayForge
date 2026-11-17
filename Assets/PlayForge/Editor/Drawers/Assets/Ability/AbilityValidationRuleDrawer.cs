using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// An ability's validation rule: its kind, as its [ForgeMenu] names it or named without "ValidationRule" or
    /// "Validation", then the kind's own fields. A rule is always of some kind, so the picker offers no None. Each list
    /// is offered the kinds of its side, the Caster Checks a caster's and the Target Checks a target's, with those of
    /// either; a check a component stands behind, as Check Range, is offered to neither, as it comes with its component.
    /// </summary>
    [CustomPropertyDrawer(typeof(IAbilityValidationRule))]
    public class AbilityValidationRuleDrawer : AbstractTypeRefDrawer<IAbilityValidationRule>
    {
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) return base.GetStringValue(prop, value);
            var name = value.Name.Replace("ValidationRule", "").Replace("Validation", "");
            return ObjectNames.NicifyVariableName(name);
        }

        protected override bool AcceptClear(SerializedProperty prop) => false;

        // A check a component stands behind comes with its component, and no list takes one by hand
        protected override bool FilterType(Type type) => !typeof(IComponentValidationRule).IsAssignableFrom(type);

        // A kind of one side is offered to that side's list alone
        protected override bool Offers(SerializedProperty prop, Type type)
        {
            var side = ValidationSideAttribute.Of(type);
            if (side == EValidationSide.Either || prop == null) return true;
            var path = prop.propertyPath;
            if (path.StartsWith(nameof(Ability.SourceActivationRules), StringComparison.Ordinal)) return side == EValidationSide.Caster;
            if (path.StartsWith(nameof(Ability.TargetActivationRules), StringComparison.Ordinal)) return side == EValidationSide.Target;
            return true;
        }
    }
}
