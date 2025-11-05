using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class FloatDrawer2 : GasifyRegistry.IFieldDrawer
    {
        public bool CanDraw(Type t) => t == typeof(float);
        public VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var f = new FloatField(GasifyRegistry.GetDrawerLabel(fi)) { value = (float)(fi.GetValue(target) ?? 0f) };
            f.RegisterValueChangedCallback(e =>
            {
                fi.SetValue(target, e.newValue);
                onChanged?.Invoke();
            });
            f.RegisterCallback<FocusOutEvent>(_ =>
            {
                onFocusOut?.Invoke();
            });
            return f;
        }
    }
}
