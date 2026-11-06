using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A proxy stage's policy: its kind, named without "ProxyStagePolicy" or "Policy", always of some kind (ED-56).
    /// </summary>
    [CustomPropertyDrawer(typeof(IAbilityProxyStagePolicy))]
    public class IProxyStagePolicyDrawer : AbstractTypeRefDrawer<IAbilityProxyStagePolicy>
    {
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) return base.GetStringValue(prop, value);
            var name = value.Name.Replace("ProxyStagePolicy", "").Replace("Policy", "");
            return ObjectNames.NicifyVariableName(name);
        }

        protected override bool AcceptClear(SerializedProperty prop) => false;
    }
}
