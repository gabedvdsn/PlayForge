using System;
using System.Reflection;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class TagDrawer2 : AbstractDrawer2
    {

        public override bool CanDraw(Type t)
        {
            return t.FullName == "FESGameplayAbilitySystem.Tag";
        }
        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Label(GasifyRegistry.GetDrawerLabel(fi)) { style = { minWidth = 140 }});

            row.Clear();
            var label = new Label(GasifyRegistry.GetDrawerLabel(fi)) { style = { minWidth = 140 } };
            var value = new TextField { value = fi.GetValue(target)?.ToString() ?? "" };
            row = DefaultRow(new Label(GasifyRegistry.GetDrawerLabel(fi)) { style = { minWidth = 140 } }, value);
            
            value.RegisterValueChangedCallback(e =>
            {
                var gen = typeof(Tag).GetMethod("Generate", BindingFlags.Public | BindingFlags.Static);
                fi.SetValue(target, gen.Invoke(null, new object[] { e.newValue ?? "" }));
                onChanged?.Invoke();
            });

            RegisterFocusEvents(value, onFocusIn, onFocusOut);
            return row;
        }
    }
}
