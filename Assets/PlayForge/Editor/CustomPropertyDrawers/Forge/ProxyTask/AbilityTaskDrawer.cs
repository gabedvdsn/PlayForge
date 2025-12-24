using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Custom property drawer for AbstractAbilityTask that extends AbstractTypeRefDrawer
    /// to also display the serialized fields of each concrete task implementation.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractAbilityTask))]
    public class AbilityTaskDrawer : AbstractTypeRefDrawer<AbstractAbilityTask>
    {
        private VisualElement _childFieldsContainer;

        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            // Get the base GUI (type selector + dropdown)
            var baseGui = base.CreatePropertyGUI(prop);
            
            // Create container for child fields
            _childFieldsContainer = new VisualElement
            {
                name = "task-fields",
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
            PopulateChildFields(prop);
        }

        /// <summary>
        /// Populates the child fields container with PropertyFields for each
        /// serialized field of the concrete task implementation.
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
            
            // Bind to ensure property changes are tracked
            if (hasFields)
            {
                _childFieldsContainer.Bind(property.serializedObject);
            }
        }

        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) 
                return base.GetStringValue(prop, value);
            
            // Clean up the display name
            var name = value.Name;
            name = name.Replace("AbilityTask", "");
            name = name.Replace("Task", "");
            
            return name;
        }
        
        /// <summary>
        /// Override SetValue to refresh child fields when type changes via dropdown selection
        /// </summary>
        protected override void SetValue(SerializedProperty prop, Type value)
        {
            base.SetValue(prop, value);
            
            // Schedule a refresh of child fields after the value is set
            _childFieldsContainer?.schedule.Execute(() =>
            {
                prop.serializedObject.Update();
                PopulateChildFields(prop);
            });
        }
    }
}