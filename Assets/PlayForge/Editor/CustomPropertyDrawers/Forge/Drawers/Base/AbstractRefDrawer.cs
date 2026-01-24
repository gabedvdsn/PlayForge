using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net.Appender;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Minimalist reference picker drawer base class.
    /// Provides a clean dropdown interface for selecting from a list of entries.
    /// </summary>
    public abstract class AbstractRefDrawer<T> : PropertyDrawer
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods - Must be implemented by derived classes
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected abstract T[] GetEntries();
        protected abstract bool CompareTo(T value, T other);
        protected abstract string GetStringValue(SerializedProperty prop, T value);
        protected abstract void SetValue(SerializedProperty prop, T value);
        protected abstract T GetCurrentValue(SerializedProperty prop);
        protected abstract Label GetLabel(SerializedProperty prop, T value);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Virtual Methods - Override in derived classes as needed
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Whether to show the "Open/Navigate" button. Override to control visibility.</summary>
        protected virtual bool AcceptOpen(SerializedProperty prop) => false;
        
        /// <summary>Whether to show the "Add/Create" button. Override to control visibility.</summary>
        protected virtual bool AcceptAdd() => false;

        protected virtual bool AcceptClear() => false;
        
        /// <summary>Gets the default value when none is selected.</summary>
        protected virtual T GetDefault() => default;

        /// <summary>Called when the open button is clicked. Override to implement navigation.</summary>
        protected virtual void OnOpen(SerializedProperty prop, T value)
        {
            if (prop.objectReferenceValue is null) return;
            if (value is not Object record) return;
            Selection.activeObject = record; EditorGUIUtility.PingObject(record);
        }
        
        /// <summary>Called when the add button is clicked. Override to implement creation.</summary>
        protected virtual void OnAdd(SerializedProperty prop, string searchText) { }
        
        /// <summary>Apply any filtering/validation to entries. Override to customize.</summary>
        protected virtual T[] ApplyValidation(SerializedProperty property, T currValue, T[] entries)
        {
            return entries;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected S[] GetAllInstances<S>() where S : BaseForgeObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(S).Name}");
            return guids.Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<S>)
                .Where(s => s != null)
                .ToArray();
        }
        
        private bool IsInList(SerializedProperty property)
        {
            return property.propertyPath.Contains(".Array.data[");
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // State Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        private class DrawerState
        {
            public bool IsOpen;
            public List<T> Results = new List<T>();
            public T[] AllEntries;
            public string SearchText = "";
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Main Entry Point
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            var root = new VisualElement { name = "RefDrawerRoot" };
            var state = new DrawerState();
            root.userData = state;
            
            // Main row
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            mainRow.style.minHeight = 20;
            root.Add(mainRow);
            
            // Label (if not in list)
            if (!IsInList(prop))
            {
                var label = GetLabel(prop, GetCurrentValue(prop));
                if (label != null)
                {
                    label.style.minWidth = 120;
                    label.style.marginRight = 4;
                    label.style.color = Colors.LabelText;
                    mainRow.Add(label);
                }
            }
            
            // Value button (the main picker)
            var valueBtn = CreateValueButton(prop);
            mainRow.Add(valueBtn);
            
            // Dropdown container (hidden by default)
            var dropdown = CreateDropdown(prop, state, valueBtn);
            root.Add(dropdown);
            
            if (AcceptClear())
            {
                var clearBtn = CreateIconButton(Icons.Clear, "Clear", () =>
                {
                    prop.objectReferenceValue = null;

                    prop.serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(prop.serializedObject.targetObject);
                    SelectItem(prop, default, valueBtn, state, dropdown);
                }, Colors.AccentRed);
                clearBtn.style.marginLeft = 4;
                //clearBtn.style.bac
                mainRow.Add(clearBtn);
            }
            
            // Open button (optional)
            if (AcceptOpen(prop))
            {
                var openBtn = CreateIconButton(Icons.Arrow, "Open", () => OnOpen(prop, GetCurrentValue(prop)), Colors.AccentBlue);
                //openBtn.style.marginLeft = 4;
                mainRow.Add(openBtn);
            }
            
            // Wire up value button click
            valueBtn.clicked += () => ToggleDropdown(prop, state, dropdown, valueBtn);
            
            return root;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // UI Components
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Button CreateValueButton(SerializedProperty prop)
        {
            var currentValue = GetCurrentValue(prop);
            var displayText = GetStringValue(prop, currentValue);
            
            var btn = new Button { name = "ValueButton", text = displayText, focusable = false };
            btn.style.flexGrow = 1;
            btn.style.height = 20;
            btn.style.marginLeft = 0;
            btn.style.marginRight = 0;
            btn.style.paddingLeft = 6;
            btn.style.paddingRight = 6;
            btn.style.fontSize = 11;
            btn.style.unityTextAlign = TextAnchor.MiddleLeft;
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
            
            return btn;
        }
        
        private Button CreateIconButton(string icon, string tooltip, Action onClick, Color color)
        {
            var btn = new Button { text = icon, tooltip = tooltip, focusable = false };
            btn.style.width = 20;
            btn.style.height = 20;
            btn.style.marginLeft = 2;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.fontSize = 10;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = color;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = color);
            
            btn.clicked += onClick;
            return btn;
        }
        
        private VisualElement CreateDropdown(SerializedProperty prop, DrawerState state, Button valueBtn)
        {
            var dropdown = new VisualElement { name = "Dropdown" };
            dropdown.style.display = DisplayStyle.None;
            dropdown.style.marginTop = 2;
            dropdown.style.marginLeft = IsInList(prop) ? 0 : 120;
            dropdown.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);
            dropdown.style.borderTopLeftRadius = 4;
            dropdown.style.borderTopRightRadius = 4;
            dropdown.style.borderBottomLeftRadius = 4;
            dropdown.style.borderBottomRightRadius = 4;
            dropdown.style.borderLeftWidth = 2;
            dropdown.style.borderLeftColor = Colors.AccentBlue;
            dropdown.style.paddingTop = 4;
            dropdown.style.paddingBottom = 4;
            dropdown.style.paddingLeft = 4;
            dropdown.style.paddingRight = 4;
            
            // Search row
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.marginBottom = 4;
            dropdown.Add(searchRow);
            
            var searchField = new TextField { value = "" };
            searchField.style.flexGrow = 1;
            searchField.style.height = 20;
            searchField.style.fontSize = 11;
            //searchField.textInput.style.paddingLeft = 4;
            searchField.contentContainer.style.paddingLeft = 4;
            
            // Placeholder styling
            if (string.IsNullOrEmpty(searchField.value))
            {
                searchField.value = "";
            }
            
            searchRow.Add(searchField);
            
            // Add button (optional)
            if (AcceptAdd())
            {
                var addBtn = CreateIconButton("+", "Create New", () =>
                {
                    OnAdd(prop, state.SearchText);
                    CloseDropdown(state, dropdown);
                }, Colors.AccentGreen);
                addBtn.style.marginLeft = 4;
                //addBtn.style.backgroundColor = new Color(0.3f, 0.45f, 0.3f);
                searchRow.Add(addBtn);
            }
            
            // Results list
            var listView = new ListView();
            listView.name = "ResultsList";
            listView.style.maxHeight = 150;
            listView.style.minHeight = 60;
            listView.fixedItemHeight = 22;
            listView.selectionType = SelectionType.Single;
            
            listView.makeItem = () =>
            {
                var item = new Button { focusable = false };
                item.style.height = 20;
                item.style.marginLeft = 0;
                item.style.marginRight = 0;
                item.style.marginTop = 1;
                item.style.marginBottom = 1;
                item.style.paddingLeft = 6;
                item.style.fontSize = 11;
                item.style.unityTextAlign = TextAnchor.MiddleLeft;
                item.style.backgroundColor = StyleKeyword.Null;
                item.style.borderTopLeftRadius = 2;
                item.style.borderTopRightRadius = 2;
                item.style.borderBottomLeftRadius = 2;
                item.style.borderBottomRightRadius = 2;
                
                item.RegisterCallback<MouseEnterEvent>(_ => 
                {
                    if (item.enabledSelf)
                        item.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                });
                item.RegisterCallback<MouseLeaveEvent>(_ => 
                {
                    if (item.enabledSelf)
                        item.style.backgroundColor = StyleKeyword.Null;
                });
                
                return item;
            };
            
            listView.bindItem = (element, index) =>
            {
                var btn = element as Button;
                if (index >= state.Results.Count) return;
                
                var item = state.Results[index];
                var isCurrent = CompareTo(item, GetCurrentValue(prop));
                
                btn.text = GetStringValue(prop, item);
                btn.enabledSelf = !isCurrent;
                
                if (isCurrent)
                {
                    btn.style.backgroundColor = new Color(0.25f, 0.35f, 0.25f, 0.5f);
                    btn.style.color = Colors.AccentGreen;
                }
                else
                {
                    btn.style.backgroundColor = StyleKeyword.Null;
                    btn.style.color = Colors.LabelText;
                }
                
                // Remove old handler
                if (btn.userData is Action oldAction)
                    btn.clicked -= oldAction;
                
                // Add new handler
                Action newAction = () =>
                {
                    SelectItem(prop, item, valueBtn, state, dropdown);
                };
                btn.userData = newAction;
                btn.clicked += newAction;
            };
            
            dropdown.Add(listView);
            
            // Search filtering
            searchField.RegisterValueChangedCallback(evt =>
            {
                state.SearchText = evt.newValue;
                FilterResults(prop, state, listView);
            });
            
            // Close on focus out (delayed to allow click)
            searchField.RegisterCallback<FocusOutEvent>(evt =>
            {
                dropdown.schedule.Execute(() =>
                {
                    if (!IsChildFocused(dropdown)) CloseDropdown(state, dropdown);
                }).ExecuteLater(200);
            });
            
            return dropdown;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Dropdown Logic
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void ToggleDropdown(SerializedProperty prop, DrawerState state, VisualElement dropdown, Button valueBtn)
        {
            if (state.IsOpen)
            {
                CloseDropdown(state, dropdown);
            }
            else
            {
                OpenDropdown(prop, state, dropdown);
            }
        }
        
        private void OpenDropdown(SerializedProperty prop, DrawerState state, VisualElement dropdown)
        {
            state.IsOpen = true;
            state.AllEntries = ApplyValidation(prop, GetCurrentValue(prop), GetEntries());
            state.Results = state.AllEntries.ToList();
            state.SearchText = "";
            
            dropdown.style.display = DisplayStyle.Flex;
            
            var listView = dropdown.Q<ListView>("ResultsList");
            if (listView != null)
            {
                listView.itemsSource = state.Results;
                listView.Rebuild();
            }
            
            var searchField = dropdown.Q<TextField>();
            searchField?.Focus();
        }
        
        private void CloseDropdown(DrawerState state, VisualElement dropdown)
        {
            state.IsOpen = false;
            dropdown.style.display = DisplayStyle.None;
        }
        
        private void FilterResults(SerializedProperty prop, DrawerState state, ListView listView)
        {
            if (string.IsNullOrEmpty(state.SearchText))
            {
                state.Results = state.AllEntries.ToList();
            }
            else
            {
                var searchLower = state.SearchText.ToLowerInvariant();
                state.Results = state.AllEntries
                    .Where(e => GetStringValue(prop, e).ToLowerInvariant().Contains(searchLower))
                    .ToList();
            }
            
            listView.itemsSource = state.Results;
            listView.Rebuild();
        }
        
        private void SelectItem(SerializedProperty prop, T item, Button valueBtn, DrawerState state, VisualElement dropdown)
        {
            SetValue(prop, item);
            valueBtn.text = GetStringValue(prop, item);
            
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);

            if (dropdown is null) return;
            
            CloseDropdown(state, dropdown);
        }
        
        private bool IsChildFocused(VisualElement parent)
        {
            var focused = parent.panel?.focusController?.focusedElement as VisualElement;
            while (focused != null)
            {
                if (focused == parent) return true;
                focused = focused.parent;
            }
            return false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Legacy Support
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Attribute Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static readonly Dictionary<(Type host, string path, Type attr), object[]> _attrCache = new();

        protected IEnumerable<TAttr> GetFieldAttributes<TAttr>(SerializedProperty property) where TAttr : System.Attribute
        {
            if (property == null) return Array.Empty<TAttr>();
            
            if (fieldInfo != null)
                return fieldInfo.GetCustomAttributes(typeof(TAttr), inherit: true).Cast<TAttr>();

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

        private static FieldInfo ResolveFieldInfo(Type host, string propertyPath)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type curType = host;
            FieldInfo lastField = null;

            var parts = propertyPath.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];

                if (p == "Array")
                {
                    i++;
                    if (curType.IsArray)
                        curType = curType.GetElementType();
                    else if (curType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(curType))
                        curType = curType.GetGenericArguments()[0];
                    continue;
                }

                var fi = curType.GetField(p, flags);
                if (fi == null) return lastField;
                lastField = fi;
                curType = fi.FieldType;
            }

            return lastField;
        }
    }
}