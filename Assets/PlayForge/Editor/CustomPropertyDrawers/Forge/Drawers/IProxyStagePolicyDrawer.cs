using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(IProxyStagePolicy))]
    public class IProxyStagePolicyDrawer : AbstractTypeRefDrawer<IProxyStagePolicy>
    {
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            return value is null ? base.GetStringValue(prop, value) : value.Name.Replace("ProxyStagePolicy", "");
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
