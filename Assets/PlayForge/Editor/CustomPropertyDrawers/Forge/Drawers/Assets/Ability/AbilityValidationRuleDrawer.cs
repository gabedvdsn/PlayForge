using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An ability's validation rule: its kind, named without "ValidationRule" or "Validation", then the kind's own fields
    /// (ED-56). A rule is always of some kind, so the picker offers no None.
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
    }
}
