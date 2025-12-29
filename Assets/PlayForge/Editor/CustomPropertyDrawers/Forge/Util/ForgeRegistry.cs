using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [InitializeOnLoad]
    public static class ForgeRegistry
    {
        public static VisualTreeAsset Dropdown;
        public static VisualTreeAsset DropdownEntry;
        
        private static Dictionary<Type, ScriptableObject[]> cache = new();
        private static string[] tagList = Array.Empty<string>();

        static ForgeRegistry()
        {
            EditorApplication.projectChanged += ClearCache;

            //RefreshAllTags();
        }
    
        public static T[] GetAll<T>() where T : ScriptableObject
        {
            var type = typeof(T);

            if (!cache.TryGetValue(type, out var objects)) objects = Refresh<T>();

            return objects as T[];
        }

        public static string[] GetAllTags() => tagList;

        public static T[] Refresh<T>() where T : ScriptableObject
        {
            var type = typeof(T);
            string filter = $"t:{type.Name}";

            string[] guids = AssetDatabase.FindAssets(filter);
            List<ScriptableObject> results = new();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T obj = AssetDatabase.LoadAssetAtPath<T>(path);
                if (obj is not null) results.Add(obj);
            }

            var arr = results.ToArray();
            cache[type] = arr;

            return arr as T[];
        }

        public static void ClearCache() => cache.Clear();

    }

}