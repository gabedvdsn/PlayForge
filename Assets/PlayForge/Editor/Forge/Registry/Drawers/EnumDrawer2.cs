using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class EnumDrawer2 : GasifyRegistry.IFieldDrawer
    {
        public bool CanDraw(Type t) => t.IsEnum;
        public VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var val = fi.GetValue(target);
            var f = new EnumField(GasifyRegistry.GetDrawerLabel(fi), (Enum)val);
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
