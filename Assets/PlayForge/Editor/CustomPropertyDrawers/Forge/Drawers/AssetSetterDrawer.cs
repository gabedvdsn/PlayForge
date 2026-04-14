using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AbstractSetAssetsTask))]
    public class AssetSetterDrawer : AbstractTypeRefDrawer<AbstractSetAssetsTask>
    {
        protected override Type GetDefault(SerializedProperty prop)
        {
            return typeof(SetAssetsTask);
        }
    }
}
