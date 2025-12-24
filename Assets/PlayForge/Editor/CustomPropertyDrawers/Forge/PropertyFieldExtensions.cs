using System;
using UnityEditor.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
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
