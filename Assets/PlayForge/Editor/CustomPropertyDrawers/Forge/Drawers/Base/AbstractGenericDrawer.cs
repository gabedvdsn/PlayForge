using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Extended drawer for [SerializeReference] fields that shows inline child fields.
    /// Builds on AbstractTypeRefDrawer to add:
    /// - Expandable/collapsible child field display
    /// - Summary badges when collapsed
    /// - Accent color customization
    /// 
    /// Use this when you want users to edit the instance's fields inline.
    /// For simple type selection without field editing, use AbstractTypeRefDrawer directly.
    /// </summary>
    public abstract class AbstractGenericDrawer<T> : AbstractTypeRefDrawer<T> where T : class
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Static State
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static readonly Dictionary<string, bool> CollapseStates = new();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Instance State
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected VisualElement HeaderRow;
        protected Button CollapseButton;
        protected VisualElement SummaryContainer;
        protected VisualElement ChildFieldsContainer;
        protected SerializedProperty CurrentProperty;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Virtual Configuration
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Accent color for the child fields left border.</summary>
        protected virtual Color AccentColor => new Color(0.4f, 0.4f, 0.4f, 0.6f);
        
        /// <summary>Whether to show collapse/expand button.</summary>
        protected virtual bool EnableCollapse => true;
        
        /// <summary>Whether to show summary badges when collapsed.</summary>
        protected virtual bool EnableSummary => true;
        
        /// <summary>Maximum summary badges to display.</summary>
        protected virtual int MaxSummaryBadges => 3;
        
        /// <summary>Common suffixes to strip from type names.</summary>
        protected virtual string[] TypeNameSuffixes => new[] 
        { 
            "Worker", "Scaler", "Provider", "Handler", "Processor", "Factory" 
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // Main Entry Point
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            CurrentProperty = prop;
            
            var root = base.CreatePropertyGUI(prop);
            
            // Find header row and add extensions
            HeaderRow = FindHeaderRow(root);
            if (HeaderRow != null)
                SetupHeaderExtensions(prop);
            
            // Create and insert child fields container
            ChildFieldsContainer = CreateChildFieldsContainer();
            InsertAfterHeader(root, ChildFieldsContainer);
            
            // Initialize display
            InitializeDisplayState(prop);
            
            // Track changes
            root.TrackPropertyValue(prop, OnPropertyValueChanged);
            
            return root;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Extensions
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void SetupHeaderExtensions(SerializedProperty prop)
        {
            bool hasValue = HasValue(prop);
            bool isCollapsed = GetCollapseState(prop);
            
            if (EnableCollapse)
            {
                CollapseButton = CreateCollapseButton(isCollapsed);
                CollapseButton.clicked += () => ToggleCollapse(prop);
                CollapseButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
                HeaderRow.Insert(0, CollapseButton);
            }
            
            if (EnableSummary)
            {
                SummaryContainer = new VisualElement { name = "summary" };
                SummaryContainer.style.flexDirection = FlexDirection.Row;
                SummaryContainer.style.alignItems = Align.Center;
                SummaryContainer.style.marginLeft = 8;
                SummaryContainer.style.flexShrink = 1;
                SummaryContainer.style.overflow = Overflow.Hidden;
                SummaryContainer.style.display = DisplayStyle.None;
                HeaderRow.Add(SummaryContainer);
            }
        }
        
        private Button CreateCollapseButton(bool collapsed)
        {
            var btn = new Button
            {
                text = collapsed ? ForgeDrawerStyles.Icons.ChevronRight : ForgeDrawerStyles.Icons.ChevronDown,
                tooltip = collapsed ? "Expand" : "Collapse",
                focusable = false
            };
            
            btn.style.width = 16;
            btn.style.height = 16;
            btn.style.marginRight = 4;
            btn.style.fontSize = 8;
            btn.style.paddingLeft = btn.style.paddingRight = 0;
            btn.style.paddingTop = btn.style.paddingBottom = 0;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = ForgeDrawerStyles.Colors.ButtonBackground;
            
            btn.RegisterCallback<PointerEnterEvent>(_ => 
                btn.style.backgroundColor = ForgeDrawerStyles.Colors.ButtonHover);
            btn.RegisterCallback<PointerLeaveEvent>(_ => 
                btn.style.backgroundColor = ForgeDrawerStyles.Colors.ButtonBackground);
            
            return btn;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Child Fields
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateChildFieldsContainer()
        {
            var container = new VisualElement { name = "child-fields" };
            container.style.marginLeft = 8;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;
            container.style.paddingLeft = 8;
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = AccentColor;
            return container;
        }
        
        /// <summary>Override to customize which fields are shown.</summary>
        protected virtual void PopulateChildFields(SerializedProperty prop)
        {
            ChildFieldsContainer.Clear();
            if (!HasValue(prop)) return;

            var iterator = prop.Copy();
            var endProperty = prop.GetEndProperty();
            int count = 0;
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty)) break;
                    
                    var field = new PropertyField(iterator.Copy())
                    {
                        label = ObjectNames.NicifyVariableName(iterator.name)
                    };
                    field.style.marginBottom = 2;
                    ChildFieldsContainer.Add(field);
                    count++;
                    
                } while (iterator.NextVisible(false));
            }
            
            if (count == 0)
            {
                ChildFieldsContainer.style.display = DisplayStyle.None;
                if (CollapseButton != null) CollapseButton.style.display = DisplayStyle.None;
            }
            else
            {
                ChildFieldsContainer.Bind(prop.serializedObject);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Summary Badges
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Override to provide custom summary content.</summary>
        protected virtual void PopulateSummary(SerializedProperty prop)
        {
            SummaryContainer.Clear();
            if (!HasValue(prop)) return;

            var iterator = prop.Copy();
            var endProperty = prop.GetEndProperty();
            int count = 0;
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty)) break;
                    if (count >= MaxSummaryBadges)
                    {
                        AddBadge("…", ForgeDrawerStyles.Colors.HintText);
                        break;
                    }
                    
                    var text = GetSummaryText(iterator);
                    if (!string.IsNullOrEmpty(text))
                    {
                        AddBadge(text, GetSummaryColor(iterator));
                        count++;
                    }
                } while (iterator.NextVisible(false));
            }
        }
        
        /// <summary>Override to customize property summary text.</summary>
        protected virtual string GetSummaryText(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                SerializedPropertyType.Float => prop.floatValue.ToString("F1"),
                SerializedPropertyType.Boolean => prop.boolValue ? "✓" : "✗",
                SerializedPropertyType.String when !string.IsNullOrEmpty(prop.stringValue) => 
                    prop.stringValue.Length > 12 ? prop.stringValue[..12] + "…" : prop.stringValue,
                SerializedPropertyType.Enum => prop.enumDisplayNames[prop.enumValueIndex],
                SerializedPropertyType.ObjectReference when prop.objectReferenceValue != null => 
                    prop.objectReferenceValue.name,
                _ => null
            };
        }
        
        /// <summary>Override to customize property badge color.</summary>
        protected virtual Color GetSummaryColor(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Boolean => prop.boolValue 
                    ? ForgeDrawerStyles.Colors.AccentGreen 
                    : ForgeDrawerStyles.Colors.AccentRed,
                SerializedPropertyType.ObjectReference => ForgeDrawerStyles.Colors.AccentBlue,
                _ => ForgeDrawerStyles.Colors.HintText
            };
        }
        
        protected void AddBadge(string text, Color color)
        {
            var badge = new Label(text);
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = 3;
            badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
            badge.style.paddingLeft = badge.style.paddingRight = 4;
            badge.style.paddingTop = badge.style.paddingBottom = 1;
            badge.style.marginRight = 4;
            SummaryContainer.Add(badge);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Collapse Logic
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void ToggleCollapse(SerializedProperty prop)
        {
            bool newState = !GetCollapseState(prop);
            SetCollapseState(prop, newState);
            UpdateDisplayState(prop, newState);
        }
        
        private void UpdateDisplayState(SerializedProperty prop, bool collapsed)
        {
            // Update button
            if (CollapseButton != null)
            {
                CollapseButton.text = collapsed 
                    ? ForgeDrawerStyles.Icons.ChevronRight 
                    : ForgeDrawerStyles.Icons.ChevronDown;
                CollapseButton.tooltip = collapsed ? "Expand" : "Collapse";
            }
            
            if (collapsed)
            {
                ChildFieldsContainer.style.display = DisplayStyle.None;
                if (EnableSummary && SummaryContainer != null)
                {
                    SummaryContainer.style.display = DisplayStyle.Flex;
                    PopulateSummary(prop);
                }
            }
            else
            {
                ChildFieldsContainer.style.display = DisplayStyle.Flex;
                if (SummaryContainer != null) SummaryContainer.style.display = DisplayStyle.None;
                if (ChildFieldsContainer.childCount == 0) PopulateChildFields(prop);
            }
        }
        
        private void InitializeDisplayState(SerializedProperty prop)
        {
            bool hasValue = HasValue(prop);
            bool collapsed = GetCollapseState(prop);
            
            if (!hasValue || !collapsed || !EnableCollapse)
            {
                if (hasValue) PopulateChildFields(prop);
                else ChildFieldsContainer.style.display = DisplayStyle.None;
            }
            else
            {
                ChildFieldsContainer.style.display = DisplayStyle.None;
                if (EnableSummary && SummaryContainer != null)
                {
                    SummaryContainer.style.display = DisplayStyle.Flex;
                    PopulateSummary(prop);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Change Handling
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override void OnTypeChanged(SerializedProperty prop, Type newType)
        {
            SetCollapseState(prop, false);
            
            ChildFieldsContainer?.schedule.Execute(() =>
            {
                prop.serializedObject.Update();
                bool hasValue = newType != null;
                
                if (CollapseButton != null)
                {
                    CollapseButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
                    CollapseButton.text = ForgeDrawerStyles.Icons.ChevronDown;
                    CollapseButton.tooltip = "Collapse";
                }
                
                ChildFieldsContainer.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
                if (SummaryContainer != null) SummaryContainer.style.display = DisplayStyle.None;
                
                if (hasValue) PopulateChildFields(prop);
            });
        }
        
        private void OnPropertyValueChanged(SerializedProperty prop)
        {
            bool hasValue = HasValue(prop);
            bool collapsed = GetCollapseState(prop);
            
            if (CollapseButton != null)
                CollapseButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (!hasValue)
            {
                ChildFieldsContainer.Clear();
                ChildFieldsContainer.style.display = DisplayStyle.None;
                SummaryContainer?.Clear();
                if (SummaryContainer != null) SummaryContainer.style.display = DisplayStyle.None;
            }
            else if (collapsed && EnableCollapse && EnableSummary)
            {
                PopulateSummary(prop);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Type Name Override
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override string CleanTypeName(string name)
        {
            foreach (var suffix in TypeNameSuffixes)
            {
                if (name.EndsWith(suffix) && name.Length > suffix.Length)
                    return name[..^suffix.Length];
            }
            return name;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static bool GetCollapseState(SerializedProperty prop) =>
            CollapseStates.TryGetValue(prop.propertyPath, out bool c) && c;
        
        private static void SetCollapseState(SerializedProperty prop, bool collapsed) =>
            CollapseStates[prop.propertyPath] = collapsed;
        
        protected static bool HasValue(SerializedProperty prop) =>
            prop.propertyType == SerializedPropertyType.ManagedReference && 
            prop.managedReferenceValue != null;
        
        private static VisualElement FindHeaderRow(VisualElement root) =>
            root.childCount > 0 && root[0].style.flexDirection == FlexDirection.Row 
                ? root[0] : null;
        
        private static void InsertAfterHeader(VisualElement root, VisualElement el)
        {
            if (root.childCount >= 1) root.Insert(1, el);
            else root.Add(el);
        }
        
        /// <summary>Count of serializable child fields for the current value.</summary>
        protected int GetChildFieldCount(SerializedProperty prop)
        {
            if (!HasValue(prop)) return 0;
            
            int count = 0;
            var iterator = prop.Copy();
            var end = prop.GetEndProperty();
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, end)) break;
                    count++;
                } while (iterator.NextVisible(false));
            }
            return count;
        }
    }
}