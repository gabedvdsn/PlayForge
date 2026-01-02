using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Minimalist Tag picker drawer with context filtering and creation support.
    /// Note: Cannot derive from AbstractRefDrawer because Tag is a struct with special
    /// TagRegistry integration and context-based filtering via attributes.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tag))]
    public class TagDrawer : PropertyDrawer
    {
        private class DrawerState
        {
            public bool IsOpen;
            public List<Tag> Results = new List<Tag>();
            public string[] ContextStrings = Array.Empty<string>();
            public bool AllowCreate = true;
            public bool IncludeUniversal = true;
            public bool IsReadOnly;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            var root = new VisualElement { name = "TagDrawerRoot" };
            var state = new DrawerState();
            root.userData = state;
            
            // Get attributes
            var contextAttr = GetFieldAttribute<ForgeTagContext>(prop);
            state.ContextStrings = contextAttr?.Context ?? Array.Empty<string>();
            state.AllowCreate = contextAttr?.AllowCreate ?? true;
            state.IncludeUniversal = contextAttr?.IncludeUniversal ?? true;
            if (GetFieldAttribute<ForgeTagNoCreate>(prop) != null) state.AllowCreate = false;
            if (GetFieldAttribute<ForgeTagExcludeUniversal>(prop) != null) state.IncludeUniversal = false;
            state.IsReadOnly = GetFieldAttribute<ForgeTagReadOnly>(prop) != null;
            
            // Main row
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            mainRow.style.minHeight = 20;
            root.Add(mainRow);
            
            // Label (if not in list)
            if (!IsInList(prop))
            {
                var label = new Label(ObjectNames.NicifyVariableName(prop.name));
                label.style.minWidth = 120;
                label.style.marginRight = 4;
                label.style.color = Colors.LabelText;
                
                var tooltipAttr = GetFieldAttribute<ForgeTagTooltip>(prop);
                if (tooltipAttr != null) label.tooltip = tooltipAttr.Tooltip;
                
                mainRow.Add(label);
            }
            
            // Value button
            var valueBtn = CreateValueButton(prop, state);
            mainRow.Add(valueBtn);
            
            // Read-only state
            if (state.IsReadOnly)
            {
                valueBtn.SetEnabled(false);
                valueBtn.style.opacity = 0.6f;
                return root;
            }
            
            // Dropdown
            var dropdown = CreateDropdown(prop, state, valueBtn);
            root.Add(dropdown);
            
            valueBtn.clicked += () => ToggleDropdown(prop, state, dropdown, valueBtn);
            
            return root;
        }
        
        private Button CreateValueButton(SerializedProperty prop, DrawerState state)
        {
            var tag = GetCurrentTag(prop);
            var displayText = tag.Equals(default(Tag)) ? "<None>" : tag.ToString();
            var contextColor = GetContextColor(state.ContextStrings);
            
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
            btn.style.borderLeftWidth = 2;
            btn.style.borderLeftColor = contextColor;
            
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
            
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
            dropdown.style.borderLeftColor = GetContextColor(state.ContextStrings);
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
            
            // Create button
            if (state.AllowCreate)
            {
                var createBtn = CreateIconButton("+", "Create new tag", () =>
                {
                    CreateNewTag(prop, searchField.value, valueBtn, state, dropdown);
                });
                createBtn.style.marginLeft = 4;
                createBtn.style.backgroundColor = new Color(0.3f, 0.45f, 0.3f);
                searchRow.Add(createBtn);
            }
            
            // Context hint (compact)
            if (state.ContextStrings.Length > 0)
            {
                var contextHint = new Label(string.Join(" · ", state.ContextStrings));
                contextHint.style.fontSize = 9;
                contextHint.style.color = Colors.HintText;
                contextHint.style.marginBottom = 4;
                dropdown.Add(contextHint);
            }
            
            // Results list
            var listView = new ListView();
            listView.name = "ResultsList";
            listView.style.maxHeight = 140;
            listView.style.minHeight = 50;
            listView.fixedItemHeight = 20;
            listView.selectionType = SelectionType.Single;
            
            listView.makeItem = () =>
            {
                var item = new Button { focusable = false };
                item.style.height = 18;
                item.style.marginLeft = 0;
                item.style.marginRight = 0;
                item.style.marginTop = 1;
                item.style.marginBottom = 1;
                item.style.paddingLeft = 6;
                item.style.fontSize = 10;
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
                
                var tag = state.Results[index];
                var currentTag = GetCurrentTag(prop);
                var isCurrent = tag.Equals(currentTag);
                
                btn.text = tag.ToString();
                btn.enabledSelf = !isCurrent;
                btn.tooltip = GetTagTooltip(tag);
                
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
                
                if (btn.userData is Action oldAction)
                    btn.clicked -= oldAction;
                
                Action newAction = () => SelectTag(prop, tag, valueBtn, state, dropdown);
                btn.userData = newAction;
                btn.clicked += newAction;
            };
            
            dropdown.Add(listView);
            
            // Search filtering
            searchField.RegisterValueChangedCallback(evt => FilterResults(state, evt.newValue, listView));
            
            // Focus management
            searchField.RegisterCallback<FocusOutEvent>(evt =>
            {
                dropdown.schedule.Execute(() =>
                {
                    if (!IsChildFocused(dropdown))
                        CloseDropdown(state, dropdown);
                }).ExecuteLater(100);
            });
            
            return dropdown;
        }
        
        private Button CreateIconButton(string icon, string tooltip, Action onClick)
        {
            var btn = new Button { text = icon, tooltip = tooltip, focusable = false };
            btn.style.width = 20;
            btn.style.height = 20;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.fontSize = 12;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
            
            btn.clicked += onClick;
            return btn;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Dropdown Logic
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void ToggleDropdown(SerializedProperty prop, DrawerState state, VisualElement dropdown, Button valueBtn)
        {
            if (state.IsOpen)
                CloseDropdown(state, dropdown);
            else
                OpenDropdown(prop, state, dropdown);
        }
        
        private void OpenDropdown(SerializedProperty prop, DrawerState state, VisualElement dropdown)
        {
            state.IsOpen = true;
            dropdown.style.display = DisplayStyle.Flex;
            
            // Load tags from registry
            state.Results = TagRegistry.GetTagsForContext(state.ContextStrings, state.IncludeUniversal)
                .OrderBy(t => t.ToString())
                .ToList();
            
            var listView = dropdown.Q<ListView>("ResultsList");
            if (listView != null)
            {
                listView.itemsSource = state.Results;
                listView.Rebuild();
            }
            
            dropdown.Q<TextField>()?.Focus();
        }
        
        private void CloseDropdown(DrawerState state, VisualElement dropdown)
        {
            state.IsOpen = false;
            dropdown.style.display = DisplayStyle.None;
        }
        
        private void FilterResults(DrawerState state, string filter, ListView listView)
        {
            var allTags = TagRegistry.GetTagsForContext(state.ContextStrings, state.IncludeUniversal);
            
            if (string.IsNullOrEmpty(filter))
            {
                state.Results = allTags.OrderBy(t => t.ToString()).ToList();
            }
            else
            {
                var filterLower = filter.ToLowerInvariant();
                state.Results = allTags
                    .Where(t => t.ToString().ToLowerInvariant().Contains(filterLower))
                    .OrderBy(t => t.ToString())
                    .ToList();
            }
            
            listView.itemsSource = state.Results;
            listView.Rebuild();
        }
        
        private void SelectTag(SerializedProperty prop, Tag tag, Button valueBtn, DrawerState state, VisualElement dropdown)
        {
            var oldTag = GetCurrentTag(prop);
            var asset = prop.serializedObject.targetObject as ScriptableObject;
            
            // Unregister old
            if (!oldTag.Equals(default(Tag)) && asset != null)
                TagRegistry.UnregisterTagUsage(oldTag, state.ContextStrings, asset);
            
            // Set new
            SetTag(prop, tag);
            valueBtn.text = tag.Equals(default(Tag)) ? "<None>" : tag.ToString();
            
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);
            
            // Register new
            if (!tag.Equals(default(Tag)) && asset != null)
                TagRegistry.RegisterTagUsage(tag, state.ContextStrings, asset);
            
            CloseDropdown(state, dropdown);
        }
        
        private void CreateNewTag(SerializedProperty prop, string tagName, Button valueBtn, DrawerState state, VisualElement dropdown)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                EditorUtility.DisplayDialog("Create Tag", "Please enter a tag name.", "OK");
                return;
            }
            
            tagName = tagName.Trim();
            var newTag = Tag.Generate(tagName);
            
            SelectTag(prop, newTag, valueBtn, state, dropdown);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Tag Access
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Tag GetCurrentTag(SerializedProperty prop)
        {
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                return Tag.Generate(nameProp.stringValue);
            return default;
        }
        
        private void SetTag(SerializedProperty prop, Tag tag)
        {
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp != null)
                nameProp.stringValue = tag.Equals(default(Tag)) ? "" : tag.ToString();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private bool IsInList(SerializedProperty property) => property.propertyPath.Contains(".Array.data[");
        
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
        
        private Color GetContextColor(string[] contextStrings)
        {
            if (contextStrings == null || contextStrings.Length == 0)
                return Colors.HintText;
            
            var first = contextStrings[0];
            if (first.Contains("Ability")) return Colors.AccentOrange;
            if (first.Contains("Effect")) return Colors.AccentRed;
            if (first.Contains("Entity")) return Colors.AccentPurple;
            if (first.Contains("Attribute")) return Colors.AccentBlue;
            if (first.Contains("Granted")) return Colors.AccentGreen;
            if (first.Contains("Required")) return Colors.AccentYellow;
            if (first.Contains("Blocked")) return Colors.AccentRed;
            
            return Colors.AccentGray;
        }
        
        private string GetTagTooltip(Tag tag)
        {
            var usage = TagRegistry.GetTagUsage(tag);
            if (usage == null) return tag.ToString();
            return $"{tag} • Used {usage.TotalUsageCount}×";
        }
        
        private T GetFieldAttribute<T>(SerializedProperty property) where T : System.Attribute
        {
            if (fieldInfo != null)
                return fieldInfo.GetCustomAttribute<T>();
            
            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0) return null;
            
            var fi = ResolveFieldInfo(targets[0].GetType(), property.propertyPath);
            return fi?.GetCustomAttribute<T>();
        }
        
        private static FieldInfo ResolveFieldInfo(Type host, string propertyPath)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type curType = host;
            FieldInfo lastField = null;
            
            foreach (var p in propertyPath.Split('.'))
            {
                if (p == "Array") continue;
                if (p.StartsWith("data[")) continue;
                
                var fi = curType.GetField(p, flags);
                if (fi == null) return lastField;
                lastField = fi;
                curType = fi.FieldType;
                
                if (curType.IsArray) curType = curType.GetElementType();
                else if (curType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(curType))
                    curType = curType.GetGenericArguments()[0];
            }
            
            return lastField;
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}