using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A Forge Project: a set of PlayForge assets of its own, those under the folder its asset is in.
    /// The Forge shows the active project's assets alone, and makes new assets in it. A project's folder can hold another
    /// project's, and an asset is in the innermost project whose folder holds it. It's an asset catalog too, of its own
    /// assets, for saves, which it keeps current as they're made, moved in and deleted; Add Its Assets
    /// names them by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Forge Project", fileName = "Forge Project", order = 0)]
    public class ForgeProject : ForgeAssetCatalog
    {
        [Tooltip("What the project is for")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("The project's settings, which its game reads as it runs: its attributes' roles, and the crowd control rules " +
                 "an entity follows where it names none")]
        public ForgeProjectSettings Settings = new();

        [Tooltip("The entity its game's GameRoot runs as, the game's own attributes, abilities and workers, as a scene's " +
                 "Bootstrapper plays the project. The project's GameRoot folder holds it")]
        public EntityIdentity GameRootEntity;

        [Tooltip("Names each PlayForge asset made, copied or moved into its folder for saves, by its GUID, as it lands, and lets " +
                 "go of one deleted, so a save finds every asset with no Add Its Assets to run. One moved out stays named, so a " +
                 "save made before still finds it. Off, the catalog is left as it is")]
        public bool KeepCatalogCurrent = true;

        // The project code uses, in place of the GameRoot's
        private static ForgeProject used;

        // Statics outlive play mode when domain reload is off
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearUsed() => used = null;

        /// <summary>
        /// The project a running game reads its settings from: the one code uses, or else its GameRoot's; or null,
        /// where neither names one. The editor's active project isn't among them, so play mode reads what a build does.
        /// </summary>
        public static ForgeProject Current
        {
            get
            {
                if (used) return used;
                var root = GameRoot.Instance;
                return root && root.Project ? root.Project : null;
            }
        }

        /// <summary>
        /// Uses <paramref name="project"/> in place of the GameRoot's, as a test or a game with no GameRoot does, or the
        /// GameRoot's again for null. Gives back the one used before, to put back.
        /// </summary>
        public static ForgeProject Use(ForgeProject project)
        {
            var was = used;
            used = project;
            return was;
        }

#if UNITY_EDITOR
        /// <summary>The folder the project's asset is in, which holds its assets, as "Assets/Games/Arena".</summary>
        public string Folder => FolderOf(UnityEditor.AssetDatabase.GetAssetPath(this));

        /// <summary>
        /// Adds each PlayForge asset of the project's own, under its folder and in no project inside it, that it doesn't
        /// name yet, named by its GUID, and says how many it added.
        /// </summary>
        [ContextMenu("Add Its Assets")]
        public int AddOwnAssets()
        {
            var folders = Folders();
            return AddAssets(path => Owner(path, folders) == this);
        }

        /// <summary>Each project's folder, with the project, as <see cref="Owner"/> reads them.</summary>
        public static List<(string Folder, ForgeProject Project)> Folders()
        {
            var found = new List<(string, ForgeProject)>();
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"t:{nameof(ForgeProject)}"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var project = UnityEditor.AssetDatabase.LoadAssetAtPath<ForgeProject>(path);
                if (project != null) found.Add((FolderOf(path), project));
            }
            return found;
        }

        /// <summary>The project an asset at a path is in: the innermost whose folder holds it; none where none does.</summary>
        public static ForgeProject Owner(string assetPath, IReadOnlyList<(string Folder, ForgeProject Project)> folders)
        {
            if (string.IsNullOrEmpty(assetPath) || folders == null) return null;
            ForgeProject owner = null;
            int deepest = -1;
            foreach (var (folder, project) in folders)
            {
                if (folder.Length <= deepest || !assetPath.StartsWith(folder + "/", StringComparison.Ordinal)) continue;
                owner = project;
                deepest = folder.Length;
            }
            return owner;
        }

        private static string FolderOf(string assetPath)
        {
            int slash = string.IsNullOrEmpty(assetPath) ? -1 : assetPath.LastIndexOf('/');
            return slash < 0 ? "Assets" : assetPath.Substring(0, slash);
        }
#endif
    }
}
