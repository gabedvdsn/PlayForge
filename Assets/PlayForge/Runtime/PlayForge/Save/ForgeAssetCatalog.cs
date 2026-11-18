using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Names the assets a save refers to, attributes, effects, abilities and items, by IDs that last across sessions, as a
    /// reference doesn't. A save names each by its ID here, and a load finds it by that.
    ///
    /// <para>In the editor, Add Every PlayForge Asset, in the catalog's menu, names each PlayForge asset in the project by
    /// its GUID, which lasts as the asset is renamed or moved. A game can add its own as it runs.</para>
    /// <para>A <see cref="ForgeProject"/> is the catalog the Create menu makes, of the assets in its folder, which
    /// it keeps current as they're made, moved in and deleted; a catalog made before goes on naming what it names.</para>
    /// </summary>
    public class ForgeAssetCatalog : ScriptableObject, IAssetNames
    {
        /// <summary>
        /// One row of an Asset Catalog: the <c>Id</c> a save names an asset by, and the <c>Asset</c>. Where an ID or an
        /// asset repeats, the first row holds.
        /// </summary>
        [Serializable]
        public struct Entry
        {
            public string Id;
            public ScriptableObject Asset;
        }

        [SerializeField] private List<Entry> entries = new();

        // Built from the entries as first asked, and again after an edit
        [NonSerialized] private Dictionary<string, ScriptableObject> byId;
        [NonSerialized] private Dictionary<ScriptableObject, string> idOf;

        public IReadOnlyList<Entry> Entries => entries;

        string IAssetNames.Label => $"the asset catalog {name}";

        UnityEngine.Object IAssetNames.Context => this;

        public int Count => entries.Count;

        /// <summary>Names an asset by an ID. False where either is missing, or taken already.</summary>
        public bool Add(string id, ScriptableObject asset)
        {
            if (string.IsNullOrEmpty(id) || asset == null) return false;
            Build();
            if (byId.ContainsKey(id) || idOf.ContainsKey(asset)) return false;

            entries.Add(new Entry { Id = id, Asset = asset });
            byId[id] = asset;
            idOf[asset] = id;
            return true;
        }

        /// <summary>The asset an ID names, where it's of the type asked for.</summary>
        public bool TryFind<T>(string id, out T asset) where T : ScriptableObject
        {
            asset = null;
            if (string.IsNullOrEmpty(id)) return false;
            Build();
            if (!byId.TryGetValue(id, out var found)) return false;
            asset = found as T;
            return asset != null;
        }

        /// <summary>The ID an asset is named by.</summary>
        public bool TryGetId(ScriptableObject asset, out string id)
        {
            id = null;
            if (asset == null) return false;
            Build();
            return idOf.TryGetValue(asset, out id);
        }

        private void Build()
        {
            if (byId != null) return;
            byId = new Dictionary<string, ScriptableObject>(entries.Count, StringComparer.Ordinal);
            idOf = new Dictionary<ScriptableObject, string>(entries.Count);
            foreach (var entry in entries)
            {
                // The first of a repeated ID or asset holds, as Add keeps it
                if (string.IsNullOrEmpty(entry.Id) || entry.Asset == null) continue;
                if (byId.ContainsKey(entry.Id) || idOf.ContainsKey(entry.Asset)) continue;
                byId[entry.Id] = entry.Asset;
                idOf[entry.Asset] = entry.Id;
            }
        }

        // An edit in the inspector, or a load, builds the maps again as next asked
        private void OnValidate() => Forget();

        private void OnEnable() => Forget();

        private void Forget()
        {
            byId = null;
            idOf = null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Adds each PlayForge asset in the project the catalog doesn't name yet, named by its GUID, and how many it added.
        /// A sub-asset's ID adds its local file ID to its file's GUID.
        /// </summary>
        [ContextMenu("Add Every PlayForge Asset")]
        public int AddProjectAssets() => AddAssets(null);

        /// <summary>
        /// Adds each PlayForge asset at a path <paramref name="within"/> takes, or each in the project for none, that the
        /// catalog doesn't name yet, named by its GUID, and says how many it added. A project adds its own.
        /// </summary>
        protected int AddAssets(Func<string, bool> within)
        {
            UnityEditor.Undo.RecordObject(this, "Add PlayForge Assets");
            int added = 0;
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (within != null && !within(path)) continue;
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is BaseForgeAsset forgeAsset && AddByGuid(forgeAsset)) added++;
            }
            if (added > 0) UnityEditor.EditorUtility.SetDirty(this);
            return added;
        }

        /// <summary>
        /// Names a PlayForge asset by its GUID, a sub-asset by its file's GUID and its local file ID, as Add Every PlayForge
        /// Asset names each. False where it's in no asset file, or where its ID or the asset is named already: an ID
        /// any entry holds is taken, though that entry's asset hasn't loaded yet, as on a fresh import.
        /// </summary>
        public bool AddByGuid(BaseForgeAsset asset)
        {
            if (asset == null || !UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string fileGuid, out long localId)) return false;
            if (string.IsNullOrEmpty(fileGuid)) return false;
            string id = UnityEditor.AssetDatabase.IsMainAsset(asset) ? fileGuid : $"{fileGuid}:{localId}";
            if (entries.Exists(entry => entry.Id == id)) return false;
            return Add(id, asset);
        }

        /// <summary>
        /// Lets go of each entry naming an asset in the file of <paramref name="fileGuid"/>, its main asset's and its
        /// sub-assets', as the file is deleted, and says how many.
        /// </summary>
        public int RemoveFile(string fileGuid)
        {
            if (string.IsNullOrEmpty(fileGuid)) return 0;
            int removed = entries.RemoveAll(entry => entry.Id == fileGuid
                                                     || entry.Id != null && entry.Id.StartsWith(fileGuid + ":", StringComparison.Ordinal));
            if (removed > 0) Forget();
            return removed;
        }
#endif
    }
}
