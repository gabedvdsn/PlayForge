using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(Ability))]
    public class AbilityDrawer : AbstractRefDrawer<Ability>
    {
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return true;
        }
        protected override Ability[] GetEntries()
        {
            return GetAllInstances<Ability>();
        }
        protected override bool CompareTo(Ability value, Ability other)
        {
            return value == other;
        }
        protected override string GetStringValue(SerializedProperty prop, Ability value)
        {
            string l = value != null ? value.GetName() ?? "<None>" : "<None>";
            return string.IsNullOrEmpty(l) ? "<Unnamed>" : l;
        }
        protected override void SetValue(SerializedProperty prop, Ability value)
        {
            prop.objectReferenceValue = value;
        }
        protected override Ability GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as Ability;
        }
        protected override Label GetLabel(SerializedProperty prop, Ability value)
        {
            if (prop.isArray) return null;
            return new Label(prop.name);
        }
    }
}
