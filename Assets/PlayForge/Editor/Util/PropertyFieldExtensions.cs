using System;
using UnityEditor.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Extensions to UI Toolkit's <c>PropertyField</c>: <c>OnPopulated</c> runs a callback once the field has built its
    /// children, at once where it has, or else on the field's scheduler, asking again until it has.
    /// </summary>
    public static class PropertyFieldExtensions
    {
        public static void OnPopulated(this PropertyField propertyField, Action callback)
        {
            if (propertyField.childCount > 0)
            {
                callback?.Invoke();
            }
            else
            {
                propertyField.schedule.Execute(() => OnPopulated(propertyField, callback));
            }
        }
    }
}
