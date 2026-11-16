using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// The concrete classes a type allows, by name, kept per type for the editor session; a kind field's picker lists
    /// them. Its own picker window, the field that opened it and the TypeRef it wrote went unused, and went.
    /// </summary>
    public static class TypePickerCache
    {
        static readonly Dictionary<Type, List<Type>> _byBase = new();

        public static IReadOnlyList<Type> GetConcreteTypesAssignableTo(Type baseOrInterface)
        {
            if (baseOrInterface == null) return Array.Empty<Type>();
            if (_byBase.TryGetValue(baseOrInterface, out var cached)) return cached;

            IEnumerable<Type> src = TypeCache.GetTypesDerivedFrom(baseOrInterface);

            var list = src
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _byBase[baseOrInterface] = list;
            return list;
        }

        public static IReadOnlyList<Type> GetConcreteTypesAssignableTo<TBase>() => GetConcreteTypesAssignableTo(typeof(TBase));
    }
}
