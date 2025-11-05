using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public abstract class AbstractDrawer2 : GasifyRegistry.IFieldDrawer
    {
        public abstract bool CanDraw(Type t);
        public abstract VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut);

        // public abstract VisualElement DrawForSelector(object target, FieldInfo fi, Action onSelect);
        
        protected VisualElement RegisterFocusEvents(VisualElement ve, Action onFocusIn, Action onFocusOut)
        {
            ve.RegisterCallback<FocusInEvent>(_ =>
            {
                onFocusIn?.Invoke();
            });
            ve.RegisterCallback<FocusOutEvent>(_ =>
            {
                onFocusOut?.Invoke();
            });
            return ve;
        }

        protected VisualElement DefaultRow(VisualElement label, VisualElement value, int height = 22)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center,
                    maxHeight = height,
                    backgroundColor = new Color(0.17f, 0.17f, 0.2f, 1f),
                    borderBottomWidth = 1, borderTopWidth = 1,
                    borderBottomColor = new Color(0, 0, 0, .5f),
                    borderTopColor = new Color(0, 0, 0, .5f),
                    justifyContent = Justify.SpaceBetween,
                    paddingLeft = 8, paddingRight = 8,
                    paddingTop = 8, paddingBottom = 8
                }
            };
            row.Add(label);
            row.Add(value);
            return row;
        }
    }
}
