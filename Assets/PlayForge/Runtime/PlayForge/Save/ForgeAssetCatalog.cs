using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Names the assets a save refers to, attributes, effects, abilities and items, by IDs that last across sessions, as a
    /// reference doesn't. A save names each by its ID here, and a load finds it by that (FEAT-9).
    ///
    /// <para>In the editor, Add Every PlayForge Asset, in the catalog's menu, names each PlayForge asset in the project by
    /// its GUID, which lasts as the asset is renamed or moved. A game can add its own as it runs.</para>
    /// <para>A <see cref="ForgeProject"/> is the catalog the Create menu makes, of the assets in its folder (FEAT-34); a
    /// catalog made before goes on naming what it names.</para>
    /// </summary>
    public class ForgeAssetCatalog : ScriptableObject, IAssetNames
    {
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
        /// catalog doesn't name yet, named by its GUID, and says how many it added. A project adds its own (FEAT-34).
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
                {
                    if (asset is not BaseForgeAsset forgeAsset) continue;
                    if (!UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(forgeAsset, out string fileGuid, out long localId)) continue;
                    string id = UnityEditor.AssetDatabase.IsMainAsset(forgeAsset) ? fileGuid : $"{fileGuid}:{localId}";
                    if (Add(id, forgeAsset)) added++;
                }
            }
            if (added > 0) UnityEditor.EditorUtility.SetDirty(this);
            return added;
        }
#endif
    }
}
