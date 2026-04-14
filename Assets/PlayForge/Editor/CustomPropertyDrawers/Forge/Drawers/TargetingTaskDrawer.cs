using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AbstractTargetingAbilityTask))]
    public class TargetingTaskDrawer : AbstractGenericDrawer<AbstractTargetingAbilityTask>
    {
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return false;
        }

        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) 
                return base.GetStringValue(prop, value);
            
            // Clean up the display name
            var name = value.Name;
            name = name.Replace("TargetTask", "");
            name = name.Replace("Task", "");

            return ObjectNames.NicifyVariableName(name);
        }
    }
}
