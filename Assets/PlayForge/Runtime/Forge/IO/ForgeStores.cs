#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// Thin convenience APIs for the three stores your editor needs.
    /// </summary>
    [InitializeOnLoad]
    public static class ForgeStores
    {
        public static string ActiveFrameworkKey => LoadMasterSettings().Status(ForgeTags.Settings.ACTIVE_FRAMEWORK, "");
        private const string SessionKey = "PlayForge/SessionId";
        
#if UNITY_EDITOR
        static ForgeStores()
        {
            _ = SessionId;
        }

        public static string SessionId
        {
            get
            {
                var id = SessionState.GetString(SessionKey, null);
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    SessionState.SetString(SessionKey, id);
                }

                return id;
            }
        }
#endif
        
        public static void SetActiveFramework(string key)
        {
            LoadMasterSettings().Set(ForgeTags.Settings.ACTIVE_FRAMEWORK, key);
            Debug.Log($"[ PlayForge ] Active framework set to {key}");
        }
        
        public static IEnumerable<string> IterateFrameworkKeys()
        {
            var root = ForgePaths.FrameworksRoot;
            if (!Directory.Exists(root)) yield break;

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var key = Path.GetFileName(dir);
                var expected = ForgePaths.FrameworkPath(key);
                if (File.Exists(expected))
                    yield return key;
            }
        }
        
        // MASTER settings (one file for the whole editor)
        public static ForgeJsonUtility.SettingsWrapper LoadMasterSettings()
        {
            var settings = ForgeJsonUtility.LoadSettings(ForgePaths.MasterSettingsPath);
            if (settings is not null) return settings;

            // Create master settings
            ForgeJsonUtility.SaveSettings(CreateDefaultMasterSettings(), ForgePaths.MasterSettingsPath);
            return ForgeJsonUtility.LoadSettings(ForgePaths.MasterSettingsPath);
        }
        
        public static void SaveMasterSettings(ForgeJsonUtility.SettingsWrapper settings) =>
            ForgeJsonUtility.SaveSettings(settings, ForgePaths.MasterSettingsPath);

        // Per-framework LOCAL settings (lives next to framework.fesproj.json)
        public static ForgeJsonUtility.SettingsWrapper LoadLocalSettings(string frameworkKey) =>
            ForgeJsonUtility.LoadSettings(ForgePaths.LocalSettingsPath(frameworkKey));

        public static void SaveLocalSettings(string frameworkKey, ForgeJsonUtility.SettingsWrapper settings) =>
            ForgeJsonUtility.SaveSettings(settings, ForgePaths.LocalSettingsPath(frameworkKey));

        public static void CreateNewFramework(FrameworkProject proj)
        {
            SaveFramework(proj);
            SaveFramework(proj, false);

            ForgeIndexBuilder.BuildOrUpdateIndex(proj);
            
            if (IterateFrameworkKeys().Count() == 1) SetActiveFramework(proj.MetaName);
        }
        
        // Framework data
        public static void SaveFramework(FrameworkProject proj, bool toProjectSettings = true) => ForgeJsonUtility.SaveFramework(proj, toProjectSettings);

        public static void SaveFrameworkAndSettings(FrameworkProject proj, ForgeJsonUtility.SettingsWrapper settings, bool toProjectSettings = true)
        {
            SaveFramework(proj, toProjectSettings);
            SaveLocalSettings(proj.MetaName, settings);
        }
        
        public static FrameworkProject LoadActiveFramework(bool loadFromProjectSettings = true)
        {
            return LoadFramework(ActiveFrameworkKey, loadFromProjectSettings);
        }
        
        public static FrameworkProject LoadFramework(string frameworkKey, bool loadFromProjectSettings = true) =>
            ForgeJsonUtility.LoadFramework(frameworkKey, loadFromProjectSettings);

        public static FrameworkIndex LoadIndex(string frameworkKey)
        {
            if (string.IsNullOrEmpty(frameworkKey)) return null;

            string path = ForgePaths.IndexAssetPath(frameworkKey);
            return AssetDatabase.LoadAssetAtPath<FrameworkIndex>(path);
        }
        
        #region Create

        private static ForgeJsonUtility.SettingsWrapper CreateDefaultMasterSettings()
        {
            var data = new Dictionary<Tag, object>()
            {
                { ForgeTags.Settings.SESSION_ID, "" },
                { ForgeTags.Settings.ACTIVE_FRAMEWORK, "" },
                { ForgeTags.Settings.LAST_OPENED_FRAMEWORK, "" },
                { ForgeTags.Settings.PROMPT_WHEN_INITIALIZE_EDITOR, true },
            };
            
            // Add settings

            return new ForgeJsonUtility.SettingsWrapper(data);
        }

        public static ForgeJsonUtility.SettingsWrapper CreateDefaultLocalSettings()
        {
            var data = new Dictionary<Tag, object>()
            {
                { ForgeTags.Settings.DATE_CREATED, DateTime.UtcNow.ToString(CultureInfo.InvariantCulture) },
                { ForgeTags.Settings.FW_HAS_UNSAVED_WORK, false },
                { ForgeTags.Settings.ROOT_TEMPLATE_ASSIGNMENTS, new Dictionary<EDataType, int>() },
                { ForgeTags.Settings.QUICK_TEMPLATE_ASSIGNMENTS, new Dictionary<EDataType, List<int>>() }
            };
            
            // Add settings
            data[ForgeTags.Settings.ROOT_TEMPLATE_ASSIGNMENTS] = new Dictionary<EDataType, int>()
            {
                { EDataType.Ability, -1 },
                { EDataType.Attribute, -1 },
                { EDataType.AttributeSet, -1 },
                { EDataType.Effect, -1 },
                { EDataType.Entity, -1 },
                { EDataType.Tag, -1 },
            };
            
            data[ForgeTags.Settings.QUICK_TEMPLATE_ASSIGNMENTS] = new Dictionary<EDataType, List<int>>()
            {
                { EDataType.Ability, new List<int>() },
                { EDataType.Attribute, new List<int>() },
                { EDataType.AttributeSet, new List<int>() },
                { EDataType.Effect, new List<int>() },
                { EDataType.Entity, new List<int>() },
                { EDataType.Tag, new List<int>() }
            };

            return new ForgeJsonUtility.SettingsWrapper(data);
        }
        
        #endregion
        
        #region Remove

        public static bool DeleteFramework(string key, out string msg)
        {
            #if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(key) || !IterateFrameworkKeys().Contains(key))
            {
                msg = $"No framework {Quotify(key)} found.";
                return false;
            }

            var target = ForgeJsonUtility.Slugify(key);
            var path = ForgePaths.FrameworkPath(target);
            var saPath = ForgePaths.RuntimeFrameworkPath(target);

            if (string.IsNullOrEmpty(path))
            {
                msg = $"FrameworkPath returned null/empty";
                return false;
            }

            var folder = Path.GetDirectoryName(path);
            var saFolder = Path.GetDirectoryName(saPath);
            if (string.IsNullOrEmpty(folder))
            {
                msg = "FrameworkDirectory returned null/empty";
                return false;
            }

            try
            {
                if (!Directory.Exists(folder))
                {
                    msg = "FrameworkDirectory does not exist";
                    return false;
                }

                if (!Directory.Exists(saFolder))
                {
                    msg = "StreamingAssets FrameworkDirectory does not exist";
                    return false;
                }

                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                
                Directory.Delete(folder, recursive: true);
                
                File.SetAttributes(saPath, FileAttributes.Normal);
                File.Delete(saPath);
                
                DeleteIndex(key);

                AssetDatabase.Refresh();
                msg = $"{Quotify(key)} deleted successfully";
                return true;
            }
            catch (Exception ex)
            {
                msg = $"Failed to delete framework {Quotify(key)}: {ex}";
                return false;
            }

            static void DeleteIndex(string key)
            {
                if (!Directory.Exists(ForgePaths.IndexRootFolderName)) return;

                string[] target = { ForgePaths.IndexRootFolderName };
                foreach (var asset in AssetDatabase.FindAssets(key, target))
                {
                    var path = AssetDatabase.GUIDToAssetPath(asset);
                    AssetDatabase.DeleteAsset(path);
                }
            }

#endif
        }
        
        static string Quotify(string body) => $"“{body}”";
        
        #endregion
    }
}
#endif