using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class IntDrawer2 : GasifyRegistry.IFieldDrawer
    {
        public bool CanDraw(Type t) => t == typeof(int);
        public VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var f = new IntegerField(GasifyRegistry.GetDrawerLabel(fi)) { value = (int)(fi.GetValue(target) ?? 0) };
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
