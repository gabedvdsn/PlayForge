using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Headless player-build verification (asset-store readiness / CI):
    ///   Unity -batchmode -nographics -projectPath &lt;proj&gt;
    ///         -executeMethod FarEmerald.PlayForge.Extended.Editor.BuildVerification.BuildWindowsPlayer
    /// Exit codes: 0 = build succeeded, 1 = build failed, 2 = no scene found, 3 = exception.
    /// Proves the runtime assembly compiles and links in a real player (no editor-only leaks).
    /// </summary>
    public static class BuildVerification
    {
        public static void BuildWindowsPlayer()
        {
            try
            {
                var scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();

                if (scenes.Length == 0)
                {
                    var guid = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" }).FirstOrDefault();
                    if (!string.IsNullOrEmpty(guid))
                    {
                        scenes = new[] { AssetDatabase.GUIDToAssetPath(guid) };
                    }
                }

                if (scenes.Length == 0)
                {
                    Debug.LogError("[BuildVerification] No scene available to build.");
                    EditorApplication.Exit(2);
                    return;
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

                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildVerification] Exception: {ex}");
                EditorApplication.Exit(3);
            }
        }
    }
}
