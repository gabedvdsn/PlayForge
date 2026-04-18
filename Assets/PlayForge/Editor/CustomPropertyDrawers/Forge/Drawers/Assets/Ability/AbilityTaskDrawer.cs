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
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) 
                return base.GetStringValue(prop, value);
            
            // Clean up the display name
            var name = value.Name;
            name = name.Replace("AbilityTask", "");
            name = name.Replace("Task", "");

            return ObjectNames.NicifyVariableName(name);
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