using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
    public static class ForgePaths
    {
        private const string RootFolderName = "PlayForgeEditor";
        private const string MasterSettingsFolderName = "MasterSettings";
        private const string FrameworksFolderName = "Frameworks";
        private const string MasterSettingsFileName = "forge.mastersettings.json";
        private const string FrameworkFileName = "_.forge." + FrameworkKey + ".json";
        private const string TemplateFileName = "_.forge." + TemplateKey + ".json";
        private const string LocalSettingsFileName = "_.forge." + SettingsKey + ".json";

        private const string FrameworkKey = "framework";
        private const string TemplateKey = "templates";
        private const string SettingsKey = "settings";

        static string ProjectRoot => Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "ProjectSettings");
        static string PlayForgeRoot => EnsureDir(Path.Combine(ProjectRoot, RootFolderName));

        public static string MasterSettingsRoot => EnsureDir(Path.Combine(PlayForgeRoot, MasterSettingsFolderName));
        public static string FrameworksRoot => EnsureDir(Path.Combine(PlayForgeRoot, FrameworksFolderName));

        public static string MasterSettingsPath => Path.Combine(MasterSettingsRoot, MasterSettingsFileName);

        public static string FrameworkFolder(string frameworkKey)
        {
            return EnsureDir(Path.Combine(FrameworksRoot, Safe(frameworkKey)));
        }

        public static string FrameworkPath(string frameworkKey) =>
            Path.Combine(FrameworkFolder(frameworkKey), FileName(frameworkKey, FrameworkFileName));

        public static string TemplatePath(string frameworkKey) =>
            Path.Combine(FrameworkFolder(frameworkKey), FileName(frameworkKey, TemplateFileName));
        
        public static string LocalSettingsPath(string frameworkKey) =>
            Path.Combine(FrameworkFolder(frameworkKey), FileName(frameworkKey, LocalSettingsFileName));
        
        public static string FileName(string name, string src)
        {
            return Safe(src.Replace("_", name));
        }
        
        static string EnsureDir(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static void EnsureDirPath(string assetPath)
        {
            #if UNITY_EDITOR
            var parts = assetPath.Split('/');
            string cur = parts[0];
            if (!AssetDatabase.IsValidFolder(cur))
                AssetDatabase.CreateFolder("Assets", cur.Substring("Assets/".Length));

            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
            #endif
        }

        static string Safe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Unnamed";
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return safe.Trim();
        }

        public static void RefreshAssets()
        {
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
        
        // ----------------- STREAMING ASSETS ---------------------

        private const string RuntimeRootFolderName = "PlayForgeEditor";
        private const string RuntimeFrameworksFolderName = "Frameworks";

        private static string RuntimeProjectRoot => Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "StreamingAssets");
        private static string RuntimeFrameworksRoot => EnsureDir(Path.Combine(RuntimeProjectRoot, RuntimeRootFolderName, RuntimeFrameworksFolderName));

        public static string RuntimeFrameworkPath(string key)
        {
            return Path.Combine(RuntimeFrameworksRoot, $"{key}.json");
        }
        
        // ------------------ FRAMEWORK INDEX -----------------------

        public const string IndexRootFolderName = "Assets/PlayForge/Runtime/PlayForge/FrameworkIndices";

        public static string IndexAssetPath(string key)
        {
            var safe = ForgeJsonUtility.Slugify(key);
            return $"{IndexRootFolderName}/{safe}_Index.asset";
        }
    }

}