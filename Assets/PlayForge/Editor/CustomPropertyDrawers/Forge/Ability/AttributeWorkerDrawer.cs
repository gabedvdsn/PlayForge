using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AbstractAttributeWorker))]
    public class AttributeWorkerDrawer : AbstractTypeRefDrawer<AbstractAttributeWorker>
    {
        private VisualElement _childFieldsContainer;
        private SerializedProperty _currentProperty;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            _currentProperty = prop;
            
            // Get the base GUI (type selector + dropdown)
            var baseGui = base.CreatePropertyGUI(prop);
            
            // Create container for child fields
            _childFieldsContainer = new VisualElement
            {
                name = "validation-rule-fields",
                style =
                {
                    marginLeft = 8,
                    marginTop = 4,
                    marginBottom = 4,
                    paddingLeft = 8,
                    borderLeftWidth = 2,
                    borderLeftColor = new Color(0.4f, 0.4f, 0.4f, 0.6f)
                }
            };
            
            // Insert child fields container after the first child (the type selector row)
            // and before the dropdown (second child)
            if (baseGui.childCount >= 1)
            {
                // Insert at index 1 (after the type selector row, before dropdown)
                baseGui.Insert(1, _childFieldsContainer);
            }
            else
            {
                baseGui.Add(_childFieldsContainer);
            }
            
            // Initial population of child fields
            PopulateChildFields(prop);
            
            // Track property changes to refresh child fields when type changes
            baseGui.TrackPropertyValue(prop, OnPropertyChanged);
            
            return baseGui;
        }

        private void OnPropertyChanged(SerializedProperty prop)
        {
            // Refresh child fields when the managed reference type changes
            PopulateChildFields(prop);
        }

        /// <summary>
        /// Populates the child fields container with PropertyFields for each
        /// serialized field of the concrete validation rule implementation.
        /// </summary>
        private void PopulateChildFields(SerializedProperty property)
        {
            _childFieldsContainer.Clear();
            
            // Check if there's a managed reference value
            if (property.propertyType != SerializedPropertyType.ManagedReference ||
                property.managedReferenceValue == null)
            {
                return;
            }

            // Get the type name for potential display
            var typeName = property.managedReferenceValue.GetType().Name;
            
            // Iterate through all visible children of the SerializeReference property
            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();
            
            bool hasFields = false;
            
            // Enter the first child
            if (iterator.NextVisible(true))
            {
                do
                {
                    // Stop if we've gone past this property's scope
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;
                    
                    hasFields = true;
                    
                    // Create a PropertyField for each child property
                    // This ensures custom drawers (like Tag, Attribute) are properly invoked
                    var childProp = iterator.Copy();
                    var field = new PropertyField(childProp)
                    {
                        style =
                        {
                            marginBottom = 2
                        }
                    };
                    
                    // Use nicified label
                    field.label = ObjectNames.NicifyVariableName(childProp.name);
                    
                    _childFieldsContainer.Add(field);
                    
                } while (iterator.NextVisible(false));
            }
            
            // If no fields, optionally show a message (or just leave empty)
            if (!hasFields)
            {
                // No configurable fields - container stays empty
                // This keeps the UI clean for simple validations like CooldownValidation
            }
            
            // Bind to ensure property changes are tracked
            _childFieldsContainer.Bind(property.serializedObject);
        }

        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) 
                return base.GetStringValue(prop, value);

            //if (prop.managedReferenceValue is IAbilityValidationRule rule) return rule.GetName();

            // Clean up the display name
            var name = value.Name;
            
            name = name.Replace("ValidationRule", "");
            name = name.Replace("Validation", "");
            
            return ObjectNames.NicifyVariableName(name);
        }
        
        /// <summary>
        /// Override SetValue to refresh child fields when type changes via dropdown selection
        /// </summary>
        protected override void SetValue(SerializedProperty prop, Type value)
        {
            base.SetValue(prop, value);
            
            // Schedule a refresh of child fields after the value is set
            // This handles the case where user selects a new type from dropdown
            _childFieldsContainer?.schedule.Execute(() =>
            {
                prop.serializedObject.Update();
                PopulateChildFields(prop);
            });
        }

        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return false;
        }
        protected override bool AcceptClear()
        {
            return false;
        }
        protected override bool AcceptAdd()
        {
            return false;
        }
    }
}
