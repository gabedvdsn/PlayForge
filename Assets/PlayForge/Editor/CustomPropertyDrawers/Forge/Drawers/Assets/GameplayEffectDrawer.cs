using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(GameplayEffect))]
    public class GameplayEffectDrawer : AbstractRefDrawer<GameplayEffect>
    {

        protected override GameplayEffect[] GetEntries()
        {
            return GetAllInstances<GameplayEffect>();
        }
        protected override bool CompareTo(GameplayEffect value, GameplayEffect other)
        {
            return value == other;
        }
        protected override string GetStringValue(SerializedProperty prop, GameplayEffect value)
        {
            string l = value?.GetName() ?? "<None>";
            return string.IsNullOrEmpty(l) ? "<Unnamed>" : l;
        }
        protected override void SetValue(SerializedProperty prop, GameplayEffect value)
        {
            prop.objectReferenceValue = value;
        }
        protected override GameplayEffect GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as GameplayEffect;
        }
        protected override Label GetLabel(SerializedProperty prop, GameplayEffect value)
        {
            if (prop.isArray) return null;
            return new Label(prop.name);
        }
    }
}
