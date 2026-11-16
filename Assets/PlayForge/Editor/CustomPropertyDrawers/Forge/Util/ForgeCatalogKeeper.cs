using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Keeps each Forge Project's asset catalog current (EP-15, the user's answer to EP-6's), so a save finds every asset
    /// with no Add Its Assets to run: a PlayForge asset made, copied or moved into a project's folder is named by its GUID
    /// as it lands, in the innermost project whose folder holds it, and a deleted file's entries go from every project. A
    /// project catches up on its own assets it doesn't name yet as it's imported, as a new one or a sample's is, and once
    /// each editor session, as one kept before EP-15 is. A deleted file is matched by its GUID, so an entry whose asset
    /// hasn't loaded yet, as on a fresh import, is never taken for one deleted; and an asset moved out of a project stays
    /// named there, so a save made before still finds it. A project with Keep Catalog Current off is left as it is.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class ForgeCatalogKeeper : AssetPostprocessor
    {
        private const string CaughtUpKey = "FarEmerald.PlayForge.CatalogsCaughtUp";

        // Once an editor session, past its first domain reload, as the asset database is ready
        static ForgeCatalogKeeper()
        {
            if (SessionState.GetBool(CaughtUpKey, false)) return;
            SessionState.SetBool(CaughtUpKey, true);
            EditorApplication.delayCall += CatchUpAll;
        }

        private static void CatchUpAll()
        {
            var folders = ForgeProject.Folders();
            foreach (var (_, project) in folders)
                if (project.KeepCatalogCurrent && CatchUp(project, folders) > 0) Save(project);
        }

        /// <summary>
        /// Names each of a project's own PlayForge assets it doesn't name yet, those in its folder and in no project inside
        /// it, as Add Its Assets does, and says how many. A file whose GUID it names already it leaves, unloaded.
        /// </summary>
        internal static int CatchUp(ForgeProject project, IReadOnlyList<(string Folder, ForgeProject Project)> folders)
        {
            if (project == null) return 0;
            var named = new HashSet<string>(project.Entries.Select(entry => entry.Id ?? ""), StringComparer.Ordinal);
            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { project.Folder }))
            {
                if (named.Contains(guid)) continue;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ForgeProject.Owner(path, folders) != project) continue;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is BaseForgeAsset forgeAsset && project.AddByGuid(forgeAsset)) added++;
            }
            return added;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            // Only an .asset file holds a PlayForge asset; a batch with none, or a Unity project with no Forge Project, is left
            var arrived = imported.Concat(moved).Where(IsAssetFile).Distinct().ToList();
            var gone = deleted.Where(IsAssetFile).ToList();
            if (arrived.Count == 0 && gone.Count == 0) return;
            var folders = ForgeProject.Folders();
            if (folders.Count == 0) return;

            var changed = new HashSet<ForgeProject>();
            foreach (var path in arrived)
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type)) continue;

                // A project imported catches up on its own assets, as a new one or a sample's does as it comes in
                if (typeof(ForgeProject).IsAssignableFrom(type))
                {
                    var project = AssetDatabase.LoadAssetAtPath<ForgeProject>(path);
                    if (project != null && project.KeepCatalogCurrent && CatchUp(project, folders) > 0) changed.Add(project);
                    continue;
                }

                var owner = ForgeProject.Owner(path, folders);
                if (owner == null || !owner.KeepCatalogCurrent) continue;
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is BaseForgeAsset forgeAsset && owner.AddByGuid(forgeAsset)) changed.Add(owner);
            }
            foreach (var path in gone)
            {
                var guid = AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.IncludeRecentlyDeletedAssets);
                if (string.IsNullOrEmpty(guid)) continue;
                foreach (var (_, project) in folders)
                    if (project.KeepCatalogCurrent && project.RemoveFile(guid) > 0) changed.Add(project);
            }

            // Each project changed is saved, which imports it again, as caught up already, so it's left as it is
            foreach (var project in changed) Save(project);
        }

        private static void Save(ForgeProject project)
        {
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssetIfDirty(project);
        }

        private static bool IsAssetFile(string path) => path != null && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
    }
}
