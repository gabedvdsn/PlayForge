using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(Tag))]
    public class TagDrawer : PropertyDrawer
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // State Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        private class DrawerState
        {
            public ListView list;
            public List<Tag> results = new List<Tag>();
            public bool isOpen;
            public ForgeTagContext contextAttribute;
            public string[] contextStrings;
            public bool allowCreate = true;
            public bool includeUniversal = true;
            public bool isReadOnly = false;
            
            // Flag to prevent dropdown close during add operation
            public bool isAddingTag = false;
            public double addClickTime = 0;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PropertyDrawer Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            var root = new VisualElement();
            var state = new DrawerState();
            root.userData = state;
            
            // Get attributes
            state.contextAttribute = GetFieldAttribute<ForgeTagContext>(prop);
            state.contextStrings = state.contextAttribute?.Context ?? Array.Empty<string>();
            state.allowCreate = state.contextAttribute?.AllowCreate ?? true;
            state.includeUniversal = state.contextAttribute?.IncludeUniversal ?? true;
            if (GetFieldAttribute<ForgeTagNoCreate>(prop) != null) state.allowCreate = false;
            if (GetFieldAttribute<ForgeTagExcludeUniversal>(prop) != null) state.includeUniversal = false;
            state.isReadOnly = GetFieldAttribute<ForgeTagReadOnly>(prop) != null;
            
            var tooltipAttr = GetFieldAttribute<ForgeTagTooltip>(prop);
            
            // Main container
            var container = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    alignSelf = Align.Stretch,
                    minHeight = 24,
                    maxHeight = 24
                }
            };
            
            // Label (if not in a list)
            var label = CreateLabel(prop);
            if (label != null)
            {
                if (tooltipAttr != null)
                    label.tooltip = tooltipAttr.Tooltip;
                container.Add(label);
            }
            
            // Value button
            var valueBtn = new Button
            {
                name = "ValueButton",
                text = GetDisplayString(prop),
                focusable = false,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 0,
                    marginLeft = 0,
                    marginRight = 0,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f),
                    minHeight = 22,
                    maxHeight = 22
                }
            };
            
            if (state.isReadOnly)
            {
                valueBtn.SetEnabled(false);
                valueBtn.style.opacity = 0.6f;
            }
            
            container.Add(valueBtn);
            
            // Clear button
            if (!state.isReadOnly)
            {
                var clearBtn = new Button
                {
                    text = "X",
                    focusable = false,
                    tooltip = "Clear",
                    style =
                    {
                        marginLeft = 4,
                        marginRight = 0,
                        paddingTop = 4,
                        paddingBottom = 6,
                        paddingLeft = 4,
                        paddingRight = 4
                    }
                };
                clearBtn.clicked += () => OnSelectItem(prop, default, valueBtn, null, null, state);
                container.Add(clearBtn);
            }
            
            root.Add(container);
            
            // Dropdown container
            if (!state.isReadOnly)
            {
                var dropdown = CreateDropdown(prop, valueBtn, state);
                root.Add(dropdown);
                
                valueBtn.clicked += () => ToggleDropdown(prop, valueBtn, dropdown, state);
            }
            
            return root;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // UI Creation
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Label CreateLabel(SerializedProperty prop)
        {
            if (IsInList(prop)) return null;
            
            var label = new Label(ObjectNames.NicifyVariableName(prop.name));
            label.style.marginRight = 4;
            label.style.marginLeft = 4;
            label.style.minWidth = 90;
            
            return label;
        }
        
        private VisualElement CreateDropdown(SerializedProperty prop, Button valueBtn, DrawerState state)
        {
            var dropdown = new VisualElement
            {
                style =
                {
                    flexShrink = 0,
                    flexDirection = FlexDirection.Row,
                    maxHeight = 200,
                    marginLeft = 4,
                    marginRight = 4,
                    display = DisplayStyle.None
                }
            };
            
            // Side bar indicator with context color
            var bar = new VisualElement
            {
                style =
                {
                    flexGrow = 0,
                    flexShrink = 1,
                    minWidth = 3,
                    maxWidth = 3,
                    marginLeft = 3,
                    backgroundColor = GetContextColor(state.contextStrings)
                }
            };
            dropdown.Add(bar);
            
            // Content container
            var content = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    maxHeight = 200,
                    marginRight = 4,
                    marginLeft = 4
                }
            };
            
            // Search row
            var searchRow = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Stretch,
                    alignSelf = Align.Stretch,
                    minHeight = 24,
                    maxHeight = 24,
                    marginBottom = 2
                }
            };
            
            var searchField = new TextField
            {
                value = "",
                style =
                {
                    flexGrow = 1,
                    flexShrink = 0,
                    marginLeft = 0,
                    maxHeight = 22,
                    minHeight = 22,
                    marginRight = 0,
                    fontSize = 12,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            searchRow.Add(searchField);
            
            // Add button (create new tag)
            if (state.allowCreate)
            {
                var addBtn = new Button
                {
                    text = "+",
                    focusable = false,
                    tooltip = "Create New Tag",
                    style =
                    {
                        marginLeft = 4,
                        marginRight = -2,
                        paddingTop = 4,
                        paddingBottom = 6,
                        paddingLeft = 4,
                        paddingRight = 4,
                        minHeight = 22,
                        maxHeight = 22
                    }
                };
                
                // Use MouseDownEvent to set the flag BEFORE focus changes
                addBtn.RegisterCallback<MouseDownEvent>(evt =>
                {
                    evt.StopPropagation();
                    state.isAddingTag = true;
                    state.addClickTime = EditorApplication.timeSinceStartup;
                }, TrickleDown.TrickleDown);
                
                // Use MouseUpEvent to actually create the tag
                addBtn.RegisterCallback<MouseUpEvent>(evt =>
                {
                    evt.StopPropagation();
                    CreateNewTag(prop, searchField.value, valueBtn, dropdown, searchField, state);
                    state.isAddingTag = false;
                });
                
                searchRow.Add(addBtn);
            }
            
            content.Add(searchRow);
            
            // Context hint
            if (state.contextStrings.Length > 0)
            {
                var contextHint = new Label($"Context: {GetContextDisplayString(state.contextStrings)}");
                contextHint.style.fontSize = 9;
                contextHint.style.color = new Color(0.6f, 0.6f, 0.6f);
                contextHint.style.marginBottom = 2;
                content.Add(contextHint);
            }
            
            // List view
            state.list = new ListView
            {
                name = "TagList",
                fixedItemHeight = 22,
                style =
                {
                    flexGrow = 1,
                    maxHeight = 150
                }
            };
            content.Add(state.list);
            
            dropdown.Add(content);
            
            // Search filter
            searchField.RegisterValueChangedCallback(evt =>
            {
                RefreshList(prop, evt.newValue, state);
            });
            
            // Focus handling - check if we're in the middle of adding a tag
            searchField.RegisterCallback<FocusOutEvent>(evt =>
            {
                // Don't close if we're adding a tag
                if (state.isAddingTag)
                    return;
                
                // Check if recently clicked add button (within 200ms)
                if (EditorApplication.timeSinceStartup - state.addClickTime < 0.2)
                    return;
                
                if (evt.relatedTarget is VisualElement related && IsChildOf(related, dropdown))
                    return;
                
                EditorApplication.delayCall += () =>
                {
                    // Double-check the flag again in delayed call
                    if (state.isAddingTag) return;
                    if (!state.isOpen) return;
                    HideDropdown(dropdown, state);
                };
            });
            
            return dropdown;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Dropdown Logic
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void ToggleDropdown(SerializedProperty prop, Button valueBtn, VisualElement dropdown, DrawerState state)
        {
            if (state.isOpen)
            {
                HideDropdown(dropdown, state);
            }
            else
            {
                ShowDropdown(prop, valueBtn, dropdown, state);
            }
        }
        
        private void ShowDropdown(SerializedProperty prop, Button valueBtn, VisualElement dropdown, DrawerState state)
        {
            state.isOpen = true;
            state.isAddingTag = false;
            dropdown.style.display = DisplayStyle.Flex;
            
            // Get available tags from registry with context filtering
            var availableTags = TagRegistry.GetTagsForContext(state.contextStrings, state.includeUniversal).ToList();
            
            // Sort by name
            availableTags = availableTags.OrderBy(t => t.ToString()).ToList();
            
            state.results = availableTags;
            
            // Setup list view
            state.list.itemsSource = state.results;
            
            state.list.makeItem = () => new Button
            {
                focusable = false,
                style =
                {
                    flexGrow = 1,
                    marginLeft = 0,
                    marginRight = 4,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    minHeight = 20,
                    maxHeight = 20
                }
            };
            
            state.list.bindItem = (element, index) =>
            {
                var tag = state.results[index];
                var button = element as Button;
                
                var currentTag = GetCurrentTag(prop);
                bool isSelected = tag.Equals(currentTag);
                
                button.style.backgroundColor = isSelected 
                    ? new StyleColor(new Color(0.25f, 0.25f, 0.25f, 1f)) 
                    : StyleKeyword.Null;
                button.enabledSelf = !isSelected;
                button.text = tag.ToString();
                button.tooltip = GetTagTooltip(tag);
                
                // Remove old click handler
                if (button.userData is Action oldAction)
                    button.clicked -= oldAction;
                
                // Add new click handler
                Action clickAction = () => OnSelectItem(prop, tag, valueBtn, dropdown, null, state);
                button.userData = clickAction;
                button.clicked += clickAction;
            };
            
            state.list.Rebuild();
            
            // Focus search field
            var searchField = dropdown.Q<TextField>();
            searchField?.Focus();
        }
        
        private void HideDropdown(VisualElement dropdown, DrawerState state)
        {
            state.isOpen = false;
            state.isAddingTag = false;
            dropdown.style.display = DisplayStyle.None;
        }
        
        private void RefreshList(SerializedProperty prop, string filter, DrawerState state)
        {
            var allTags = TagRegistry.GetTagsForContext(state.contextStrings, state.includeUniversal).ToList();
            
            if (!string.IsNullOrEmpty(filter))
            {
                state.results = allTags
                    .Where(t => t.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(t => t.ToString())
                    .ToList();
            }
            else
            {
                state.results = allTags.OrderBy(t => t.ToString()).ToList();
            }
            
            state.list.itemsSource = state.results;
            state.list.Rebuild();
        }
        
        private void OnSelectItem(SerializedProperty prop, Tag tag, Button valueBtn, VisualElement dropdown, TextField searchField, DrawerState state)
        {
            // Get old tag before changing
            var oldTag = GetCurrentTag(prop);
            var asset = prop.serializedObject.targetObject as ScriptableObject;
            
            // Unregister old tag if it exists
            if (!oldTag.Equals(default(Tag)) && asset != null)
            {
                TagRegistry.UnregisterTagUsage(oldTag, state.contextStrings, asset);
            }
            
            // Set new tag
            SetTag(prop, tag);
            valueBtn.text = GetDisplayString(prop);
            
            if (searchField != null)
                searchField.value = tag.ToString();
            
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);
            
            // Register new tag usage in TagRegistry
            if (!tag.Equals(default(Tag)) && asset != null)
            {
                TagRegistry.RegisterTagUsage(tag, state.contextStrings, asset);
            }
            
            if (dropdown != null)
                HideDropdown(dropdown, state);
        }
        
        private void CreateNewTag(SerializedProperty prop, string tagName, Button valueBtn, VisualElement dropdown, TextField searchField, DrawerState state)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                EditorUtility.DisplayDialog("Create Tag", "Please enter a tag name.", "OK");
                state.isAddingTag = false;
                return;
            }
            
            tagName = tagName.Trim();
            
            // Get old tag before changing
            var oldTag = GetCurrentTag(prop);
            var asset = prop.serializedObject.targetObject as ScriptableObject;
            
            // Unregister old tag if it exists
            if (!oldTag.Equals(default(Tag)) && asset != null)
            {
                TagRegistry.UnregisterTagUsage(oldTag, state.contextStrings, asset);
            }
            
            var newTag = Tag.Generate(tagName);
            
            // Set the tag value
            SetTag(prop, newTag);
            valueBtn.text = GetDisplayString(prop);
            
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);
            
            // Register usage in TagRegistry
            if (asset != null)
            {
                TagRegistry.RegisterTagUsage(newTag, state.contextStrings, asset);
            }
            
            // Clear the search field
            if (searchField != null)
                searchField.value = "";
            
            // Refresh the list to show the new tag
            RefreshList(prop, "", state);
            
            // Reset adding flag and close dropdown
            state.isAddingTag = false;
            HideDropdown(dropdown, state);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Tag Property Access
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Tag GetCurrentTag(SerializedProperty prop)
        {
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
            {
                return Tag.Generate(nameProp.stringValue);
            }
            return default;
        }
        
        private void SetTag(SerializedProperty prop, Tag tag)
        {
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp != null)
            {
                nameProp.stringValue = tag.Equals(default(Tag)) ? "" : tag.ToString();
            }
        }
        
        private string GetDisplayString(SerializedProperty prop)
        {
            var tag = GetCurrentTag(prop);
            return tag.Equals(default(Tag)) ? "<None>" : tag.ToString();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private bool IsInList(SerializedProperty property)
        {
            return property.propertyPath.Contains(".Array.data[");
        }
        
        private bool IsChildOf(VisualElement element, VisualElement parent)
        {
            while (element != null)
            {
                if (element == parent) return true;
                element = element.parent;
            }
            return false;
        }
        
        private Color GetContextColor(string[] contextStrings)
        {
            if (contextStrings == null || contextStrings.Length == 0)
                return new Color(0.3f, 0.3f, 0.3f);
            
            var first = contextStrings[0];
            
            if (first.Contains("Ability")) return new Color(1f, 0.6f, 0.2f);
            if (first.Contains("Effect")) return new Color(1f, 0.3f, 0.3f);
            if (first.Contains("Entity")) return new Color(0.7f, 0.4f, 1f);
            if (first.Contains("Attribute")) return new Color(0.3f, 0.6f, 1f);
            if (first.Contains("Visibility")) return new Color(0.3f, 0.8f, 0.8f);
            if (first.Contains("Granted")) return new Color(0.4f, 0.9f, 0.4f);
            if (first.Contains("Required")) return new Color(1f, 0.8f, 0.2f);
            if (first.Contains("Blocked")) return new Color(1f, 0.4f, 0.4f);
            
            return new Color(0.5f, 0.5f, 0.5f);
        }
        
        private string GetContextDisplayString(string[] contextStrings)
        {
            if (contextStrings == null || contextStrings.Length == 0)
                return "Any";
            
            return string.Join(", ", contextStrings);
        }
        
        private string GetTagTooltip(Tag tag)
        {
            var usage = TagRegistry.GetTagUsage(tag);
            if (usage == null) return tag.ToString();
            
            var contexts = usage.UsageByContext.Values.Take(3)
                .Select(c => $"{c.Context.FriendlyName} ({c.Assets.Count})")
                .ToList();
            
            var tooltip = $"{tag}\nUsed {usage.TotalUsageCount} time(s)";
            if (contexts.Count > 0)
                tooltip += $"\nContexts: {string.Join(", ", contexts)}";
            
            return tooltip;
        }
        
        private T GetFieldAttribute<T>(SerializedProperty property) where T : System.Attribute
        {
            if (fieldInfo != null)
                return fieldInfo.GetCustomAttribute<T>();
            
            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0) return null;
            
            var hostType = targets[0].GetType();
            var fi = ResolveFieldInfo(hostType, property.propertyPath);
            
            return fi?.GetCustomAttribute<T>();
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
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}