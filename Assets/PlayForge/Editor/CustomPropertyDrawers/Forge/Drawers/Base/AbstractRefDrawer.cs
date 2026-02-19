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

        protected VisualElement Root;

        protected void Repaint()
        {
            Root.MarkDirtyRepaint();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Virtual Methods - Override in derived classes as needed
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected Button _openButton;
        protected Button _clearButton;
        
        /// <summary>Whether to show the "Open/Navigate" button. Override to control visibility.</summary>
        protected virtual bool AcceptOpen(SerializedProperty prop) => false;
        
        /// <summary>Whether to show the "Add/Create" button. Override to control visibility.</summary>
        protected virtual bool AcceptAdd() => false;

        protected virtual bool AcceptClear(SerializedProperty prop) => GetCurrentValue(prop) != null;

        /// <summary>Gets the default value when none is selected.</summary>
        /// <param name="prop"></param>
        protected virtual T GetDefault(SerializedProperty prop) => default;

        /// <summary>Called when the open button is clicked. Override to implement navigation.</summary>
        protected virtual void OnOpen(SerializedProperty prop, T value)
        {
            if (prop.objectReferenceValue is null) return;
            var record = value as Object;
            if (record is null) return;
            Selection.activeObject = record; 
            EditorGUIUtility.PingObject(record);
        }

        /// <summary>Called when the add button is clicked. Override to implement creation.</summary>
        protected virtual void OnAdd(SerializedProperty prop, string searchText)
        {
            UpdateButtonVisibility(prop);
        }

        protected virtual void OnClear(SerializedProperty prop, Button valueBtn, DrawerState state, VisualElement dropdown)
        {
            try
            {
                ClearReferenceValue(prop);
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
            
            SelectItem(prop, default, valueBtn, state, dropdown);
            UpdateButtonVisibility(prop);
            
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);
            Repaint();
        }

        protected abstract void ClearReferenceValue(SerializedProperty prop);
        
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
        
        protected class DrawerState
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
            Root = new VisualElement { name = "RefDrawerRoot" };
            var state = new DrawerState();
            Root.userData = state;
            
            // Main row
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            mainRow.style.minHeight = 20;
            Root.Add(mainRow);
            
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
            Root.Add(dropdown);
            
            if (AcceptClear(prop))
            {
                mainRow.Add(CreateClearButton(prop, valueBtn, state, dropdown));
            }
            
            // Open button (optional)
            if (AcceptOpen(prop))
            {
                mainRow.Add(CreateOpenButton(prop, valueBtn, state, dropdown));
            }
            
            // Wire up value button click
            valueBtn.clicked += () => ToggleDropdown(prop, state, dropdown, valueBtn);
            
            return Root;
        }

        protected virtual Button CreateOpenButton(SerializedProperty prop, Button valueBtn, DrawerState state, VisualElement dropdown)
        {
            _openButton = CreateIconButton(Icons.Arrow, "Open", () => OnOpen(prop, GetCurrentValue(prop)), Colors.AccentBlue);
            return _openButton;
        }
        
        protected virtual Button CreateClearButton(SerializedProperty prop, Button valueBtn, DrawerState state, VisualElement dropdown)
        {
            _clearButton = CreateIconButton(Icons.Clear, "Clear", () =>
            {
                OnClear(prop, valueBtn, state, dropdown);
            }, Colors.AccentRed);
            _clearButton.style.marginLeft = 4;
            return _clearButton;
        }
        
        protected void UpdateButtonVisibility(SerializedProperty prop)
        {
            bool hasValue = GetCurrentValue(prop) != null;
            
            if (_openButton != null)
                _openButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (_clearButton != null)
                _clearButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
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
            searchRow.Add(searchField);
            
            // Add button (optional)
            if (AcceptAdd())
            {
                var addBtn = CreateIconButton("+", "Create new", () =>
                {
                    OnAdd(prop, searchField.value);
                    CloseDropdown(state, dropdown);
                }, Colors.AccentGreen);
                addBtn.style.marginLeft = 4;
                searchRow.Add(addBtn);
            }
            
            // Results list
            var listView = new ListView();
            listView.name = "ResultsList";
            listView.style.maxHeight = 150;
            listView.style.minHeight = 50;
            listView.fixedItemHeight = 22;
            listView.selectionType = SelectionType.None;
            
            // makeItem - register handlers ONCE here
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
                
                // Disable default click
                item.clickable = null;
                
                // Handlers read from userData which is set in bindItem
                item.RegisterCallback<PointerEnterEvent>(evt =>
                {
                    if (item.userData is ItemData data)
                        item.style.backgroundColor = data.HoverColor;
                });
                
                item.RegisterCallback<PointerLeaveEvent>(evt =>
                {
                    if (item.userData is ItemData data)
                        item.style.backgroundColor = data.NormalColor;
                });
                
                item.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button == 0 && item.userData is ItemData data)
                    {
                        data.OnSelect();
                        evt.StopPropagation();
                    }
                });
                
                item.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 0) evt.StopPropagation();
                });
                
                return item;
            };
            
            // bindItem - update userData with current item data
            listView.bindItem = (element, index) =>
            {
                var btn = element as Button;
                if (btn == null || index >= state.Results.Count) return;
                
                var item = state.Results[index];
                btn.text = GetStringValue(prop, item);
                
                var currentValue = GetCurrentValue(prop);
                bool isSelected = CompareTo(item, currentValue);
                
                var normalColor = isSelected 
                    ? Colors.DrawerSelectedRow
                    : Colors.DrawerRow;
                var hoverColor = isSelected
                    ? Colors.DrawerSelectedRow
                    : Colors.DrawerHoveredRow;
                
                btn.style.backgroundColor = normalColor;
                btn.style.color = isSelected ? Colors.AccentGreen : Colors.LabelText;
                
                // Store data for event handlers
                var capturedItem = item;
                btn.userData = new ItemData
                {
                    NormalColor = normalColor,
                    HoverColor = hoverColor,
                    OnSelect = () => SelectItem(prop, capturedItem, valueBtn, state, dropdown)
                };
            };
            
            dropdown.Add(listView);
            
            // Search filtering - NO FocusOutEvent
            searchField.RegisterValueChangedCallback(evt =>
            {
                state.SearchText = evt.newValue;
                FilterResults(prop, state, listView);
            });
            
            // ESC to close
            searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    CloseDropdown(state, dropdown);
                    evt.StopPropagation();
                }
            });
            
            // Close when clicking outside
            Root.RegisterCallback<AttachToPanelEvent>(attachEvt =>
            {
                var panelRoot = Root.panel?.visualTree;
                if (panelRoot == null) return;
                
                panelRoot.RegisterCallback<PointerDownEvent>(pointerEvt =>
                {
                    if (!state.IsOpen) return;
                    
                    var clickPos = pointerEvt.position;
                    if (dropdown.worldBound.Contains(clickPos)) return;
                    if (valueBtn.worldBound.Contains(clickPos)) return;
                    
                    CloseDropdown(state, dropdown);
                }, TrickleDown.TrickleDown);
            });
            
            return dropdown;
        }

        // Add this helper class inside AbstractRefDrawer:
        private class ItemData
        {
            public Color NormalColor;
            public Color HoverColor;
            public Action OnSelect;
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