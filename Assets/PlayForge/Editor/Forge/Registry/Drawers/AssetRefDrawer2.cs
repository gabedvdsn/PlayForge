using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class AssetRefDrawer2 : AbstractDrawer2
    {
        public override bool CanDraw(Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition().Name == "AssetRef`1";
        }
        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            Debug.Log($"Try draw asset ref {fi.FieldType} {fi.Name}");
            var t = fi.FieldType;
            var elemT = t.GetGenericArguments()[0];

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Label(GasifyRegistry.GetDrawerLabel(fi)) { style = { minWidth = 140 }});

            var objField = new ObjectField { objectType = elemT, allowSceneObjects = false };

            var inst = fi.GetValue(target) ?? Activator.CreateInstance(fi.FieldType);
            fi.SetValue(target, inst);

            var editorObjField = t.GetField("editorObject", BindingFlags.NonPublic | BindingFlags.Instance);
            if (editorObjField != null) objField.value = editorObjField.GetValue(inst) as UnityEngine.Object;

            objField.RegisterValueChangedCallback(e =>
            {
                var method = t.GetMethod("EditorSet", BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    string Compute(UnityEngine.Object o) => AssetDatabase.GetAssetPath(o);
                    method.Invoke(inst, new object[] { e.newValue, (Func<UnityEngine.Object, string>)Compute });
                    fi.SetValue(target, inst);
                    onChanged?.Invoke();
                }
            });

            RegisterFocusEvents(objField, onFocusIn, onFocusOut);
            row.Add(objField);
            return row;
        }
    }
}
