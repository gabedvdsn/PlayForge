using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public abstract class AbstractRefDrawer<T> : PropertyDrawer
    {
        protected abstract T[] GetEntries();
        protected abstract bool CompareTo(T value, T other);
        protected abstract string GetStringValue(SerializedProperty prop, T value);
        protected abstract void SetValue(SerializedProperty prop, T value);
        protected abstract T GetCurrentValue(SerializedProperty prop);
        protected abstract Label GetLabel(SerializedProperty prop, T value);
        protected virtual bool AcceptOpen(SerializedProperty prop) => true;
        protected virtual bool AcceptClear() => true;
        protected virtual bool AcceptAdd() => true;
        protected virtual T GetDefault() => default;

        protected Label CurateLabel(Label label, SerializedProperty prop, T value)
        {
            if (IsInList(prop)) return null;
            
            label.text += "\t";
            label.style.marginRight = 4;
            label.style.marginLeft = 4;
            label.style.minWidth = 90;
            
            return label;
        }
        
        private bool IsInList(SerializedProperty property)
        {
            // Check for array element pattern in path
            return property.propertyPath.Contains(".Array.data[");
        }

        protected virtual T[] ApplyValidation(SerializedProperty property, T currValue, T[] entries)
        {
            return entries;
            
            /*var defaultAttrs = GetFieldAttributes<ForgeFilterName>(property).ToArray();
            if (defaultAttrs.Length == 0) return entries;

            return entries.Where(e => defaultAttrs.Any(a => a.Names.Any(_name => e.StartsWith(_name, StringComparison.InvariantCultureIgnoreCase)))).ToArray();*/
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            return CreateGUI(prop);
        }

        protected S[] GetAllInstances<S>() where S : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(S).Name}");

            return guids.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<S>)
                .ToArray();
        }

        // Helper class to store drawer state
        private class DrawerState
        {
            public ListView list;
            public List<T> results = new List<T>();
            public bool isOpen;
        }
        
        private VisualElement CreateGUI(SerializedProperty prop)
        {
            var root = new VisualElement();

            var state = new DrawerState();
            root.userData = state;
            
            var container = new VisualElement()
            {
                style =
                {
                    flexGrow = 1, flexShrink = 1,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center, alignSelf = Align.Stretch,
                    minHeight = 24, maxHeight = 24
                }
            };

            var label = CurateLabel(GetLabel(prop, GetCurrentValue(prop)), prop, GetCurrentValue(prop));
            if (label is not null) container.Add(label);

            var value = new Button()
            {
                name = "ValueButton",
                text = GetStringValue(prop, GetCurrentValue(prop)),
                focusable = false,
                style =
                {
                    flexGrow = 1, flexShrink = 0,
                    marginLeft = 0, marginRight = 0,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    backgroundColor = new Color(.3f, .3f, .3f),
                    minHeight = 22, maxHeight = 22
                }
            };
            
            var clear = new Button()
            {
                text = "X",
                focusable = false,
                tooltip = "Clear",
                style =
                {
                    marginLeft = 4, marginRight = 0,
                    paddingTop = 4, paddingBottom = 6, paddingLeft = 4, paddingRight = 4
                }
            };

            clear.clicked += () =>
            {
                OnSelectItem(prop, default, value, null, null);
            };
            
            var open = new Button()
            {
                text = ">",
                focusable = false,
                tooltip = "Open in Manager",
                style =
                {
                    marginLeft = 4, marginRight = 6,
                    paddingTop = 4, paddingBottom = 6, paddingLeft = 4, paddingRight = 4
                }
            };
            
            container.Add(value);
            if (AcceptClear()) container.Add(clear);
            if (AcceptOpen(prop)) container.Add(open);
            
            root.Add(container);

            var dd = new VisualElement()
            {
                style =
                {
                    flexShrink = 0, flexDirection = FlexDirection.Row,
                    maxHeight = 180,
                    marginLeft = 4, marginRight = 4,
                    display = DisplayStyle.None
                }
            };

            var bar = new VisualElement()
            {
                style =
                {
                    flexGrow = 0, flexShrink = 1,
                    minWidth = 3, maxWidth = 3,
                    marginLeft = 3,
                    backgroundColor = new Color(.3f, .3f, .3f)
                }
            };

            dd.Add(bar);

            var ddContainer = new VisualElement()
            {
                style =
                {
                    flexGrow = 1, flexShrink = 1,
                    maxHeight = 180, marginRight = 4, marginLeft = 4
                }
            };

            var searchContainer = new VisualElement()
            {
                style =
                {
                    flexGrow = 1, flexShrink = 1,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Stretch, alignSelf = Align.Stretch,
                    minHeight = 24, maxHeight = 24, marginBottom = 2
                }
            };

            var search = new TextField()
            {
                value = "",
                style =
                {
                    flexGrow = 1, flexShrink = 0, marginLeft = 0, maxHeight = 22, minHeight = 22,
                    marginRight = 0, fontSize = 12, unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            
            var add = new Button()
            {
                text = "+",
                focusable = false,
                tooltip = "Create New...",
                style =
                {
                    //display = DisplayStyle.None,
                    marginLeft = 4, marginRight = -2,
                    paddingTop = 4, paddingBottom = 6, paddingLeft = 4, paddingRight = 4,
                    minHeight = 22, maxHeight = 22
                }
            };
            
            searchContainer.Add(search);
            if (AcceptAdd()) searchContainer.Add(add);
            
            ddContainer.Add(searchContainer);
            
            state.list = new ListView()
            {
                name = "List"
            };

            ddContainer.Add(state.list);

            value.clicked += () => ShowDropdown(prop, value, search, dd, add, state);
            
            search.RegisterCallback<FocusInEvent>(_ =>
            {
                //ShowDropdown(prop, search, dd, add); 
            });
            
            search.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (evt.relatedTarget is not null && IsDropdownElement(evt.relatedTarget, state)) return;
                HideDropdown(prop, dd);
            });

            dd.Add(ddContainer);
            
            root.Add(dd);

            return root;
        }
        
        void RefreshDropdownResults(SerializedProperty prop, string query, T[] entries, Button addButton, DrawerState state)
        {
            state.results = entries.ToList();
            
            if (query != string.Empty)
            {
                state.results = entries.Where(e => GetStringValue(prop, e).Contains(query)).ToList();
            }
            else
            {
                state.results = entries.ToList();
                
            }

            /*addButton.style.display = query != string.Empty && !entries.Contains(query) ? DisplayStyle.Flex : DisplayStyle.None;
            */

            state.list.itemsSource = state.results;
            state.list.Rebuild();
        }

        private void ShowDropdown(SerializedProperty prop, Button btn, TextField inp, VisualElement dd, Button addButton, DrawerState state)
        {
            if (state.isOpen)
            {
                state.isOpen = false;
                HideDropdown(prop, dd);
                return;
            }

            state.isOpen = true;
            
            dd.style.display = DisplayStyle.Flex;
            T value = GetCurrentValue(prop);

            //inp.value = GetStringValue(value);
            inp.Focus();
            
            var entries = GetEntries();
            
            var validated = ApplyValidation(prop, value, entries).ToArray();

            state.results = validated.ToList();
            state.list.itemsSource = state.results;
            
            inp.RegisterValueChangedCallback(evt =>
            {
                RefreshDropdownResults(prop, evt.newValue, validated, addButton, state);
            });
            
            state.list.makeItem = () => new Button()
            {
                focusable = false,
                enabledSelf = true,
                style =
                {
                    flexGrow = 1,
                    marginLeft = 0, marginRight = 44,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    minHeight = 20, maxHeight = 20,
                }
            };
            
            state.list.bindItem = (entry, i) =>
            {
                var item = state.results[i];
                var button = entry as Button;

                button.style.backgroundColor = StyleKeyword.Null;
                button.enabledSelf = true;

                //Debug.Log($"Comparing item: '{GetStringValue(item)}' to '{inp.value}' => {CompareTo(item, GetCurrentValue(prop))}");
                if (CompareTo(item, GetCurrentValue(prop)))
                {
                    button.style.backgroundColor = new StyleColor(new Color(.25f, .25f, .25f, 1f));
                    button.enabledSelf = false;
                }

                var stringValue = GetStringValue(prop, item);
                
                button.text = stringValue;

                if (button.userData is Action oldAction) button.clicked -= oldAction;
                
                var action = new Action(() => OnSelectItem(prop, item, btn, inp, dd));
                button.userData = action;
                button.clicked += action;
            };
            
            state.list.Rebuild();

            
        }
        
        void OnSelectItem(SerializedProperty prop, T item, Button value, TextField inp, VisualElement dd)
        {
            SetValue(prop, item);

            value.text = GetStringValue(prop, item);
            if (inp is not null) inp.value = GetStringValue(prop, item);
                
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);

            HideDropdown(prop, dd);
        }
        
        private bool IsDropdownElement(IEventHandler handler, DrawerState state)
        {
            var element = handler as VisualElement;
            return element is not null && state.list.Contains(element);
        }
        
        private void HideDropdown(SerializedProperty prop, VisualElement dd)
        {
            if (dd is not null) dd.style.display = DisplayStyle.None;
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var nameProp = property.FindPropertyRelative("Name");
            bool showHelp = false;

            string _name = nameProp.stringValue;
            if (_name is null) showHelp = true;
            
            return EditorGUIUtility.singleLineHeight + (showHelp ? EditorGUIUtility.singleLineHeight + 2 : 0);
        }
        
        static readonly Dictionary<(Type host, string path, Type attr), object[]> _attrCache = new();

        /// <summary>Get attributes defined on the serialized field being drawn.</summary>
        protected IEnumerable<TAttr> GetFieldAttributes<TAttr>(SerializedProperty property) where TAttr : System.Attribute
        {
            if (property is null) return Array.Empty<TAttr>();
            
            // 1) Try Unity's supplied fieldInfo first (works for most cases)
            if (fieldInfo != null)
                return fieldInfo.GetCustomAttributes(typeof(TAttr), inherit: true).Cast<TAttr>();

            // 2) Fallback: resolve FieldInfo by walking propertyPath
            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0) return Enumerable.Empty<TAttr>();
            var hostType = targets[0].GetType();
            var key = (hostType, property.propertyPath, typeof(TAttr));
            if (_attrCache.TryGetValue(key, out var cached))
                return cached.Cast<TAttr>();

            var fi = ResolveFieldInfo(hostType, property.propertyPath);
            var arr = fi != null
                ? fi.GetCustomAttributes(typeof(TAttr), inherit: true).Cast<TAttr>().ToArray()
                : Array.Empty<TAttr>();

            _attrCache[key] = arr.Cast<object>().ToArray();
            return arr;
        }

        // Walks something like "myList.Array.data[2].field.subField"
        static FieldInfo ResolveFieldInfo(Type host, string propertyPath)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type curType = host;
            FieldInfo lastField = null;

            // split path
            var parts = propertyPath.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];

                if (p == "Array")
                {
                    // Next should be "data[<index>]"
                    i++; // skip "data[x]"
                    // move type to element type
                    if (curType.IsArray)
                        curType = curType.GetElementType();
                    else if (curType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(curType))
                        curType = curType.GetGenericArguments()[0];
                    continue;
                }

                // normal field step
                var fi = curType.GetField(p, flags);
                if (fi == null) return lastField; // bail out with best guess
                lastField = fi;
                curType = fi.FieldType;
            }

            return lastField;
        }
    }
}
