using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Headless player-build verification (asset-store readiness / CI):
    ///   Unity -batchmode -nographics -projectPath &lt;proj&gt;
    ///         -executeMethod FarEmerald.PlayForge.Extended.Editor.BuildVerification.BuildWindowsPlayer
    /// Exit codes: 0 = build succeeded, 1 = build failed, 3 = exception.
    /// Proves the runtime assembly compiles and links in a real player (no editor-only leaks).
    /// </summary>
    public static class BuildVerification
    {
        private const string MadeScenePath = "Assets/PlayForgeBuildVerification.unity";

        public static void BuildWindowsPlayer()
        {
            int code;
            string madeScene = null;
            try
            {
                var scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();

                // With none enabled, an empty scene made for the purpose: the build proves the runtime compiles and
                // links, which needs no content. It built whichever scene the AssetDatabase listed first (SP-12)
                if (scenes.Length == 0)
                {
                    madeScene = MadeScenePath;
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, madeScene);
                    scenes = new[] { madeScene };
                }

                Debug.Log($"[BuildVerification] Building StandaloneWindows64 with scene: {scenes[0]}");

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = "Build/Verification/PlayForgeVerification.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });

                var summary = report.summary;
                Debug.Log($"[BuildVerification] Result: {summary.result} — {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s");

                code = summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildVerification] Exception: {ex}");
                code = 3;
            }

            // Before exiting, as Exit ends the process at once
            if (madeScene != null) AssetDatabase.DeleteAsset(madeScene);
            EditorApplication.Exit(code);
        }
    }
}
