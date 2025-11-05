using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class BoolDrawer2 : AbstractDrawer2
    {
        public override bool CanDraw(Type t) => t == typeof(bool);
        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var toggle = new Toggle(GasifyRegistry.GetDrawerLabel(fi)) { value = (bool)(fi.GetValue(target) ?? false) };
            toggle.RegisterValueChangedCallback(e =>
            {
                fi.SetValue(target, e.newValue);
                onChanged?.Invoke();
            });
            RegisterFocusEvents(toggle, onFocusIn, onFocusOut);
            return toggle;
        }
    }
}
