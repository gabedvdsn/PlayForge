using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A pick of assets (ED-61, EP-1): PlayForge's dropdown of the assets offered, by their names, the one held checked.
    /// Assets in more than one folder sit in a submenu for each folder under the one they share, as Fire and Ice under an
    /// Effects folder; and the dropdown's Context says an asset's description and its file. An asset's template is picked
    /// this way, from the asset menu's Set Template…, and an asset field's in the active Forge Project. It was Unity's
    /// searchable dropdown, one long list, and before that the Asset Browser, a window of its own with an accent-colored
    /// header, type badges and an Assign button on every row.
    /// </summary>
    internal sealed class ForgeAssetPicker
    {
        /// <summary>The dropdown it shows, which a test reads.</summary>
        public ForgeDropdown Dropdown { get; }

        /// <param name="assets">The assets to offer, in the order to list them, each with the name it shows; a null asset is None, first.</param>
        /// <param name="title">The dropdown's heading, as "Template".</param>
        /// <param name="none">What the dropdown says when it offers nothing.</param>
        /// <param name="picked">What a pick does.</param>
        /// <param name="current">The asset the field holds, which it checks; null for none.</param>
        public ForgeAssetPicker(IReadOnlyList<(Object Asset, string Name)> assets, string title, string none, Action<Object> picked, Object current = null)
        {
            assets ??= Array.Empty<(Object, string)>();
            Dropdown = new ForgeDropdown(title) { Empty = none };

            // A submenu for each folder, under the one they share, where they're in more than one
            var folders = assets.Where(each => each.Asset != null).Select(each => FolderOf(each.Asset)).Distinct().ToList();
            var shared = folders.Count > 1 ? Shared(folders) : null;

            foreach (var (asset, name) in assets)
            {
                var each = asset;
                if (asset == null)
                {
                    Dropdown.AddFixed(name, () => picked?.Invoke(null)).Checked = current == null;
                    continue;
                }
                var path = shared == null ? "" : Below(FolderOf(asset), shared);
                Dropdown.Add(path, name, () => picked?.Invoke(each), ContextOf(asset), isChecked: asset == current);
            }
        }

        /// <summary>Shows it under a rect of the field's panel.</summary>
        public void Show(Rect worldBound) => Dropdown.Show(worldBound);

        /// <summary>What the dropdown says of an asset: its description, where it has one, and its file.</summary>
        internal static string ContextOf(Object asset)
        {
            var file = AssetDatabase.GetAssetPath(asset);
            var description = ForgeSummaries.DescriptionOf(asset);
            if (string.IsNullOrWhiteSpace(description)) return file;
            return string.IsNullOrEmpty(file) ? description.Trim() : $"{description.Trim()}\n{file}";
        }

        private static string FolderOf(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            int slash = string.IsNullOrEmpty(path) ? -1 : path.LastIndexOf('/');
            return slash < 0 ? "" : path.Substring(0, slash);
        }

        // The deepest folder that holds every one, as "Assets/PlayForge/Arena/Effects"
        private static string Shared(IReadOnlyList<string> folders)
        {
            var shared = folders[0].Split('/').ToList();
            foreach (var folder in folders.Skip(1))
            {
                var parts = folder.Split('/');
                int same = 0;
                while (same < shared.Count && same < parts.Length && shared[same] == parts[same]) same++;
                shared.RemoveRange(same, shared.Count - same);
            }
            return string.Join("/", shared);
        }

        // A folder's path below the shared one, as "Fire" below "…/Effects"; "" for the shared one itself
        private static string Below(string folder, string shared)
        {
            if (string.IsNullOrEmpty(shared)) return folder;
            return folder.Length > shared.Length && folder.StartsWith(shared + "/", StringComparison.Ordinal) ? folder.Substring(shared.Length + 1) : "";
        }
    }
}
