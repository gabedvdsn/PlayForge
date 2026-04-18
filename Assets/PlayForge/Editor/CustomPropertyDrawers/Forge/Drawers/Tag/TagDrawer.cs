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
    /// Hierarchical Tag picker drawer with context filtering, parent assignment, and creation support.
    /// Shows leaf name by default, full path on hover.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tag))]
    public class TagDrawer : PropertyDrawer
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Drawer State
        // ═══════════════════════════════════════════════════════════════════════════
        
        private class DrawerState
        {
            public bool IsOpen;
            public List<Tag> Results = new List<Tag>();
            public string[] ContextStrings = Array.Empty<string>();
            public bool AllowCreate = true;
            public bool IncludeUniversal = true;
            public bool IsReadOnly;
            public string CurrentFilter = "";
            
            // For fresh property access (fixes list item issues)
            public string PropertyPath;
            public UnityEngine.Object TargetObject;
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            var root = new VisualElement { name = "TagDrawerRoot" };
            var state = new DrawerState
            {
                PropertyPath = prop.propertyPath,
                TargetObject = prop.serializedObject.targetObject
            };
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
                label.style.marginRight = 0;
                label.style.marginLeft = 4;
                label.style.color = Colors.LabelText;
                
                var tooltipAttr = GetFieldAttribute<ForgeTagTooltip>(prop);
                if (tooltipAttr != null) label.tooltip = tooltipAttr.Tooltip;
                
                mainRow.Add(label);
            }
            
            // Value button - use fresh property lookup
            var valueBtn = CreateValueButton(state);
            mainRow.Add(valueBtn);
            
            // Read-only state
            if (state.IsReadOnly)
            {
                valueBtn.SetEnabled(false);
                valueBtn.style.opacity = 0.6f;
                return root;
            }
            
            // Dropdown
            var dropdown = CreateDropdown(root, state, valueBtn);
            root.Add(dropdown);
            
            valueBtn.clicked += () => ToggleDropdown(state, dropdown, valueBtn);
            
            return root;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Fresh Property Access - Fixes stale reference issues in lists
        // ═══════════════════════════════════════════════════════════════════════════
        
        private SerializedProperty GetFreshProperty(DrawerState state)
        {
            if (state.TargetObject == null) return null;
            var so = new SerializedObject(state.TargetObject);
            return so.FindProperty(state.PropertyPath);
        }
        
        private Tag GetCurrentTagFresh(DrawerState state)
        {
            var prop = GetFreshProperty(state);
            if (prop == null) return default;
            return GetCurrentTag(prop);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Value Button - Shows leaf name, full path on hover
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Button CreateValueButton(DrawerState state)
        {
            var tag = GetCurrentTagFresh(state);
            var contextColor = GetContextColor(state.ContextStrings);
            
            var displayText = GetDisplayText(tag);
            var fullPath = tag.Equals(default(Tag)) ? "" : tag.DisplayName;
            
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
            
            btn.tooltip = string.IsNullOrEmpty(fullPath) ? "No tag selected" : fullPath;
            
            // Hover: show display name, restore short name on leave
            btn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                btn.style.backgroundColor = Colors.ButtonHover;
                var currentTag = GetCurrentTagFresh(state);
                if (!ShowFullPath() && !currentTag.Equals(default(Tag)))
                {
                    btn.text = currentTag.DisplayName;
                }
            });
            
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = Colors.ButtonBackground;
                // Get fresh tag on leave
                var currentTag = GetCurrentTagFresh(state);
                btn.text = GetDisplayText(currentTag);
            });
            
            return btn;
        }
        
        private bool ShowFullPath()
        {
            return EditorPrefs.GetBool(TheForge.PREFS_PREFIX + "ShowFullTagPath", false);
        }
        
        private bool GroupByRoot()
        {
            return EditorPrefs.GetBool(TheForge.PREFS_PREFIX + "GroupTagsByRoot", true);
        }
        
        private string GetDisplayText(Tag tag)
        {
            if (tag.Equals(default(Tag))) return "<None>";

            return tag.DisplayName;

            if (ShowFullPath())
                return tag.DisplayName;

            // Use registered display name if available (strips the deterministic hash padding)
            var displayName = tag.DisplayName;
            if (tag.Depth > 1)
            {
                return $"…{displayName}";
            }
            return displayName;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Dropdown with Hierarchy Support
        // ═══════════════════════════════════════════════════════════════════════════
        
        private class TagItemData
        {
            public Tag Tag;
            public Color NormalColor;
            public Color HoverColor;
            public Action<Tag> OnSelect;
            public Action<Tag> OnSetAsParent;
            public int Depth;
            public bool IsCurrent;
        }
        
        private VisualElement CreateDropdown(VisualElement root, DrawerState state, Button valueBtn)
        {
            var dropdown = new VisualElement { name = "Dropdown" };
            dropdown.style.display = DisplayStyle.None;
            dropdown.style.marginTop = 2;
            dropdown.style.marginLeft = IsInListPath(state.PropertyPath) ? 0 : 120;
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
            dropdown.style.minWidth = 280;
            
            // Current tag display (just shows full path)
            var currentTagHeader = new VisualElement { name = "CurrentTagHeader" };
            currentTagHeader.style.marginBottom = 4;
            dropdown.Add(currentTagHeader);
            
            // Divider
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.BorderDark;
            divider.style.marginTop = 2;
            divider.style.marginBottom = 4;
            dropdown.Add(divider);
            
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
            
            if (state.AllowCreate)
            {
                var createBtn = CreateIconButton("+", "Create new tag (uses search text)", () =>
                {
                    CreateNewTag(state, searchField.value, valueBtn, dropdown);
                });
                createBtn.style.marginLeft = 4;
                createBtn.style.backgroundColor = new Color(0.3f, 0.45f, 0.3f);
                searchRow.Add(createBtn);
            }
            
            // Context hint
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
            listView.style.maxHeight = 200;
            listView.style.minHeight = 60;
            listView.fixedItemHeight = 22;
            listView.selectionType = SelectionType.Single;
            
            listView.makeItem = () => CreateTagListItem();
            
            listView.bindItem = (element, index) =>
            {
                BindTagListItem(element, index, state, valueBtn, dropdown);
            };
            
            dropdown.Add(listView);
            
            // Search filtering
            searchField.RegisterValueChangedCallback(evt =>
            {
                state.CurrentFilter = evt.newValue;
                FilterResults(state, evt.newValue, listView);
            });
            
            searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    CloseDropdown(state, dropdown);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Return && state.AllowCreate && !string.IsNullOrWhiteSpace(searchField.value))
                {
                    CreateNewTag(state, searchField.value, valueBtn, dropdown);
                    evt.StopPropagation();
                }
            });
            
            // Click outside to close
            root.RegisterCallback<AttachToPanelEvent>(attachEvt =>
            {
                var panelRoot = root.panel?.visualTree;
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
        
        /// <summary>
        /// Updates the header to show just the full tag path.
        /// </summary>
        private void UpdateCurrentTagHeader(VisualElement dropdown, DrawerState state)
        {
            var header = dropdown.Q<VisualElement>("CurrentTagHeader");
            if (header == null) return;
            
            header.Clear();
            
            var tag = GetCurrentTagFresh(state);
            
            if (tag.Equals(default(Tag)))
            {
                var noTagLabel = new Label("No tag selected");
                noTagLabel.style.fontSize = 11;
                noTagLabel.style.color = Colors.HintText;
                noTagLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                header.Add(noTagLabel);
                return;
            }
            
            // Show display name (clean, human-readable)
            var displayName = tag.DisplayName;
            var nameLabel = new Label(displayName);
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = Colors.AccentCyan;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.tooltip = tag.DisplayName; // Full hashed name on hover
            header.Add(nameLabel);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Tag List Item with Parent Assignment Button
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateTagListItem()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.height = 20;
            container.style.marginTop = 1;
            container.style.marginBottom = 1;
            container.style.borderTopLeftRadius = 2;
            container.style.borderTopRightRadius = 2;
            container.style.borderBottomLeftRadius = 2;
            container.style.borderBottomRightRadius = 2;
            
            // Main clickable area (tag name)
            var tagBtn = new Button { name = "TagButton", focusable = false };
            tagBtn.style.flexGrow = 1;
            tagBtn.style.height = 18;
            tagBtn.style.marginLeft = 0;
            tagBtn.style.marginRight = 0;
            tagBtn.style.paddingLeft = 6;
            tagBtn.style.paddingRight = 4;
            tagBtn.style.fontSize = 10;
            tagBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
            tagBtn.style.backgroundColor = StyleKeyword.Null;
            tagBtn.style.borderTopLeftRadius = 2;
            tagBtn.style.borderTopRightRadius = 0;
            tagBtn.style.borderBottomLeftRadius = 2;
            tagBtn.style.borderBottomRightRadius = 0;
            tagBtn.clickable = null;
            container.Add(tagBtn);
            
            // "Set as Parent" button (hidden by default, shown on hover)
            var parentBtn = new Button { name = "ParentButton", text = "▲", focusable = false };
            parentBtn.style.width = 20;
            parentBtn.style.height = 18;
            parentBtn.style.paddingLeft = 0;
            parentBtn.style.paddingRight = 0;
            parentBtn.style.paddingTop = 0;
            parentBtn.style.paddingBottom = 0;
            parentBtn.style.marginLeft = 2;
            parentBtn.style.marginRight = 2;
            parentBtn.style.fontSize = 8;
            parentBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            parentBtn.style.backgroundColor = Colors.AccentPurple.Fade(0.4f);
            parentBtn.style.borderTopLeftRadius = 0;
            parentBtn.style.borderTopRightRadius = 2;
            parentBtn.style.borderBottomLeftRadius = 0;
            parentBtn.style.borderBottomRightRadius = 2;
            parentBtn.style.display = DisplayStyle.None; // Hidden by default
            parentBtn.tooltip = "Set as parent of current tag";
            container.Add(parentBtn);
            
            // Hover behavior - show/hide parent button
            container.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (container.userData is TagItemData data)
                {
                    container.style.backgroundColor = data.HoverColor;
                    // Only show parent button if this isn't the current tag
                    if (!data.IsCurrent)
                    {
                        parentBtn.style.display = DisplayStyle.Flex;
                    }
                }
            });
            
            container.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (container.userData is TagItemData data)
                {
                    container.style.backgroundColor = data.NormalColor;
                    parentBtn.style.display = DisplayStyle.None;
                }
            });
            
            // Tag selection (click on tag name)
            tagBtn.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 0 && container.userData is TagItemData data)
                {
                    data.OnSelect?.Invoke(data.Tag);
                    evt.StopPropagation();
                }
            });
            
            tagBtn.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) evt.StopPropagation();
            });
            
            // Parent assignment (click on parent button)
            parentBtn.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 0 && container.userData is TagItemData data)
                {
                    data.OnSetAsParent?.Invoke(data.Tag);
                    evt.StopPropagation();
                }
            });
            
            parentBtn.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) evt.StopPropagation();
            });
            
            return container;
        }
        
        private void BindTagListItem(VisualElement element, int index, DrawerState state, Button valueBtn, VisualElement dropdown)
        {
            if (index >= state.Results.Count) return;

            var tag = state.Results[index];
            var currentTag = GetCurrentTagFresh(state);
            var isCurrent = tag.Equals(currentTag);
            
            var tagBtn = element.Q<Button>("TagButton");
            var parentBtn = element.Q<Button>("ParentButton");
            
            if (tagBtn == null) return;

            // Display with hierarchy indent
            var groupByRoot = GroupByRoot();
            var indent = groupByRoot ? (tag.Depth - 1) * 12 : 0;
            var cleanName = tag.DisplayName;
            var displayName = groupByRoot && tag.Depth > 1
                ? $"└ {cleanName}"
                : cleanName;
            
            tagBtn.text = displayName;
            tagBtn.style.paddingLeft = 6 + indent;
            tagBtn.tooltip = tag.DisplayName;

            var normalColor = isCurrent 
                ? new Color(0.25f, 0.35f, 0.25f, 0.5f) 
                : new Color(0, 0, 0, 0);
            var hoverColor = isCurrent
                ? new Color(0.3f, 0.45f, 0.3f, 0.5f)
                : new Color(0.3f, 0.3f, 0.3f, 0.5f);

            element.style.backgroundColor = normalColor;
            tagBtn.style.color = isCurrent ? Colors.AccentGreen : Colors.LabelText;
            
            // Style based on depth
            if (tag.Depth == 1 && groupByRoot)
            {
                tagBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                tagBtn.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
            
            // Reset parent button visibility
            if (parentBtn != null)
            {
                parentBtn.style.display = DisplayStyle.None;
                // Disable parent button for current tag (can't be parent of itself)
                parentBtn.SetEnabled(!isCurrent);
                
                // Update tooltip
                if (!currentTag.Equals(default(Tag)))
                {
                    parentBtn.tooltip = $"Set '{tag.DisplayName}' as parent of '{currentTag.GetLeafName()}'";
                }
            }

            element.userData = new TagItemData
            {
                Tag = tag,
                NormalColor = normalColor,
                HoverColor = hoverColor,
                OnSelect = t => SelectTag(state, t, valueBtn, dropdown),
                OnSetAsParent = t => SetTagAsParent(state, t, valueBtn, dropdown),
                Depth = tag.Depth,
                IsCurrent = isCurrent
            };
        }
        
        /// <summary>
        /// Sets the specified tag as the parent of the current tag.
        /// </summary>
        private void SetTagAsParent(DrawerState state, Tag parentTag, Button valueBtn, VisualElement dropdown)
        {
            var currentTag = GetCurrentTagFresh(state);
            if (currentTag.Equals(default(Tag))) return;
            
            // Get the leaf name of current tag
            var leafName = currentTag.GetLeafName();
            
            // Create new tag with parent
            var newPath = $"{parentTag.DisplayName}.{leafName}";
            var newTag = Tag.GenerateAsUnique(newPath);
            
            SelectTag(state, newTag, valueBtn, dropdown);
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
        
        private void ToggleDropdown(DrawerState state, VisualElement dropdown, Button valueBtn)
        {
            if (state.IsOpen)
                CloseDropdown(state, dropdown);
            else
                OpenDropdown(state, dropdown, valueBtn);
        }
        
        private void OpenDropdown(DrawerState state, VisualElement dropdown, Button valueBtn)
        {
            state.IsOpen = true;
            dropdown.style.display = DisplayStyle.Flex;
            
            // Update header with current tag
            UpdateCurrentTagHeader(dropdown, state);
            
            // Load tags hierarchically
            LoadTagsHierarchically(state);
            
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
        
        private void LoadTagsHierarchically(DrawerState state)
        {
            var allTags = ForgeTagRegistry.GetTagsForContext(state.ContextStrings, state.IncludeUniversal).ToList();
            
            if (GroupByRoot())
            {
                var sorted = new List<Tag>();
                var roots = allTags.Where(t => t.IsRoot).OrderBy(t => t.Name).ToList();
                
                foreach (var root in roots)
                {
                    sorted.Add(root);
                    var children = allTags
                        .Where(t => t.IsChildOf(root))
                        .OrderBy(t => t.Name)
                        .ToList();
                    sorted.AddRange(children);
                }
                
                var remaining = allTags.Except(sorted).OrderBy(t => t.Name);
                sorted.AddRange(remaining);
                
                state.Results = sorted;
            }
            else
            {
                state.Results = allTags.OrderBy(t => t.Name).ToList();
            }
        }
        
        private void FilterResults(DrawerState state, string filter, ListView listView)
        {
            var allTags = ForgeTagRegistry.GetTagsForContext(state.ContextStrings, state.IncludeUniversal);
            
            if (string.IsNullOrEmpty(filter))
            {
                LoadTagsHierarchically(state);
            }
            else
            {
                var filterLower = filter.ToLowerInvariant();
                state.Results = allTags
                    .Where(t => t.DisplayName.ToLowerInvariant().Contains(filterLower) ||
                                t.DisplayName.ToLowerInvariant().Contains(filterLower))
                    .OrderBy(t => t.DisplayName)
                    .ToList();
            }
            
            listView.itemsSource = state.Results;
            listView.Rebuild();
        }
        
        private void SelectTag(DrawerState state, Tag tag, Button valueBtn, VisualElement dropdown)
        {
            var prop = GetFreshProperty(state);
            if (prop == null) return;
            
            var oldTag = GetCurrentTag(prop);
            var asset = prop.serializedObject.targetObject as ScriptableObject;
            
            // Unregister old
            if (!oldTag.Equals(default(Tag)) && asset != null)
                ForgeTagRegistry.UnregisterTagUsage(oldTag, state.ContextStrings, asset);
            
            // Set new
            SetTag(prop, tag);
            
            // Apply changes
            prop.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prop.serializedObject.targetObject);
            
            // Update button with new display text
            UpdateValueButton(valueBtn, tag);
            
            // Register new
            if (!tag.Equals(default(Tag)) && asset != null) ForgeTagRegistry.RegisterTagUsage(tag, state.ContextStrings, asset);
            
            CloseDropdown(state, dropdown);
        }
        
        private void UpdateValueButton(Button btn, Tag tag)
        {
            var displayText = GetDisplayText(tag);
            var fullPath = tag.Equals(default(Tag)) ? "" : tag.DisplayName;
            
            btn.text = displayText;
            btn.tooltip = string.IsNullOrEmpty(fullPath) ? "No tag selected" : fullPath;
        }
        
        private void CreateNewTag(DrawerState state, string tagName, Button valueBtn, VisualElement dropdown)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                EditorUtility.DisplayDialog("Create Tag", "Please enter a tag name.", "OK");
                return;
            }
            
            tagName = tagName.Trim();
            
            if (!TagRegistry.IsValidPath(tagName))
            {
                EditorUtility.DisplayDialog("Create Tag", 
                    "Invalid tag path. Use letters, numbers, underscores.\nUse dots for hierarchy (e.g., 'Status.Debuff.Burn').", 
                    "OK");
                return;
            }

            var t = Tag.GenerateAsUnique(tagName);
            UnityEngine.Debug.Log($"Generating new tag {t.DisplayName} => {t.Name}");
            
            SelectTag(state, Tag.GenerateAsUnique(tagName), valueBtn, dropdown);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Tag Access
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Tag GetCurrentTag(SerializedProperty prop)
        {
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                return TagRegistry.Resolve(nameProp.stringValue);
            return default;
        }
        
        private void SetTag(SerializedProperty prop, Tag tag)
        {
            var nameProp = prop.FindPropertyRelative(nameof(Tag.Name));
            if (nameProp != null) nameProp.stringValue = tag.Equals(default(Tag)) ? "" : tag.Name;

            var displayNameProp = prop.FindPropertyRelative(nameof(Tag.DisplayName));
            if (displayNameProp is not null)
            {
                displayNameProp.stringValue = tag.Equals(default(Tag)) ? "" : tag.DisplayName;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private bool IsInList(SerializedProperty property) => property.propertyPath.Contains(".Array.data[");
        private bool IsInListPath(string path) => path.Contains(".Array.data[");
        
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