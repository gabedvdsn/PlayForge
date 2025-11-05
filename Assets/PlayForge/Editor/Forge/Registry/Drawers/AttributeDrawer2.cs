using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class AttributeDrawer2 : AbstractDrawer2
    {

        public override bool CanDraw(Type t)
        {
            return t.FullName == "FESGameplayAbilitySystem.Attribute";
        }
        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Label(GasifyRegistry.GetDrawerLabel(fi)) { style = { minWidth = 140 }});

            var tf = new TextField { value = fi.GetValue(target)?.ToString() ?? "" };
            tf.RegisterValueChangedCallback(e =>
            {
                var gen = typeof(Attribute).GetMethod("Generate", BindingFlags.Public | BindingFlags.Static);
                fi.SetValue(target, gen.Invoke(null, new object[] { e.newValue ?? "" }));
                onChanged?.Invoke();
            });

            RegisterFocusEvents(tf, onFocusIn, onFocusOut);
            row.Add(tf);
            return row;
        }
    }
}
