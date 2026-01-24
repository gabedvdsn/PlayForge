using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(Attribute))]
    public class AttributeDrawer : AbstractRefDrawer<Attribute>
    {
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return true;
        }
        protected override bool AcceptClear()
        {
            return true;
        }
        protected override Attribute[] GetEntries()
        {
            return GetAllInstances<Attribute>();
        }
        protected override bool CompareTo(Attribute value, Attribute other)
        {
            return value == other;
        }
        protected override string GetStringValue(SerializedProperty prop, Attribute value)
        {
            string l = value != null ? value.GetName() ?? "<None>" : "<None>";
            return string.IsNullOrEmpty(l) ? "<Unnamed>" : l;
        }
        protected override void SetValue(SerializedProperty prop, Attribute value)
        {
            prop.objectReferenceValue = value;
        }
        protected override Attribute GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as Attribute;
        }
        protected override Label GetLabel(SerializedProperty prop, Attribute value)
        {
            if (prop.isArray) return null;
            return new Label(prop.name);
        }
    }
}
