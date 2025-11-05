using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class DictionaryDrawer2 : AbstractDrawer2
    {
        public override bool CanDraw(Type t)
            {
                if (typeof(IDictionary).IsAssignableFrom(t)) return true;
                return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
            }

        public override VisualElement Draw(object target, FieldInfo fi, Action onChanged, Action onFocusIn, Action onFocusOut)
        {
            // Ensure non-null dictionary instance
            var dictObj = fi.GetValue(target);
            if (dictObj == null)
            {
                dictObj = Activator.CreateInstance(fi.FieldType);
                fi.SetValue(target, dictObj);
            }

            // Strong typing helpers
            var isGeneric = fi.FieldType.IsGenericType && fi.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>);
            Type keyType, valType;
            IDictionary dict;

            if (isGeneric)
            {
                var args = fi.FieldType.GetGenericArguments();
                keyType = args[0];
                valType = args[1];
                dict = (IDictionary)dictObj;
            }
            else
            {
                // Fallback for non-generic IDictionary (rare)
                keyType = typeof(object);
                valType = typeof(object);
                dict = (IDictionary)dictObj;
            }

            var box = new Foldout { text = GasifyRegistry.GetDrawerLabel(fi), value = true };

            // Make a stable, bindable list of entries
            var entries = new List<Entry>();
            foreach (DictionaryEntry de in dict)
                entries.Add(new Entry { Key = de.Key, Value = de.Value });

            var registry = GasifyRegistry.DefaultRegistry();

            var lv = new ListView
            {
                itemsSource = entries,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.Single,
                style = { height = 22 }
            };

            lv.makeItem = () =>
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

                // Key slot
                var keySlot = new VisualElement { style = { flexGrow = 1 } };
                // Separator
                var arrow = new Label("→") { style = { width = 16, unityTextAlign = TextAnchor.MiddleCenter } };
                // Value slot
                var valSlot = new VisualElement { style = { flexGrow = 1 } };
                // Remove
                var remove = new Button { text = "−" };
                remove.style.width = 22;

                row.Add(keySlot);
                row.Add(arrow);
                row.Add(valSlot);
                row.Add(remove);

                row.userData = new RowRefs { KeySlot = keySlot, ValSlot = valSlot, RemoveBtn = remove };
                return row;
            };

            lv.bindItem = (ve, i) =>
            {
                if (i < 0 || i >= entries.Count) return;
                var e = entries[i];
                var refs = (RowRefs)ve.userData;

                refs.KeySlot.Clear();
                refs.ValSlot.Clear();

                // Proxies so we can reuse the registry drawers consistently
                var keyFI = new KvpProxyFieldInfo(
                    fieldName: "Key",
                    fieldType: keyType,
                    getter: () => e.Key,
                    setter: newKey =>
                    {
                        // If unchanged, ignore
                        if (KeysEqual(dict, e.Key, newKey)) return;

                        // Reject duplicate keys
                        if (ContainsKey(dict, newKey)) return;

                        // Move value to new key
                        dict.Remove(e.Key);
                        dict[newKey] = e.Value;
                        e.Key = newKey;
                        onChanged?.Invoke();
                    });

                var valFI = new KvpProxyFieldInfo(
                    fieldName: "Value",
                    fieldType: valType,
                    getter: () => e.Value,
                    setter: newVal =>
                    {
                        dict[e.Key] = newVal;
                        e.Value = newVal;
                        onChanged?.Invoke();
                    });

                // Use your registry to draw both sides
                var keyDrawer = registry.Find(keyType);
                var valDrawer = registry.Find(valType);

                // Wrap in tiny cards for readability
                var keyCard = new VisualElement { style = { flexGrow = 1 } };
                keyCard.Add(keyDrawer.Draw(new FieldProxyTarget(), keyFI, onChanged, onFocusIn, onFocusOut));
                refs.KeySlot.Add(keyCard);

                var valCard = new VisualElement { style = { flexGrow = 1 } };
                valCard.Add(valDrawer.Draw(new FieldProxyTarget(), valFI, onChanged, onFocusIn, onFocusOut));
                refs.ValSlot.Add(valCard);

                // Remove button wiring
                if (refs.RemoveBtn.userData is Action old) refs.RemoveBtn.clicked -= old;
                Action onRemove = () =>
                {
                    dict.Remove(e.Key);
                    entries.RemoveAt(i);
                    lv.Rebuild();
                    onChanged?.Invoke();
                };
                refs.RemoveBtn.userData = onRemove;
                refs.RemoveBtn.clicked += onRemove;
            };

            // Add button: creates a default, non-colliding key
            var addBtn = new Button(() =>
                {
                    object newKey = DefaultKey(keyType, dict);
                    object newVal = DefaultValue(valType);

                    // Ensure unique key (especially for ints/enums)
                    int guard = 0;
                    while (ContainsKey(dict, newKey) && guard++ < 512)
                        newKey = NextKey(keyType, newKey);

                    dict[newKey] = newVal;
                    entries.Add(new Entry { Key = newKey, Value = newVal });
                    lv.Rebuild();
                    onChanged?.Invoke();
                })
                { text = "Add" };

            box.Add(lv);
            box.Add(addBtn);
            return box;
        }

        // ————— helpers —————

        class Entry
        {
            public object Key;
            public object Value;
        }

        class RowRefs
        {
            public VisualElement KeySlot;
            public VisualElement ValSlot;
            public Button RemoveBtn;
        }

        class KvpProxyFieldInfo : FieldInfo
        {
            readonly string _name;
            readonly Type _type;
            readonly Func<object> _getter;
            readonly Action<object> _setter;

            public KvpProxyFieldInfo(string fieldName, Type fieldType, Func<object> getter, Action<object> setter)
            {
                _name = fieldName;
                _type = fieldType;
                _getter = getter;
                _setter = setter;
            }

            public override string Name => _name;
            public override Type FieldType => _type;
            public override Type DeclaringType => typeof(FieldProxyTarget);
            public override Type ReflectedType => typeof(FieldProxyTarget);
            public override FieldAttributes Attributes => FieldAttributes.Public;
            public override RuntimeFieldHandle FieldHandle => default;

            public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();
            public override bool IsDefined(Type attributeType, bool inherit) => false;

            public override object GetValue(object obj) => _getter();
            public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture) => _setter(value);
        }

        class FieldProxyTarget
        {
        }

        static bool ContainsKey(IDictionary dict, object key)
        {
            foreach (DictionaryEntry de in dict)
                if (Equals(de.Key, key))
                    return true;
            return false;
        }

        static bool KeysEqual(IDictionary dict, object a, object b) => Equals(a, b);

        static object DefaultKey(Type keyType, IDictionary dict)
        {
            if (keyType == typeof(int)) return 0;
            if (keyType.IsEnum) return Enum.GetValues(keyType).Cast<object>().FirstOrDefault() ?? Activator.CreateInstance(keyType);
            if (keyType == typeof(string)) return "Key";
            // Value types → default(T); ref types → null
            return keyType.IsValueType ? Activator.CreateInstance(keyType) : null;
        }

        static object NextKey(Type keyType, object current)
        {
            if (keyType == typeof(int)) return ((int)(current ?? 0)) + 1;
            if (keyType.IsEnum)
            {
                var vals = Enum.GetValues(keyType).Cast<object>().ToList();
                var idx = Math.Max(0, vals.IndexOf(current));
                return vals[(idx + 1) % vals.Count];
            }

            if (keyType == typeof(string))
            {
                var s = (current as string) ?? "Key";
                // Append/increment numeric suffix
                int num = 1;
                var baseName = s;
                var lastDigits = System.Text.RegularExpressions.Regex.Match(s, @"(.*?)(\d+)$");
                if (lastDigits.Success)
                {
                    baseName = lastDigits.Groups[1].Value;
                    num = int.Parse(lastDigits.Groups[2].Value) + 1;
                }

                return $"{baseName}{num}";
            }

            // Fallback: unchanged
            return current;
        }

        static object DefaultValue(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
    }
}
