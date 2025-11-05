// CompositeDrawer.cs
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine; // <-- for FormatterServices
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class CompositeDrawer2 : AbstractDrawer2
    {
        public override bool CanDraw(Type t)
        {
            if (t == typeof(string)) return false;
            if (t.IsEnum)            return false;
            if (t.IsPrimitive)       return false;

            return t.IsClass || t.IsValueType;
        }

        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            var fieldType = fi.FieldType;
            var label     = GasifyRegistry.GetDrawerLabel(fi);

            // === Current value or create one (safely) ===
            var current = fi.GetValue(target);

            // If field type is abstract/interface, show a type picker first
            if (current == null && (fieldType.IsAbstract || fieldType.IsInterface))
                return BuildConcreteTypePicker(label, target, fi, fieldType, onChanged);

            if (current == null)
            {
                if (!TryCreateInstance(fieldType, out current))
                {
                    // Nothing we can do -> explain why
                    var warn = new Label($"{label} : cannot instantiate {fieldType.Name}. Provide a concrete type or a default constructor.")
                    {
                        style = { opacity = 0.85f }
                    };
                    return warn;
                }

                // assign the new instance to the parent
                fi.SetValue(target, current);
                onChanged?.Invoke();
            }

            // We use a Foldout as the container for the composite’s fields
            var root = new Foldout { text = label, value = false };
            RegisterFocusEvents(root, onFocusIn, onFocusOut);

            // If this type is mirrored, we *draw* using the source shape, but
            // we *get/set* against the runtime/generated instance.
            var drawType = fieldType.GetCustomAttribute<MirrorFromAttribute>()?.SourceType ?? fieldType;

            foreach (var sub in GetEditableFields(drawType))
            {
                var runtimeSub = ResolveRuntimeField(fieldType, drawType, sub);
                if (runtimeSub == null) continue;

                var drawer = GasifyRegistry.DefaultRegistry().Find(runtimeSub.FieldType);

                var child = drawer.Draw(
                    current,
                    runtimeSub,
                    () =>
                    {
                        // If the composite itself is a struct, push the boxed copy
                        if (fieldType.IsValueType)
                            fi.SetValue(target, current);
                        onChanged?.Invoke();
                    },
                    null, null);

                root.Add(child);
            }

            return root;
        }

        // ---------- helpers ----------

        static bool TryCreateInstance(Type t, out object instance)
        {
            instance = null;

            // Value types (incl. structs) always have a default
            if (t.IsValueType)
            {
                instance = Activator.CreateInstance(t);
                return true;
            }

            // Don’t try to create abstract/interface
            if (t.IsAbstract || t.IsInterface)
                return false;

            // Public parameterless ctor?
            var ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
            {
                instance = ctor.Invoke(null);
                return true;
            }

#if UNITY_EDITOR
            // Editor only: create without running any ctor. This lets you edit fields
            // on classes with no default ctor. (Don’t use at runtime.)
            try
            {
                instance = FormatterServices.GetUninitializedObject(t);
                return true;
            }
            catch { /* fall through */ }
#endif
            return false;
        }

        VisualElement BuildConcreteTypePicker(string label, object target, FieldInfo fi, Type abstractType, Action onChanged)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Label(label) { style = { flexGrow = 1 } });

            var pickBtn = new Button { text = "Pick Type…" };
            pickBtn.clicked += () =>
            {
                var menu = new UnityEditor.GenericMenu();
                var types = TypePickerCache.GetConcreteTypesAssignableTo(abstractType)
                                           .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                                           .ToList();

                if (types.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("No concrete types found"));
                }
                else
                {
                    foreach (var t in types)
                    {
                        var nice = UnityEditor.ObjectNames.NicifyVariableName(t.Name);
                        menu.AddItem(new GUIContent(nice), false, () =>
                        {
                            if (TryCreateInstance(t, out var inst))
                            {
                                fi.SetValue(target, inst);
                                onChanged?.Invoke();
                                // Force the inspector/creator to rebuild this area so the new
                                // concrete instance gets its editable sub-fields.
                                row.schedule.Execute(() =>
                                {
                                    // Simple signal: replace the picker with a short note
                                    row.Clear();
                                    row.Add(new Label($"{label} : {nice} created. Collapse/expand to refresh."));
                                }).ExecuteLater(0);
                            }
                            else
                            {
                                UnityEditor.EditorUtility.DisplayDialog("Create Instance",
                                    $"Could not create instance of {t.FullName}. Make sure it has a public parameterless constructor (or we can use editor-only uninitialized creation).",
                                    "OK");
                            }
                        });
                    }
                }

                menu.ShowAsContext();
            };

            row.Add(pickBtn);
            return row;
        }

        static FieldInfo ResolveRuntimeField(Type runtimeType, Type sourceType, FieldInfo sourceFi)
        {
            // Name match (the generator keeps names stable, even if it wraps types into AssetRef/TypeRef)
            return runtimeType.GetField(sourceFi.Name, BindingFlags.Public | BindingFlags.Instance);
        }

        static FieldInfo[] GetEditableFields(Type type)
        {
            // Base-first order + honor [GasifyHidden] and [GasifyOrder]
            return type
                .BaseTypesFirst()
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(f => !f.IsDefined(typeof(ForgeHiddenAttribute), true))
                .Select(f => (f, order: f.GetCustomAttribute<ForgeOrderAttribute>()?.Order ?? int.MaxValue, token: f.MetadataToken))
                .OrderBy(x => x.order).ThenBy(x => x.token)
                .Select(x => x.f)
                .ToArray();
        }
    }

    static class TypeExt
    {
        public static System.Collections.Generic.IEnumerable<Type> BaseTypesFirst(this Type t)
        {
            var chain = new System.Collections.Generic.List<Type>();
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType) chain.Add(cur);
            chain.Reverse();
            return chain;
        }
    }
}
