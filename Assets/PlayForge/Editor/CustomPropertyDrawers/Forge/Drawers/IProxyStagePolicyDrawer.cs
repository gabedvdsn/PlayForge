using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(IProxyStagePolicy))]
    public class IProxyStagePolicyDrawer : AbstractTypeRefDrawer<IProxyStagePolicy>
    {
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) 
                return base.GetStringValue(prop, value);
            
            // Clean up the display name
            var name = value.Name;
            name = name.Replace("ProxyStagePolicy", "");
            name = name.Replace("Policy", "");
            
            // Add spaces before capitals
            var sb = new System.Text.StringBuilder();
            foreach (char c in name)
            {
                if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
                sb.Append(c);
            }
            
            return sb.ToString();
        }
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return false;
        }
        protected override Type GetDefault()
        {
            var types = TypePickerCache.GetConcreteTypesAssignableTo<IProxyStagePolicy>();
            if (types.Count > 0) return types[0];
            return base.GetDefault();
        }

    }
}
