using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(Tag))]
    public class TagDrawer : AbstractRefDrawer<Tag>
    {
        protected override Tag[] GetEntries()
        {
            return new[]
            {
                Tag.Generate("Melee"), Tag.Generate("Poison"), Tag.Generate("Burning"), Tag.Generate("Flying"), Tag.Generate("Self-Cast"), Tag.Generate("Bow & Arrow"), Tag.Generate("Tuber"), Tag.Generate("Glamping"), Tag.Generate("Computer"), Tag.Generate("Electronics"), Tag.Generate("Organic"), Tag.Generate("Apparent")
            };
            
            //return ForgeRegistry.GetAllTags();
        }
        protected override bool AcceptClear()
        {
            return false;
        }
        protected override bool CompareTo(Tag value, Tag other)
        {
            return value.Name == other.Name;
        }
        protected override string GetStringValue(SerializedProperty prop, Tag value)
        {
            return value.Name != string.Empty ? value.Name : "<None>";
        }

        protected override void SetValue(SerializedProperty prop, Tag value)
        {
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp is not null) nameProp.stringValue = value.Name;
        }
        protected override Tag GetCurrentValue(SerializedProperty prop)
        {
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp is not null) return Tag.Generate(nameProp.stringValue);

            return default;
        }
        protected override Label GetLabel(SerializedProperty prop, Tag value)
        {
            if (prop.isArray) return null;
            return new Label(prop.name);
        }
    }
}
