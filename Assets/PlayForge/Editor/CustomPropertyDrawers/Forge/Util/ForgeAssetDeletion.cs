using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Deletes a PlayForge asset the one way The Forge and the asset browser share: it names the assets that use it,
    /// asks, and moves it to the trash, where it can be restored. Both deleted for good, without looking (ED-8).
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
        public static bool MoveToTrash(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return false;

            var users = FindUsers(path);
            var message = users.Count == 0
                ? $"Move '{asset.name}' to the trash? You can restore it from there."
                : $"'{asset.name}' is used by {users.Count} asset{(users.Count == 1 ? "" : "s")}:\n"
                  + string.Join("\n", users.Take(ListedUsers).Select(user => "• " + user))
                  + (users.Count > ListedUsers ? $"\n…and {users.Count - ListedUsers} more" : "")
                  + "\n\nTheir references to it will break. Move it to the trash anyway? You can restore it from there.";
            if (!Confirm("Delete Asset", message)) return false;

            return AssetDatabase.MoveAssetToTrash(path);
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
