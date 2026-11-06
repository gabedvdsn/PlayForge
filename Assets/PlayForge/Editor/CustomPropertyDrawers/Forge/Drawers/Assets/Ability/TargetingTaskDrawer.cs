using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>An ability's targeting task: its kind, named without "TargetTask" or "Task", then its fields (ED-56).</summary>
    [CustomPropertyDrawer(typeof(AbstractTargetingAbilityTask))]
    public class TargetingTaskDrawer : AbstractGenericDrawer<AbstractTargetingAbilityTask>
    {
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) return base.GetStringValue(prop, value);
            var name = value.Name.Replace("TargetTask", "").Replace("Task", "");
            return ObjectNames.NicifyVariableName(name);
        }
    }
}
