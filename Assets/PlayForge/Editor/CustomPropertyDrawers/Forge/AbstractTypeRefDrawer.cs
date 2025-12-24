using System;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public abstract class AbstractTypeRefDrawer<T> : AbstractRefDrawer<Type>, IDrawerToClass<T>
        where T : class
    {
        protected Type target;

        public virtual T Generate()
        {
            if (target is not null) return Activator.CreateInstance(target) as T;
            return null;
        }

        protected override Type[] GetEntries()
        {
            return TypePickerCache.GetConcreteTypesAssignableTo<T>().ToArray();
        }

        protected override void SetValue(SerializedProperty prop, Type value)
        {
            target = value;

            if (value is not null)
            {
                var instance = Activator.CreateInstance(value) as T;
                prop.managedReferenceValue = instance;
            }
            else prop.managedReferenceValue = null;
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
            if (target is not null) return target;

            if (prop.managedReferenceValue is not null)
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

            if (target is null)
            {
                target = GetDefault();
                if (target != null) SetValue(prop, target);
            }

            return null;
        }
        protected override Label GetLabel(SerializedProperty prop, Type value)
        {
            if (prop.isArray) return null;
            return new Label(prop.name);
        }

        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return GetCurrentValue(prop) is not null;
        }
        protected override bool AcceptClear()
        {
            return base.AcceptClear();
        }
        protected override bool AcceptAdd()
        {
            return false;
        }

    }
}
