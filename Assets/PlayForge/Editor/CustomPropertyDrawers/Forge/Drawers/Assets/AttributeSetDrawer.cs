using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AttributeSet))]
    public class AttributeSetDrawer : AbstractRefDrawer<AttributeSet>
    {
        protected override AttributeSet[] GetEntries()
        {
            return GetAllInstances<AttributeSet>();
        }
        protected override bool CompareTo(AttributeSet value, AttributeSet other)
        {
            return value == other;
        }
        protected override string GetStringValue(SerializedProperty prop, AttributeSet value)
        {
            string l = value != null ? value.GetName() ?? "<None>" : "<None>";
            return string.IsNullOrEmpty(l) ? "<Unnamed>" : l;
        }
        protected override void SetValue(SerializedProperty prop, AttributeSet value)
        {
            prop.objectReferenceValue = value;
        }
        protected override AttributeSet GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as AttributeSet;
        }
        protected override Label GetLabel(SerializedProperty prop, AttributeSet value)
        {
            if (prop.isArray) return null;
            return new Label(prop.name);
        }
        protected override bool AcceptClear()
        {
            return true;
        }
    }
}
