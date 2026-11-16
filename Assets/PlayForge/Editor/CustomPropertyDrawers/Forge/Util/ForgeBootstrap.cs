using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The files a project brings PlayForge up with, made by code (EP-11, Decision 89): the Bootstrapper, GameRoot and
    /// ProcessControl prefabs, in the PlayForge Folder's Bootstrap folder, as the first Forge Project is made and again where
    /// one's gone; and each Forge Project's own Game Root entity, in its GameRoot folder. PlayForge ships none of them, so
    /// a deleted prefab comes back, and every project's Game Root is its own.
    /// </summary>
    internal static class ForgeBootstrap
    {
        public const string BootstrapperName = "Bootstrapper";
        public const string GameRootName = "GameRoot";
        public const string ProcessControlName = "ProcessControl";

        /// <summary>Where a prefab of the Bootstrap folder is, as "Assets/PlayForge/Bootstrap/GameRoot.prefab".</summary>
        public static string PathOf(string prefab) => $"{ForgeFolders.Bootstrap}/{prefab}.prefab";

        /// <summary>The Bootstrapper prefab, where it's there.</summary>
        public static Bootstrapper Bootstrapper => AssetDatabase.LoadAssetAtPath<Bootstrapper>(PathOf(BootstrapperName));

        /// <summary>The prefabs that are missing, by name, none where all three are there.</summary>
        public static List<string> Missing()
        {
            var missing = new List<string>();
            foreach (var prefab in new[] { ProcessControlName, GameRootName, BootstrapperName })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(prefab)) == null) missing.Add(prefab);
            return missing;
        }

        /// <summary>
        /// Makes each Bootstrap prefab that's missing: ProcessControl's and the GameRoot's, then the Bootstrapper's, which
        /// names them and plays <paramref name="project"/> where it names none yet. Says how many it made.
        /// </summary>
        public static int EnsurePrefabs(ForgeProject project)
        {
            var missing = Missing();
            if (missing.Count == 0)
            {
                // A Bootstrapper that plays no project plays this one
                var existing = Bootstrapper;
                if (existing != null && existing.Project == null && project != null)
                {
                    existing.Project = project;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                return 0;
            }
            ForgeAssetCreation.EnsureFolder(ForgeFolders.Bootstrap);

            if (missing.Contains(ProcessControlName)) Save(ProcessControlName, made => made.AddComponent<ProcessControl>());
            if (missing.Contains(GameRootName))
                Save(GameRootName, made =>
                {
                    // Stepping in Update and LateUpdate, as a GAS must for its end of frame (GAS-1)
                    var root = made.AddComponent<GameRoot>();
                    root.ProcessLifecycle = EProcessLifecycle.Synchronous;
                    root.ProcessTiming = EProcessStepTiming.UpdateAndLate;
                });

            var processControl = AssetDatabase.LoadAssetAtPath<ProcessControl>(PathOf(ProcessControlName));
            var gameRoot = AssetDatabase.LoadAssetAtPath<GameRoot>(PathOf(GameRootName));
            if (missing.Contains(BootstrapperName))
                Save(BootstrapperName, made =>
                {
                    var bootstrapper = made.AddComponent<Bootstrapper>();
                    bootstrapper.Project = project;
                    bootstrapper.ProcessControlPrefab = processControl;
                    bootstrapper.GameRootPrefab = gameRoot;
                    bootstrapper.RegisterSets = new List<AttributeSet>();
                });
            else if (Bootstrapper is { } kept)
            {
                // A Bootstrapper kept where a prefab it named came back names the new one
                if (kept.ProcessControlPrefab == null) kept.ProcessControlPrefab = processControl;
                if (kept.GameRootPrefab == null) kept.GameRootPrefab = gameRoot;
                if (kept.Project == null) kept.Project = project;
                EditorUtility.SetDirty(kept);
                AssetDatabase.SaveAssetIfDirty(kept);
            }
            return missing.Count;
        }

        // A prefab of the Bootstrap folder, made from a GameObject its maker fills, the scene's copy gone after
        private static void Save(string prefab, System.Action<GameObject> fill)
        {
            var made = new GameObject(prefab);
            try
            {
                fill(made);
                PrefabUtility.SaveAsPrefabAsset(made, PathOf(prefab));
            }
            finally
            {
                Object.DestroyImmediate(made);
            }
        }

        /// <summary>
        /// Makes a Forge Project's Game Root entity in its GameRoot folder, as "Assets/PlayForge/Arena/GameRoot/GameRoot.asset",
        /// the game's own entity, on the System side, and gives it the project (EP-11).
        /// </summary>
        public static EntityIdentity MakeGameRootEntity(ForgeProject project)
        {
            if (project == null) return null;
            var folder = $"{project.Folder}/{GameRootName}";
            ForgeAssetCreation.EnsureFolder(folder);
            var entity = ScriptableObject.CreateInstance<EntityIdentity>();
            entity.Name = "Game Root";
            entity.Affiliation = new List<Tag> { Tags.SYSTEM };
            entity.AssetTag = Tag.Generate("Entity.GameRoot", "GameRoot");
            entity.Description = "The game's own entity. Handles all processes without an assigned handler.";
            AssetDatabase.CreateAsset(entity, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{GameRootName}.asset"));
            project.GameRootEntity = entity;
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssetIfDirty(project);
            return entity;
        }
    }
}
