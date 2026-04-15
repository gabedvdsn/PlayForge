using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public abstract class AbstractForgeObjectDrawer<T> : AbstractRefDrawer<T> where T : BaseForgeAsset
    {
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return GetCurrentValue(prop) != null;
        }
        
        protected override string GetStringValue(SerializedProperty prop, T value)
        {
            string l = value != null ? value.GetName() ?? "<None>" : "<None>";
            return string.IsNullOrEmpty(l) ? "<Unnamed>" : l;
        }
        protected override T[] GetEntries()
        {
            return GetAllInstances<T>();
        }
        protected override bool CompareTo(T value, T other)
        {
            return value == other;
        }
        protected override void SetValue(SerializedProperty prop, T value)
        {
            prop.objectReferenceValue = value;
            UpdateButtonVisibility(prop);
        }
        protected override T GetCurrentValue(SerializedProperty prop)
        {
            return prop.objectReferenceValue as T;
        }
        protected override Label GetLabel(SerializedProperty prop, T value)
        {
            if (prop.isArray) return null;
            return new Label(prop.displayName);
        }
        protected override T GetDefault(SerializedProperty prop)
        {
            return GetCurrentValue(prop);
        }
        protected override void ClearReferenceValue(SerializedProperty prop)
        {
            prop.objectReferenceValue = null;
        }
    }
}
