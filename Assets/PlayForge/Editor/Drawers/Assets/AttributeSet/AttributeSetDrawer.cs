using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A reference to an attribute set: Unity's object field, with Open or New beside it. It was a button that opened a
    /// dropdown list of its own.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeSet))]
    public class AttributeSetDrawer : AbstractForgeObjectDrawer<AttributeSet>
    {
    }
}
