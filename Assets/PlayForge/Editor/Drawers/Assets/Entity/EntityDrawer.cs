using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A reference to an entity: Unity's object field, with Open or New beside it. It was a button that opened a
    /// dropdown list of its own.
    /// </summary>
    [CustomPropertyDrawer(typeof(EntityIdentity))]
    public class EntityDrawer : AbstractForgeObjectDrawer<EntityIdentity>
    {
    }
}
