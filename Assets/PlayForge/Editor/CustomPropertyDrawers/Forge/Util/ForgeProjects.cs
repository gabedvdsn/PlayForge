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
        /// The active project: the one the user chose; or else the one at the project's root, the default, which holds
        /// every asset no other project does; or else none, when The Forge shows the assets in no project, and every asset
        /// while the project has no Forge Project. It fell back on the first by name, so a project whose only Forge
        /// Project was the Demo's showed the Demo's assets alone.
        /// </summary>
        public static ForgeProject Active
        {
            get
            {
                var guid = EditorPrefs.GetString(ActiveKey, "");
                var chosen = string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<ForgeProject>(AssetDatabase.GUIDToAssetPath(guid));
                return chosen != null ? chosen : ForgeProject.Folders().FirstOrDefault(each => each.Folder == "Assets").Project;
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

        /// <summary>The active project's assets of a type, as a picker offers them; every one while the project has none.</summary>
        public static List<Object> AssetsOf(Type type)
        {
            var inActive = InActive();
            return AssetDatabase.FindAssets($"t:{type.Name}")
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Select(path => AssetDatabase.LoadAssetAtPath(path, type))
                .Where(asset => asset != null && inActive(asset))
                .ToList();
        }

        /// <summary>
        /// Whether a tag picker offers a tag in the active project: a default, one no asset uses, and one the project's
        /// assets or its Tag Library use; a tag only another project uses, it doesn't (the user's point, FEAT-34).
        /// </summary>
        public static Func<Tag, bool> OffersTag()
        {
            var inActive = InActive();
            return tag =>
            {
                var record = ForgeTagRegistry.GetTagUsage(tag);
                if (record == null || record.IsSystemDefault) return true;
                var users = record.UsageByContext.Values.SelectMany(usage => usage.Usages)
                    .Select(usage => usage.Asset).Where(asset => asset != null).ToList();
                return users.Count == 0 || users.Any(asset => inActive(asset));
            };
        }

        /// <summary>
        /// The active project, made while the project has none: the default project, at the project's root, which holds
        /// every asset no other project does.
        /// </summary>
        public static ForgeProject EnsureActive()
        {
            var active = Active;
            if (active != null) return active;
            // Projects, none chosen and none at the root: the assets are those in no project, and so is a new one's
            if (All().Count > 0) return null;
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
        public static string FolderFor(Type type) => FolderIn(Active, type);

        /// <summary>
        /// The folder an asset of a type goes in, in a project: the type's folder from Project Settings, within the
        /// project's folder; the type's folder alone for no project.
        /// </summary>
        public static string FolderIn(ForgeProject project, Type type)
        {
            var folder = PlayForgeSettings.FolderFor(type);
            var root = project != null ? project.Folder : "Assets";
            if (root == "Assets") return folder;
            return folder == "Assets" ? root
                : folder.StartsWith("Assets/", StringComparison.Ordinal) ? root + folder.Substring("Assets".Length)
                : $"{root}/{folder}";
        }

        /// <summary>
        /// Moves an asset into a project, its type's folder there, and gives its new path; or null where it can't move,
        /// which it logs.
        /// </summary>
        public static string MoveInto(Object asset, ForgeProject project)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || project == null) return null;
            var folder = FolderIn(project, asset.GetType());
            ForgeAssetCreation.EnsureFolder(folder);
            var target = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{System.IO.Path.GetFileName(path)}");
            var error = AssetDatabase.MoveAsset(path, target);
            if (string.IsNullOrEmpty(error)) return target;
            ForgeLog.Warning($"{asset.name} can't move into {project.name}: {error}");
            return null;
        }

        /// <summary>Copies an asset into a project, its type's folder there, and gives the copy; or null where it can't.</summary>
        public static Object CopyInto(Object asset, ForgeProject project)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || project == null) return null;
            var folder = FolderIn(project, asset.GetType());
            ForgeAssetCreation.EnsureFolder(folder);
            var target = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{System.IO.Path.GetFileName(path)}");
            return AssetDatabase.CopyAsset(path, target) ? AssetDatabase.LoadAssetAtPath<Object>(target) : null;
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
