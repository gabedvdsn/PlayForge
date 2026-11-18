using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// An asset loader, as an ability's Asset Loaders list holds one: its kind, picked from the Set tasks, as "Set
    /// Effects" for a <c>SetEffectsTask</c>, then the kind's own fields.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractSetAssetsTask))]
    public class AssetSetterDrawer : AbstractTypeRefDrawer<AbstractSetAssetsTask>
    {
    }
}
