using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The project's Forge Projects and the active one (FEAT-34, Decision 44). The Forge shows the active project's assets
    /// alone, and a new asset goes in it: in the folder Project Settings names for its type, within the project's folder.
    /// While the project has no Forge Project, The Forge shows every asset, and the first asset made makes the default
    /// one, at the project's root, which holds every asset no other project does. Which is active belongs to each user,
    /// so it stays in EditorPrefs, for this Unity project alone.
    /// </summary>
    internal static class ForgeProjects
    {
        /// <summary>Raised as another project becomes the active one.</summary>
        public static event Action Changed;

        // The active project's GUID, for this Unity project, keyed by a hash of its path that lasts across sessions
        private static string ActiveKey => $"{PlayForgePreferences.Prefix}ActiveProject_{StableHash(Application.dataPath):X8}";

        private static uint StableHash(string text)
        {
            uint hash = 2166136261;
            foreach (char each in text ?? "") hash = (hash ^ each) * 16777619;
            return hash;
        }

        /// <summary>The project's Forge Projects, by name.</summary>
        public static List<ForgeProject> All() => ForgeProject.Folders().Select(each => each.Project)
            .OrderBy(project => project.name, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>
        /// The active project: the one the user chose, or else the first; none while the project has none, when every
        /// asset is in.
        /// </summary>
        public static ForgeProject Active
        {
            get
            {
                var guid = EditorPrefs.GetString(ActiveKey, "");
                var chosen = string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<ForgeProject>(AssetDatabase.GUIDToAssetPath(guid));
                return chosen != null ? chosen : All().FirstOrDefault();
            }
            set
            {
                var guid = value == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
                if (EditorPrefs.GetString(ActiveKey, "") == guid) return;
                EditorPrefs.SetString(ActiveKey, guid);
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Whether assets are in the active project, read against each project's folder, which one call reads for many
        /// assets; every asset is, while the project has no Forge Project.
        /// </summary>
        public static Func<Object, bool> InActive()
        {
            var folders = ForgeProject.Folders();
            if (folders.Count == 0) return _ => true;
            var active = Active;
            return asset => asset != null && ForgeProject.Owner(AssetDatabase.GetAssetPath(asset), folders) == active;
        }

        /// <summary>
        /// The active project, made while the project has none: the default project, at the project's root, which holds
        /// every asset no other project does.
        /// </summary>
        public static ForgeProject EnsureActive()
        {
            var active = Active;
            if (active != null) return active;
            var made = ScriptableObject.CreateInstance<ForgeProject>();
            made.Description = "The project's own assets: those no other Forge Project holds";
            AssetDatabase.CreateAsset(made, AssetDatabase.GenerateUniqueAssetPath("Assets/Forge Project.asset"));
            Active = made;
            return made;
        }

        /// <summary>
        /// The folder a new asset of a type goes in: the type's folder from Project Settings, within the active project's
        /// folder, as Assets/Data/Abilities is Assets/Arena/Data/Abilities in a project at Assets/Arena. While the project
        /// has none, the type's folder, which the default project, at the project's root, holds.
        /// </summary>
        public static string FolderFor(Type type)
        {
            var folder = PlayForgeSettings.FolderFor(type);
            var root = Active != null ? Active.Folder : "Assets";
            if (root == "Assets") return folder;
            return folder == "Assets" ? root
                : folder.StartsWith("Assets/", StringComparison.Ordinal) ? root + folder.Substring("Assets".Length)
                : $"{root}/{folder}";
        }

        /// <summary>A new project, named, in a folder of its own at the project's root, which becomes the active one.</summary>
        public static ForgeProject Make(string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "New Project" : name.Trim();
            var folder = AssetDatabase.GenerateUniqueAssetPath($"Assets/{ForgeAssetCreation.FileNameOf(name)}");
            ForgeAssetCreation.EnsureFolder(folder);
            var project = ScriptableObject.CreateInstance<ForgeProject>();
            AssetDatabase.CreateAsset(project, $"{folder}/{ForgeAssetCreation.FileNameOf(name)}.asset");
            Active = project;
            return project;
        }
    }
}
