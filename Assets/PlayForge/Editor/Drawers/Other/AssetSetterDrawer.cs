using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    [CustomPropertyDrawer(typeof(AbstractSetAssetsTask))]
    public class AssetSetterDrawer : AbstractTypeRefDrawer<AbstractSetAssetsTask>
    {
    }
}
