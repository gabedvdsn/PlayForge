using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class StringDrawer2 : AbstractDrawer2
    {
        public override bool CanDraw(Type t) => t == typeof(string);
        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var tf = new TextField(GasifyRegistry.GetDrawerLabel(fi)) { value = (string)fi.GetValue(target) ?? "" };
            GasifyRegistry.StyleInput(tf);
            tf.RegisterValueChangedCallback(e =>
            {
                fi.SetValue(target, e.newValue);
                onChanged?.Invoke();
            });
            RegisterFocusEvents(tf, onFocusIn, onFocusOut);
            return tf;
        }
    }
}
