using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab.Editor
{
    /// <summary>
    /// Window → PlayForge → Creator Lab (LAB-1): the lab in a new scene, played. The scenes open first are saved if the
    /// author wants. In the editor the lab lists every asset of the project, and opens one in The Forge.
    /// </summary>
    public static class CreatorLabMenu
    {
        public const string MenuPath = "Window/PlayForge/Creator Lab";

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            CreatorLab.DefaultLibrary ??= () => new EditorLabLibrary();
            CreatorLab.OpenInForge ??= TheForge.OpenToAsset;
        }

        [MenuItem(MenuPath, false, 20)]
        private static void OpenFromMenu() => Open();

        /// <summary>
        /// Opens the lab: in a new scene, with the scenes open now saved if the author wants, and plays it where
        /// <paramref name="play"/> asks; while playing, in the scene playing. False where the author keeps their scene.
        /// </summary>
        public static bool Open(bool play = true)
        {
            if (EditorApplication.isPlaying)
            {
                if (CreatorLab.Current == null) CreatorLab.Create();
                return true;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Creator Lab").AddComponent<CreatorLab>();
            if (play) EditorApplication.EnterPlaymode();
            return true;
        }
    }

    /// <summary>The library in the editor: every PlayForge asset of the project's own folders, by kind (LAB-5).</summary>
    public sealed class EditorLabLibrary : ILabLibrary
    {
        private readonly Dictionary<LabAssetKind, List<ScriptableObject>> byKind = new();
        private readonly string[] folders;

        public string Name => "Every asset";

        /// <param name="folders">The folders it searches, or the project's Assets where none are given.</param>
        public EditorLabLibrary(params string[] folders)
        {
            this.folders = folders is { Length: > 0 } ? folders : new[] { "Assets" };
            Refresh();
        }

        public IReadOnlyList<ScriptableObject> Assets(LabAssetKind kind) =>
            byKind.TryGetValue(kind, out var list) ? list : (IReadOnlyList<ScriptableObject>)Array.Empty<ScriptableObject>();

        public void Refresh()
        {
            byKind.Clear();
            foreach (LabAssetKind kind in Enum.GetValues(typeof(LabAssetKind)))
            {
                var type = LabAssets.TypeOf(kind);
                byKind[kind] = AssetDatabase.FindAssets($"t:{type.Name}", folders)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct()
                    .Select(path => AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject)
                    .Where(asset => asset && LabAssets.TryKindOf(asset, out var found) && found == kind)
                    .OrderBy(asset => LabAssets.NameOf(asset), StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }
}
