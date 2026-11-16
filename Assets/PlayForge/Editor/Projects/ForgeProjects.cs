using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// The project's Forge Projects and the active one (FEAT-34, Decision 44). The Forge shows the active project's assets
    /// alone, and a new asset goes in it: in the folder Project Settings names for its type, within the project's folder.
    /// A project is made in a folder of its own under the PlayForge Folder, as Assets/PlayForge/Arena, with its Game Root
    /// entity, and the Bootstrap prefabs made where they're missing (EP-10, EP-11, Decision 89). While the Unity project has
    /// no Forge Project, The Forge asks for one; and the first asset made elsewhere makes one named for the game. Which is
    /// active belongs to each user, so it stays in EditorPrefs, for this Unity project alone.
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
        /// The project an asset is in, the innermost whose folder holds it, which its settings come from in the editor; or
        /// none, for one in no project or not yet saved (TW-29).
        /// </summary>
        public static ForgeProject Of(Object asset)
        {
            var path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            return string.IsNullOrEmpty(path) ? null : ForgeProject.Owner(path, ForgeProject.Folders());
        }

        /// <summary>
        /// The active project, made while the Unity project has none: one named for the game, in a folder of its own under
        /// the PlayForge Folder (EP-10). It was made at the project's root, in no folder of its own.
        /// </summary>
        public static ForgeProject EnsureActive()
        {
            var active = Active;
            if (active != null) return active;
            // Projects, none chosen and none at the root: the assets are those in no project, and so is a new one's
            if (All().Count > 0) return null;
            return Make(DefaultName);
        }

        /// <summary>The name a project made without one takes: the game's, as Player Settings names it, or My Game.</summary>
        public static string DefaultName
        {
            get
            {
                var game = ForgeAssetCreation.FileNameOf(PlayerSettings.productName ?? "").Trim();
                return string.IsNullOrEmpty(game) || WhyNotName(game) != null ? "My Game" : game;
            }
        }

        /// <summary>
        /// Why a project can't take a name, or null where it can: it needs one, and the PlayForge Folder's Bootstrap folder
        /// isn't a project's (EP-11).
        /// </summary>
        public static string WhyNotName(string name)
        {
            var file = ForgeAssetCreation.FileNameOf(name ?? "").Trim();
            if (string.IsNullOrEmpty(file)) return "A Forge Project needs a name.";
            if (string.Equals(file, ForgeFolders.BootstrapName, StringComparison.OrdinalIgnoreCase))
                return $"{ForgeFolders.BootstrapName} is where PlayForge keeps the prefabs a scene starts with.";
            return null;
        }

        /// <summary>
        /// The folder a new asset of a type goes in: the type's folder from Project Settings, within the active project's
        /// folder, as Abilities is Assets/PlayForge/Arena/Abilities in a project at Assets/PlayForge/Arena (EP-10).
        /// </summary>
        public static string FolderFor(Type type) => FolderIn(Active, type);

        /// <summary>
        /// The folder an asset of a type goes in, in a project: the type's folder from Project Settings, within the
        /// project's folder, a folder set as "Assets/Data/Abilities" read as "Data/Abilities" there; under the PlayForge
        /// Folder for no project (EP-10).
        /// </summary>
        public static string FolderIn(ForgeProject project, Type type)
        {
            var folder = PlayForgeSettings.FolderFor(type, project);
            var root = project != null ? project.Folder : ForgeFolders.Root;
            var within = folder == "Assets" ? ""
                : folder.StartsWith("Assets/", StringComparison.Ordinal) ? folder.Substring("Assets/".Length)
                : folder;
            return string.IsNullOrEmpty(within) ? root : $"{root}/{within}";
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

        /// <summary>
        /// A new project, named, made at once in a folder of its own under the PlayForge Folder, as
        /// Assets/PlayForge/Arena/Arena.asset, with its Game Root entity in its GameRoot folder; the Bootstrap prefabs are made
        /// where they're missing, the Bootstrapper playing it where it plays none. It becomes the active one (EP-10, EP-11).
        /// </summary>
        public static ForgeProject Make(string name)
        {
            name = string.IsNullOrWhiteSpace(name) || WhyNotName(name) != null ? DefaultName : name.Trim();
            var file = ForgeAssetCreation.FileNameOf(name);
            var root = ForgeFolders.Root;
            ForgeAssetCreation.EnsureFolder(root);
            var folder = AssetDatabase.GenerateUniqueAssetPath($"{root}/{file}");
            ForgeAssetCreation.EnsureFolder(folder);
            var project = ScriptableObject.CreateInstance<ForgeProject>();
            AssetDatabase.CreateAsset(project, $"{folder}/{file}.asset");
            ForgeBootstrap.MakeGameRootEntity(project);
            ForgeBootstrap.EnsurePrefabs(project);
            Active = project;
            return project;
        }
    }
}
