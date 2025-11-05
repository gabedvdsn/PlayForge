using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended
{
    #region Attributes

    

    #endregion

    public static class GasifyRegistry
    {
        #region Helpers

        // Utilities shared by drawers
        public static string GetDrawerLabel(FieldInfo fi)
        {
            var lbl = fi.GetCustomAttribute<ForgeLabelAttribute>()?.Label ?? ObjectNames.NicifyVariableName(fi.Name);
            return lbl;
        }

        public static VisualElement BuildRowBase(FieldInfo fi)
        {
            var root = new VisualElement()
            {
                name = "FieldRow",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 0,
                    width = 300,
                    minWidth = 300,
                    maxWidth = 300,
                    backgroundColor = new Color(0.17f, 0.17f, 0.2f, 1f),
                    alignItems = Align.Auto
                }
            };
            return root;
        }

        public static void StyleInput(TextField tf)
        {
            var input = tf.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(0.14f, 0.14f, 0.16f, 1f);
                input.style.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            }
        }

        #endregion

        #region Internal

        public static FieldDrawerRegistry DefaultRegistry() =>
            new FieldDrawerRegistry();

        public interface IFieldDrawer
        {
            bool CanDraw(Type t);
            VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut);
        }

        public class FieldDrawerRegistry
        {
            readonly List<IFieldDrawer> _drawers = new();
            public FieldDrawerRegistry Register(IFieldDrawer d)
            {
                _drawers.Add(d);
                return this;
            }

            public IFieldDrawer Find(Type t)
            {
                /*// exact first
                var d = _drawers.FirstOrDefault(x => x.CanDraw(t));
                if (d != null) return d;
                // enums are handled by the enum drawer
                if (t.IsEnum) return _drawers.First(x => x is EnumDrawer2);*/
                // fallback: string drawer as last resort
                return _drawers.First(x => x is null);
            }
        }

        #endregion
    }

}
