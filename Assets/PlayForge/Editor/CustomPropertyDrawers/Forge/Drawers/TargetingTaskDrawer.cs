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
    }
}
