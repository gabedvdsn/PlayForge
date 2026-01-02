using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Type picker drawer base class for selecting concrete types that implement/inherit from T.
    /// Creates instances of the selected type when assigned.
    /// </summary>
    public abstract class AbstractTypeRefDrawer<T> : AbstractRefDrawer<Type>
        where T : class
    {
        protected Type target;

        /// <summary>
        /// Generates an instance of the currently selected type.
        /// </summary>
        public virtual T Generate()
        {
            if (target != null) 
                return Activator.CreateInstance(target) as T;
            return null;
        }

        protected override Type[] GetEntries()
        {
            return TypePickerCache.GetConcreteTypesAssignableTo<T>().ToArray();
        }

        protected override void SetValue(SerializedProperty prop, Type value)
        {
            target = value;

            if (value != null)
            {
                var instance = Activator.CreateInstance(value) as T;
                prop.managedReferenceValue = instance;
            }
            else
            {
                prop.managedReferenceValue = null;
            }
        }

        protected override bool CompareTo(Type value, Type other)
        {
            return value == other;
        }
        
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            return ObjectNames.NicifyVariableName(value?.Name) ?? "<None>";
        }

        protected override Type GetCurrentValue(SerializedProperty prop)
        {
            if (target != null) 
                return target;

            if (prop.managedReferenceValue != null)
            {
                target = prop.managedReferenceValue.GetType();
                return target;
            }

            string fullTypeName = prop.managedReferenceFullTypename;
            if (!string.IsNullOrEmpty(fullTypeName))
            {
                var parts = fullTypeName.Split(' ');
                if (parts.Length == 2)
                {
                    var typeName = parts[1];
                    target = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.FullName == typeName);

                    return target;
                }
            }

            if (target == null)
            {
                target = GetDefault();
                if (target != null) 
                    SetValue(prop, target);
            }

            return null;
        }
        
        protected override Label GetLabel(SerializedProperty prop, Type value)
        {
            if (prop.isArray) return null;
            return new Label(prop.displayName);
        }

        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return GetCurrentValue(prop) != null;
        }
        
        protected override bool AcceptAdd()
        {
            return false;
        }
    }
}