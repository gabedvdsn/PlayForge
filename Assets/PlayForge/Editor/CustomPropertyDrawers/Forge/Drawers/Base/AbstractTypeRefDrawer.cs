using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Base drawer for [SerializeReference] fields with abstract/interface types.
    /// Provides a type picker dropdown that creates instances of selected concrete types.
    /// 
    /// Use this when you only need type selection without inline field editing.
    /// For inline field editing with collapse/expand, use AbstractGenericDrawer.
    /// </summary>
    public abstract class AbstractTypeRefDrawer<T> : AbstractRefDrawer<Type> where T : class
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // State
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected Type CurrentType;

        // ═══════════════════════════════════════════════════════════════════════════
        // Virtual Configuration
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Override to filter which types appear in the dropdown.</summary>
        protected virtual bool FilterType(Type type) => true;
        
        /// <summary>Override to customize type name cleaning (e.g., removing suffixes).</summary>
        protected virtual string CleanTypeName(string typeName) => typeName;

        // ═══════════════════════════════════════════════════════════════════════════
        // AbstractRefDrawer Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override Type[] GetEntries()
        {
            return TypePickerCache.GetConcreteTypesAssignableTo<T>()
                .Where(FilterType)
                .OrderBy(t => t.Name)
                .ToArray();
        }

        protected override void SetValue(SerializedProperty prop, Type value)
        {
            CurrentType = value;
            prop.managedReferenceValue = value != null 
                ? Activator.CreateInstance(value) as T 
                : null;
            
            prop.serializedObject.ApplyModifiedProperties();
            OnTypeChanged(prop, value);
            Repaint();
        }

        protected override Type GetCurrentValue(SerializedProperty prop)
        {
            // Return cached if valid
            if (CurrentType != null) 
                return CurrentType;

            // Try from current instance
            if (prop.managedReferenceValue != null)
            {
                CurrentType = prop.managedReferenceValue.GetType();
                return CurrentType;
            }

            // Try to parse from serialized type name
            CurrentType = ParseTypeFromProperty(prop);
            if (CurrentType != null)
                return CurrentType;

            // Apply default if configured
            var defaultType = GetDefault(prop);
            if (defaultType != null)
            {
                SetValue(prop, defaultType);
                return defaultType;
            }

            return null;
        }

        protected override bool CompareTo(Type value, Type other) => value == other;
        
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value == null) return "<None>";
            return ObjectNames.NicifyVariableName(CleanTypeName(value.Name));
        }
        
        protected override Label GetLabel(SerializedProperty prop, Type value)
        {
            return IsInList(prop) ? null : new Label(prop.displayName);
        }

        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
        protected override bool AcceptClear(SerializedProperty prop) => GetCurrentValue(prop) != null;

        protected override void ClearReferenceValue(SerializedProperty prop)
        {
            prop.managedReferenceValue = null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Extension Points
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Called after a new type is selected. Override for additional setup.</summary>
        protected virtual void OnTypeChanged(SerializedProperty prop, Type newType) { }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private Type ParseTypeFromProperty(SerializedProperty prop)
        {
            string fullTypeName = prop.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullTypeName)) return null;

            var parts = fullTypeName.Split(' ');
            if (parts.Length != 2) return null;

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == parts[1]);
        }
        
        private static bool IsInList(SerializedProperty prop) => 
            prop.propertyPath.Contains(".Array.data[");
    }
}