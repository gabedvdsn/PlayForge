using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(IAbilityInjection))]
    public class AbilityInjectionDrawer : AbstractGenericDrawer<IAbilityInjection>
    {
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return false;
        }
        protected override bool AcceptAdd()
        {
            return false;
        }
        protected override Type GetDefault()
        {
            return typeof(InterruptInjection);
        }

        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            string v = base.GetStringValue(prop, value);
            return v.Replace("Injection", "");
        }
    }
}
