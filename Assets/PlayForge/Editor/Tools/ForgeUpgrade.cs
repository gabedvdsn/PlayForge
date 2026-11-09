using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Window/PlayForge/Upgrade Assets: re-saves every asset in a project that holds PlayForge data, so each is written in
    /// the form the current version saves (FEAT-29). A field that became a number moves its old float on load, through
    /// its legacy field (<see cref="LegacyNumber"/>); re-saving writes the number, so the legacy fields can go a version
    /// after. An asset holds PlayForge data where its class, or a class it holds by reference, comes from PlayForge, or
    /// where its class keeps a field of PlayForge's own types, as a number.
    /// </summary>
    public static class ForgeUpgrade
    {
        private const string MenuPath = "Window/PlayForge/Upgrade Assets";

        // The PlayForge assemblies' names, whose classes' data an asset may hold
        private const string PlayForgeAssembly = "FarEmerald.PlayForge";

        [MenuItem(MenuPath, false, 30)]
        private static void UpgradeFromMenu()
        {
            var paths = AssetsToUpgrade();
            if (paths.Count == 0)
            {
                Debug.Log("[PlayForge] Upgrade Assets: no asset holds PlayForge data.");
                return;
            }
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Upgrade Assets",
                    $"Re-save {paths.Count} assets that hold PlayForge data, so each is written in this version's form? " +
                    "Source control shows each one that changes.", "Re-save", "Cancel"))
                return;
            int saved = Resave(paths);
            Debug.Log($"[PlayForge] Upgrade Assets: re-saved {saved} assets.");
        }

        /// <summary>
        /// The assets under the folders given, the project's Assets folder by default, that hold PlayForge data: scriptable
        /// objects, prefabs and scenes.
        /// </summary>
        public static IReadOnlyList<string> AssetsToUpgrade(params string[] folders)
        {
            if (folders == null || folders.Length == 0) folders = new[] { "Assets" };
            var scripts = PlayForgeScriptGuids();
            var found = new List<string>();
            foreach (var path in AssetDatabase.FindAssets("", folders).Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                if (!(path.EndsWith(".asset") || path.EndsWith(".prefab") || path.EndsWith(".unity"))) continue;
                if (HoldsPlayForge(path, scripts)) found.Add(path);
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }

        /// <summary>Re-saves the assets, each in the current form, and says how many it re-saved.</summary>
        public static int Resave(IEnumerable<string> paths)
        {
            var list = paths?.Where(path => !string.IsNullOrEmpty(path) && File.Exists(path)).Distinct().ToList() ?? new List<string>();
            if (list.Count == 0) return 0;
            AssetDatabase.ForceReserializeAssets(list, ForceReserializeAssetsOptions.ReserializeAssets);
            return list.Count;
        }

        // Whether an asset holds PlayForge data: its text names a PlayForge script or a class from a PlayForge assembly by
        // reference, or, saved as binary, its main asset is of such a class
        private static bool HoldsPlayForge(string path, HashSet<string> scripts)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                return false;
            }

            if (!text.StartsWith("%YAML"))
                return AssetDatabase.GetMainAssetTypeAtPath(path) is { } type && HoldsPlayForge(type);
            if (text.Contains($"asm: {PlayForgeAssembly}")) return true;

            const string marker = "guid: ";
            for (int at = text.IndexOf(marker, StringComparison.Ordinal); at >= 0; at = text.IndexOf(marker, at + marker.Length, StringComparison.Ordinal))
            {
                int start = at + marker.Length;
                if (start + 32 <= text.Length && scripts.Contains(text.Substring(start, 32))) return true;
            }
            return false;
        }

        // The GUIDs of the scripts whose classes hold PlayForge data
        private static HashSet<string> PlayForgeScriptGuids()
        {
            var guids = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                var type = script != null ? script.GetClass() : null;
                if (type != null && HoldsPlayForge(type)) guids.Add(guid);
            }
            return guids;
        }

        private static readonly Dictionary<Type, bool> Held = new();

        /// <summary>
        /// Whether a class holds PlayForge data: it or a class it's built on comes from a PlayForge assembly, or it keeps a
        /// saved field of a PlayForge type, as a number.
        /// </summary>
        public static bool HoldsPlayForge(Type type)
        {
            if (type == null) return false;
            if (Held.TryGetValue(type, out var known)) return known;
            Held[type] = false;
            bool holds = false;
            for (var each = type; each != null && each != typeof(object) && !holds; each = each.BaseType)
            {
                if (FromPlayForge(each)) holds = true;
                else
                {
                    foreach (var field in each.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.IsNotSerialized) continue;
                        if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null && field.GetCustomAttribute<SerializeReference>() == null) continue;
                        var held = field.FieldType.IsArray ? field.FieldType.GetElementType()
                            : field.FieldType.IsGenericType ? field.FieldType.GetGenericArguments().FirstOrDefault() : field.FieldType;
                        if (held != null && FromPlayForge(held)) { holds = true; break; }
                    }
                }
            }
            Held[type] = holds;
            return holds;
        }

        private static bool FromPlayForge(Type type)
        {
            var name = type.Assembly.GetName().Name;
            return name == PlayForgeAssembly || name.StartsWith(PlayForgeAssembly + ".Editor");
        }
    }
}
