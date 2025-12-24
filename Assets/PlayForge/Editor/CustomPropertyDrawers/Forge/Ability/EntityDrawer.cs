using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(EntityIdentity))]
    public class EntityDrawer : AbstractRefDrawer<EntityIdentity>
    {

        protected override EntityIdentity[] GetEntries()
        {
            return GetAllInstances<EntityIdentity>();
        }
        protected override bool CompareTo(EntityIdentity value, EntityIdentity other)
        {
            return value == other;
        }
        protected override string GetStringValue(SerializedProperty prop, EntityIdentity value)
        {
            string l = value != null ? value.GetName() ?? "<None>" : "<None>";
            return string.IsNullOrEmpty(l) ? "<Unnamed>" : l;
        }
        protected override void SetValue(SerializedProperty prop, EntityIdentity value)
        {
            prop.objectReferenceValue = value;
        }
        protected override EntityIdentity GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as EntityIdentity;
        }
        protected override Label GetLabel(SerializedProperty prop, EntityIdentity value)
        {
            if (prop.isArray) return null;
            return new Label(prop.name);
        }
    }
}
