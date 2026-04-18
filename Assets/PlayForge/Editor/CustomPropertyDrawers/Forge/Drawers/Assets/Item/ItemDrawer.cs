using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Property drawer for Item references.
    /// Provides a clean dropdown interface for selecting items with preview info.
    /// </summary>
    [CustomPropertyDrawer(typeof(Item))]
    public class ItemDrawer : AbstractForgeObjectDrawer<Item>
    {
        /*protected override Item[] GetEntries()
        {
            return GetAllInstances<Item>();
        }

        protected override bool CompareTo(Item value, Item other)
        {
            return value == other;
        }

        protected override string GetStringValue(SerializedProperty prop, Item value)
        {
            if (value == null) return "<None>";
            
            var displayName = value.GetName();
            if (string.IsNullOrEmpty(displayName))
                displayName = value.name;
            
            // Add level info
            if (value.MaxLevel > 1)
                return $"{displayName} (Lv.{value.StartingLevel}-{value.MaxLevel})";
            
            return displayName;
        }

        protected override void SetValue(SerializedProperty prop, Item value)
        {
            prop.objectReferenceValue = value;
        }

        protected override Item GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as Item;
        }

        protected override Label GetLabel(SerializedProperty prop, Item value)
        {
            return new Label(prop.displayName);
        }*/
    }
}