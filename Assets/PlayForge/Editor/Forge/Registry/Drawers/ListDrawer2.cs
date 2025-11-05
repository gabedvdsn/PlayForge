using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class ListDrawer2 : AbstractDrawer2
    {
        public override bool CanDraw(Type t) => typeof(IList).IsAssignableFrom(t);

        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var root = new VisualElement
            {
                style = { flexDirection = FlexDirection.Column }
            };
            RegisterFocusEvents(root, onFocusIn, onFocusOut);

            var label = new Label(GasifyRegistry.GetDrawerLabel(fi));
            root.Add(label);

            var list = (IList)fi.GetValue(target);
            var listType = fi.FieldType;
            var elemType = listType.IsArray ? listType.GetElementType()! : listType.GetGenericArguments()[0];

            if (list == null)
            {
                list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(elemType));
                Debug.Log(fi.Name);
                fi.SetValue(target, list);
                onChanged?.Invoke();
            }

            var content = new VisualElement { style = { flexDirection = FlexDirection.Column, marginLeft = 8 } };
            root.Add(content);

            void Redraw()
            {
                content.Clear();
                for (int i = 0; i < list.Count; i++)
                {
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    content.Add(row);

                    var idxLabel = new Label($"[{i}]") { style = { minWidth = 28 } };
                    row.Add(idxLabel);

                    var remove = new Button(() =>
                    {
                        list.RemoveAt(i);
                        fi.SetValue(target, list);
                        onChanged?.Invoke();
                        Redraw();
                    }) { text = "–", tooltip = "Remove" };
                    row.Add(remove);

                    var elem = list[i];
                    if (elem == null && elemType.IsClass)
                    {
                        elem = Activator.CreateInstance(elemType);
                        list[i] = elem;
                        fi.SetValue(target, list);
                        onChanged?.Invoke();
                    }
                    if (elem == null && elemType.IsValueType)
                        elem = Activator.CreateInstance(elemType);

                    var drawer = GasifyRegistry.DefaultRegistry().Find(elemType);

                    // Critical: if it's a struct, any child change must be written back into list[i]
                    var child = drawer.Draw(elem, elem?.GetType().GetField("value") ?? null, () =>
                    {
                        if (elemType.IsValueType)
                        {
                            list[i] = elem;
                            fi.SetValue(target, list);
                        }
                        onChanged?.Invoke();
                    }, null, null);

                    // The line above uses a hacky field lookup; instead draw directly by creating a shim FieldInfo:
                    // Simpler: call a helper that draws a fieldless value by wrapping with a fake FieldInfo.
                    child = DrawValue(elemType, () => list[i], v =>
                    {
                        list[i] = v;
                        fi.SetValue(target, list);
                        onChanged?.Invoke();
                    });

                    row.Add(child);
                }
            }

            // Add button
            var add = new Button(() =>
            {
                var v = Activator.CreateInstance(elemType);
                list.Add(v);
                fi.SetValue(target, list);
                onChanged?.Invoke();
                Redraw();
            }) { text = "+ Add" };
            root.Add(add);

            Redraw();
            return root;
        }

        // A tiny helper that draws a standalone value (no FieldInfo available) using a drawer.
        VisualElement DrawValue(Type valueType, Func<object> getter, Action<object> setter)
        {
            var container = new VisualElement { style = { flexGrow = 1 } };

            // Create a tiny shim object that holds a single public field "V"
            var shimType = typeof(ValueShim<>).MakeGenericType(valueType);
            var field = shimType.GetField("V");
            var shim = Activator.CreateInstance(shimType);
            field.SetValue(shim, getter());

            var drawer = GasifyRegistry.DefaultRegistry().Find(valueType);
            var child = drawer.Draw(shim, field, () =>
            {
                setter(field.GetValue(shim));
            }, null, null);

            container.Add(child);
            return container;
        }

        // Holds a single public field for reuse of Draw(object, FieldInfo,...)
        public class ValueShim<T> { public T V; }
    }
}
