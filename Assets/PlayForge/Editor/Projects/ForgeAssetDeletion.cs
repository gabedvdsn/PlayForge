using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Deletes PlayForge assets the one way The Forge and the asset browser share: it names the assets that use them,
    /// asks once, and moves them to the trash, where they can be restored. Both deleted for good, without looking.
    /// The Forge deletes its whole selection, from its menu or with the Delete key.
    /// </summary>
    internal static class ForgeAssetDeletion
    {
        // How many users the question lists before it counts the rest
        private const int ListedUsers = 8;

        // The question, as a title and a message, answered true to go ahead. Tests replace it, since a dialog would
        // block their run
        internal static Func<string, string, bool> Confirm = (title, message) =>
            EditorUtility.DisplayDialog(title, message, "Move to Trash", "Cancel");

        /// <summary>Asks, then moves the asset to the trash. Returns whether it went.</summary>
        public static bool MoveToTrash(Object asset) => MoveToTrash(new[] { asset }) == 1;

        /// <summary>
        /// Asks once, naming the assets that use any of them, then moves each asset to the trash. Returns how many went.
        /// </summary>
        public static int MoveToTrash(IReadOnlyList<Object> assets)
        {
            var paths = (assets ?? Array.Empty<Object>()).Where(asset => asset != null)
                .Select(AssetDatabase.GetAssetPath).Where(path => !string.IsNullOrEmpty(path)).Distinct().ToList();
            if (paths.Count == 0) return 0;

            // The users of any of them, but for each other: a selection that uses itself loses nothing
            var users = paths.SelectMany(FindUsers).Distinct()
                .Where(user => paths.All(path => Path.GetFileNameWithoutExtension(path) != user)).OrderBy(user => user).ToList();
            string them = paths.Count == 1 ? $"'{Path.GetFileNameWithoutExtension(paths[0])}'" : $"These {paths.Count} assets";
            string it = paths.Count == 1 ? "it" : "them";
            var message = users.Count == 0
                ? $"Move {(paths.Count == 1 ? them : $"{paths.Count} assets")} to the trash? You can restore {it} from there."
                : $"{them} {(paths.Count == 1 ? "is" : "are")} used by {users.Count} asset{(users.Count == 1 ? "" : "s")}:\n"
                  + string.Join("\n", users.Take(ListedUsers).Select(user => "• " + user))
                  + (users.Count > ListedUsers ? $"\n…and {users.Count - ListedUsers} more" : "")
                  + $"\n\nTheir references to {it} will break. Move {it} to the trash anyway? You can restore {it} from there.";
            if (!Confirm(paths.Count == 1 ? "Delete Asset" : "Delete Assets", message)) return 0;

            int gone = 0;
            foreach (var path in paths)
                if (AssetDatabase.MoveAssetToTrash(path)) gone++;
            return gone;
        }

        /// <summary>The names of the project's assets, prefabs and scenes that refer to the asset at a path.</summary>
        internal static List<string> FindUsers(string path)
        {
            return AssetDatabase.GetAllAssetPaths()
                .Where(candidate => candidate != path && candidate.StartsWith("Assets/") && CanRefer(candidate))
                .Where(candidate => AssetDatabase.GetDependencies(candidate, false).Contains(path))
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name)
                .ToList();
        }

        private static bool CanRefer(string path) =>
            path.EndsWith(".asset") || path.EndsWith(".prefab") || path.EndsWith(".unity");
    }
}
