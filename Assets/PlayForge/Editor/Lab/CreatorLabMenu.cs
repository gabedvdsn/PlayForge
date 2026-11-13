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

    /// <summary>
    /// The library in the editor, by kind (LAB-5, LAB-18): the active Forge Project's assets, as The Forge's pickers offer
    /// them, or every asset of the project's own folders while it has no Forge Project; it switches the active project, as
    /// The Forge does, and follows as The Forge switches it. Given folders, it plays those folders' assets alone.
    /// </summary>
    public sealed class EditorLabLibrary : ILabLibrary
    {
        private readonly Dictionary<LabAssetKind, List<ScriptableObject>> byKind = new();
        private readonly string[] folders;

        /// <summary>The active project's name, "Assets in no project" where none is active, or "Every asset".</summary>
        public string Name
        {
            get
            {
                if (folders != null || ForgeProject.Folders().Count == 0) return "Every asset";
                var active = ForgeProjects.Active;
                return active ? active.name : "Assets in no project";
            }
        }

        /// <param name="folders">The folders it plays alone; with none, the active Forge Project's assets.</param>
        public EditorLabLibrary(params string[] folders)
        {
            this.folders = folders is { Length: > 0 } ? folders : null;
            Refresh();
        }

        public IReadOnlyList<ScriptableObject> Assets(LabAssetKind kind) =>
            byKind.TryGetValue(kind, out var list) ? list : (IReadOnlyList<ScriptableObject>)Array.Empty<ScriptableObject>();

        public IReadOnlyList<string> Projects =>
            folders != null ? Array.Empty<string>() : ForgeProjects.All().Select(project => project.name).ToList();

        public int Project => folders != null ? -1 : ForgeProjects.All().IndexOf(ForgeProjects.Active);

        public void UseProject(int index)
        {
            if (folders != null) return;
            var all = ForgeProjects.All();
            ForgeProjects.Active = index >= 0 && index < all.Count ? all[index] : null;
            Refresh();
        }

        public void Refresh()
        {
            byKind.Clear();
            // The project's own folders, the packages' left out; of them, the active Forge Project's
            var inActive = folders == null ? ForgeProjects.InActive() : null;
            var searched = folders ?? new[] { "Assets" };
            foreach (LabAssetKind kind in Enum.GetValues(typeof(LabAssetKind)))
            {
                var type = LabAssets.TypeOf(kind);
                byKind[kind] = AssetDatabase.FindAssets($"t:{type.Name}", searched)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Distinct()
                    .Select(path => AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject)
                    .Where(asset => asset && LabAssets.TryKindOf(asset, out var found) && found == kind && (inActive == null || inActive(asset)))
                    .OrderBy(asset => LabAssets.NameOf(asset), StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }
}
