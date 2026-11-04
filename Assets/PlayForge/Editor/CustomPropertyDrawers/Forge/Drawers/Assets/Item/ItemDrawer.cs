using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A reference to an item: Unity's object field, with Open or New beside it (ED-38). It was a button that opened a
    /// dropdown list of its own.
    /// </summary>
    [CustomPropertyDrawer(typeof(Item))]
    public class ItemDrawer : AbstractForgeObjectDrawer<Item>
    {
    }
}
