using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Generic drawer for SerializeReference-based worker types.
    /// Shows type selector dropdown and inline child fields with collapse support.
    /// </summary>
    public class AbstractGenericDrawer<T> : AbstractTypeRefDrawer<T> where T : class
    {
        // Static collapse state tracking
        private static readonly Dictionary<string, bool> _collapsedStates = new();
        
        protected VisualElement _childFieldsContainer;
        protected VisualElement _summaryContainer;
        protected SerializedProperty _currentProperty;
        protected Button _collapseButton;
        protected VisualElement _root;
        protected VisualElement _headerRow;
        
        // Configurable accent color for child fields border
        protected virtual Color AccentColor => new Color(0.4f, 0.4f, 0.4f, 0.6f);
        
        // Whether to show collapse functionality (can be overridden)
        protected virtual bool EnableCollapse => true;
        
        // Whether to show summary when collapsed (can be overridden)
        protected virtual bool EnableSummary => true;
        
        private static bool IsCollapsed(string propertyPath) => 
            _collapsedStates.TryGetValue(propertyPath, out bool c) && c;
        
        private static void SetCollapsed(string propertyPath, bool collapsed) => 
            _collapsedStates[propertyPath] = collapsed;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            _currentProperty = prop;
            
            // Get the base GUI (type selector + dropdown)
            var baseGui = base.CreatePropertyGUI(prop);
            _root = baseGui;
            
            bool hasValue = prop.propertyType == SerializedPropertyType.ManagedReference && 
                           prop.managedReferenceValue != null;
            bool isCollapsed = IsCollapsed(prop.propertyPath);
            
            // Find the header row (first child) and add collapse button + summary
            if (baseGui.childCount >= 1)
            {
                var headerRow = baseGui[0];
                if (headerRow is VisualElement row && row.style.flexDirection == FlexDirection.Row)
                {
                    _headerRow = row;
                    
                    if (EnableCollapse && hasValue)
                    {
                        InsertCollapseButton(row, prop, isCollapsed);
                    }
                    
                    if (EnableSummary)
                    {
                        InsertSummaryContainer(row);
                    }
                }
            }
            
            // Create container for child fields
            _childFieldsContainer = new VisualElement
            {
                name = "worker-fields",
                style =
                {
                    marginLeft = 8,
                    marginTop = 4,
                    marginBottom = 4,
                    paddingLeft = 8,
                    borderLeftWidth = 2,
                    borderLeftColor = AccentColor
                }
            };
            
            // Insert child fields container after the first child (the type selector row)
            if (baseGui.childCount >= 1)
            {
                baseGui.Insert(1, _childFieldsContainer);
            }
            else
            {
                baseGui.Add(_childFieldsContainer);
            }
            
            // Initial population of child fields (respecting collapsed state)
            if (!isCollapsed || !EnableCollapse)
            {
                PopulateChildFields(prop);
                UpdateSummaryVisibility(false);
            }
            else
            {
                _childFieldsContainer.style.display = DisplayStyle.None;
                UpdateSummaryVisibility(true);
                RefreshSummary(prop);
            }
            
            // Track property changes to refresh child fields when type changes
            baseGui.TrackPropertyValue(prop, OnPropertyChanged);
            
            return baseGui;
        }
        
        private void InsertCollapseButton(VisualElement headerRow, SerializedProperty prop, bool isCollapsed)
        {
            _collapseButton = new Button
            {
                text = isCollapsed ? ForgeDrawerStyles.Icons.ChevronRight : ForgeDrawerStyles.Icons.ChevronDown,
                tooltip = isCollapsed ? "Expand" : "Collapse",
                style =
                {
                    width = 16,
                    height = 16,
                    marginRight = 4,
                    fontSize = 8,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    backgroundColor = ForgeDrawerStyles.Colors.ButtonBackground
                }
            };
            
            _collapseButton.RegisterCallback<MouseEnterEvent>(_ => 
                _collapseButton.style.backgroundColor = ForgeDrawerStyles.Colors.ButtonHover);
            _collapseButton.RegisterCallback<MouseLeaveEvent>(_ => 
                _collapseButton.style.backgroundColor = ForgeDrawerStyles.Colors.ButtonBackground);
            
            _collapseButton.clicked += () => ToggleCollapse(prop);
            
            // Insert at the beginning of the header row
            headerRow.Insert(0, _collapseButton);
        }
        
        private void InsertSummaryContainer(VisualElement headerRow)
        {
            _summaryContainer = new VisualElement
            {
                name = "summary-container",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 8,
                    flexShrink = 1,
                    overflow = Overflow.Hidden
                }
            };
            
            // Add at the end of the header row
            headerRow.Add(_summaryContainer);
        }
        
        private void UpdateSummaryVisibility(bool showSummary)
        {
            if (_summaryContainer == null) return;
            
            _summaryContainer.style.display = (showSummary && EnableSummary) 
                ? DisplayStyle.Flex 
                : DisplayStyle.None;
        }
        
        private void RefreshSummary(SerializedProperty prop)
        {
            if (_summaryContainer == null || !EnableSummary) return;
            
            _summaryContainer.Clear();
            
            if (prop.propertyType != SerializedPropertyType.ManagedReference ||
                prop.managedReferenceValue == null)
            {
                return;
            }
            
            // PopulateSummary(_summaryContainer, prop);
        }
        
        /// <summary>
        /// Override to populate the summary container with custom elements.
        /// Called when the drawer is collapsed and has a value.
        /// </summary>
        /// <param name="container">The container to add summary elements to</param>
        /// <param name="property">The serialized property being drawn</param>
        protected virtual void PopulateSummary(VisualElement container, SerializedProperty property)
        {
            // Default implementation: show key field values as badges
            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();
            
            int fieldCount = 0;
            const int maxFields = 3; // Limit to prevent overflow
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;
                    
                    if (fieldCount >= maxFields)
                    {
                        // Add ellipsis indicator
                        container.Add(CreateSummaryLabel("...", ForgeDrawerStyles.Colors.HintText));
                        break;
                    }
                    
                    var summaryElement = CreateFieldSummary(iterator.Copy());
                    if (summaryElement != null)
                    {
                        if (fieldCount > 0)
                        {
                            summaryElement.style.marginLeft = 4;
                        }
                        container.Add(summaryElement);
                        fieldCount++;
                    }
                    
                } while (iterator.NextVisible(false));
            }
            
            if (fieldCount == 0)
            {
                container.Add(CreateSummaryLabel("(empty)", ForgeDrawerStyles.Colors.HintText, true));
            }
        }
        
        /// <summary>
        /// Creates a summary element for a single field.
        /// Override to customize how individual fields appear in the summary.
        /// </summary>
        protected virtual VisualElement CreateFieldSummary(SerializedProperty fieldProp)
        {
            string valueStr = GetFieldSummaryValue(fieldProp);
            if (string.IsNullOrEmpty(valueStr)) return null;
            
            return CreateSummaryBadge(valueStr, AccentColor);
        }
        
        /// <summary>
        /// Gets a string representation of a field value for the summary.
        /// </summary>
        protected virtual string GetFieldSummaryValue(SerializedProperty fieldProp)
        {
            return fieldProp.propertyType switch
            {
                SerializedPropertyType.ObjectReference => fieldProp.objectReferenceValue != null 
                    ? fieldProp.objectReferenceValue.name 
                    : null,
                SerializedPropertyType.String => !string.IsNullOrEmpty(fieldProp.stringValue) 
                    ? fieldProp.stringValue 
                    : null,
                SerializedPropertyType.Integer => fieldProp.intValue.ToString(),
                SerializedPropertyType.Float => fieldProp.floatValue.ToString("F1"),
                SerializedPropertyType.Boolean => fieldProp.boolValue ? "✓" : "✗",
                SerializedPropertyType.Enum => fieldProp.enumDisplayNames.Length > fieldProp.enumValueIndex && fieldProp.enumValueIndex >= 0
                    ? fieldProp.enumDisplayNames[fieldProp.enumValueIndex]
                    : null,
                SerializedPropertyType.Vector2 => $"({fieldProp.vector2Value.x:F1}, {fieldProp.vector2Value.y:F1})",
                SerializedPropertyType.Vector3 => $"({fieldProp.vector3Value.x:F1}, {fieldProp.vector3Value.y:F1}, {fieldProp.vector3Value.z:F1})",
                _ => null
            };
        }
        
        /// <summary>
        /// Helper to create a styled summary badge.
        /// </summary>
        protected VisualElement CreateSummaryBadge(string text, Color color, string tooltip = null)
        {
            var badge = new Label(text)
            {
                tooltip = tooltip,
                style =
                {
                    fontSize = 9,
                    color = color,
                    backgroundColor = new Color(color.r, color.g, color.b, 0.15f),
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 1,
                    paddingBottom = 1,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    maxWidth = 80,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            return badge;
        }
        
        /// <summary>
        /// Helper to create a simple summary label.
        /// </summary>
        protected VisualElement CreateSummaryLabel(string text, Color color, bool italic = false)
        {
            var label = new Label(text)
            {
                style =
                {
                    fontSize = 9,
                    color = color,
                    unityFontStyleAndWeight = italic ? FontStyle.Italic : FontStyle.Normal
                }
            };
            return label;
        }
        
        private void ToggleCollapse(SerializedProperty prop)
        {
            bool newCollapsed = !IsCollapsed(prop.propertyPath);
            SetCollapsed(prop.propertyPath, newCollapsed);
            
            // Update button appearance
            if (_collapseButton != null)
            {
                _collapseButton.text = newCollapsed 
                    ? ForgeDrawerStyles.Icons.ChevronRight 
                    : ForgeDrawerStyles.Icons.ChevronDown;
                _collapseButton.tooltip = newCollapsed ? "Expand" : "Collapse";
            }
            
            // Show/hide child fields and summary
            if (_childFieldsContainer != null)
            {
                if (newCollapsed)
                {
                    _childFieldsContainer.style.display = DisplayStyle.None;
                    UpdateSummaryVisibility(true);
                    RefreshSummary(prop);
                }
                else
                {
                    _childFieldsContainer.style.display = DisplayStyle.Flex;
                    UpdateSummaryVisibility(false);
                    
                    // Repopulate if needed (in case fields weren't populated when collapsed)
                    if (_childFieldsContainer.childCount == 0)
                    {
                        PopulateChildFields(prop);
                    }
                }
            }
        }
        
        protected void OnPropertyChanged(SerializedProperty prop)
        {
            bool hasValue = prop.propertyType == SerializedPropertyType.ManagedReference && 
                           prop.managedReferenceValue != null;
            bool isCollapsed = IsCollapsed(prop.propertyPath);
            
            // Update collapse button visibility
            if (_collapseButton != null)
            {
                _collapseButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Only populate if not collapsed (or no value)
            if (!hasValue)
            {
                _childFieldsContainer?.Clear();
                _childFieldsContainer.style.display = DisplayStyle.None;
                _summaryContainer?.Clear();
                UpdateSummaryVisibility(false);
            }
            else if (!isCollapsed)
            {
                _childFieldsContainer.style.display = DisplayStyle.Flex;
                UpdateSummaryVisibility(false);
                PopulateChildFields(prop);
            }
            else
            {
                // Collapsed with value - refresh summary
                UpdateSummaryVisibility(true);
                RefreshSummary(prop);
            }
        }

        /// <summary>
        /// Populates the child fields container with PropertyFields for each
        /// serialized field of the concrete worker implementation.
        /// </summary>
        protected void PopulateChildFields(SerializedProperty property)
        {
            _childFieldsContainer.Clear();
            
            if (property.propertyType != SerializedPropertyType.ManagedReference ||
                property.managedReferenceValue == null)
            {
                return;
            }

            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();
            
            int fieldCount = 0;
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;
                    
                    var childProp = iterator.Copy();
                    var field = new PropertyField(childProp)
                    {
                        style = { marginBottom = 2 }
                    };
                    
                    field.label = ObjectNames.NicifyVariableName(childProp.name);
                    _childFieldsContainer.Add(field);
                    fieldCount++;
                    
                } while (iterator.NextVisible(false));
            }
            
            // If no fields, hide the container entirely
            if (fieldCount == 0)
            {
                _childFieldsContainer.style.display = DisplayStyle.None;
                
                // Also hide collapse button if there's nothing to collapse
                if (_collapseButton != null)
                {
                    _collapseButton.style.display = DisplayStyle.None;
                }
            }
            else
            {
                _childFieldsContainer.Bind(property.serializedObject);
            }
        }

        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) 
                return base.GetStringValue(prop, value);

            var name = value.Name;
            
            // Clean up common suffixes for cleaner display
            name = name.Replace("AttributeWorker", "");
            name = name.Replace("ImpactWorker", "");
            name = name.Replace("EffectWorker", "");
            name = name.Replace("TagWorker", "");
            name = name.Replace("AnalysisWorker", "");
            name = name.Replace("Worker", "");
            
            return ObjectNames.NicifyVariableName(name);
        }
        
        protected override void SetValue(SerializedProperty prop, Type value)
        {
            base.SetValue(prop, value);
            
            // Reset collapsed state when type changes
            SetCollapsed(prop.propertyPath, false);
            
            _childFieldsContainer?.schedule.Execute(() =>
            {
                prop.serializedObject.Update();
                
                // Update collapse button visibility
                bool hasValue = value != null;
                if (_collapseButton != null)
                {
                    _collapseButton.style.display = hasValue ? DisplayStyle.Flex : DisplayStyle.None;
                    _collapseButton.text = ForgeDrawerStyles.Icons.ChevronDown;
                    _collapseButton.tooltip = "Collapse";
                }
                
                // Show and populate child fields
                if (hasValue)
                {
                    _childFieldsContainer.style.display = DisplayStyle.Flex;
                }
                
                PopulateChildFields(prop);
            });
        }
        
        /// <summary>
        /// Gets the number of serializable fields for the current value.
        /// Useful for determining if collapse button should be shown.
        /// </summary>
        protected int GetFieldCount(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference ||
                property.managedReferenceValue == null)
            {
                return 0;
            }

            int count = 0;
            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;
                    count++;
                } while (iterator.NextVisible(false));
            }
            
            return count;
        }
    }
}