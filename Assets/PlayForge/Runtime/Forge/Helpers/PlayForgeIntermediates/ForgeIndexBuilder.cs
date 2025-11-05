#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
    public static class ForgeIndexBuilder
    {
        public static FrameworkIndex BuildOrUpdateIndex(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            var proj = ForgeStores.LoadFramework(key, false);
            if (proj is null) return null;

            return BuildOrUpdateIndex(proj);
        }

        public static FrameworkIndex BuildMilestoneIndex(FrameworkProject proj, string identifier)
        {
            ForgePaths.EnsureDirPath(ForgePaths.IndexRootFolderName);
            var path = ForgePaths.IndexAssetPath($"{proj.MetaName}_Milestone_{identifier}");

            var index = AssetDatabase.LoadAssetAtPath<FrameworkIndex>(path);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<FrameworkIndex>();
                AssetDatabase.CreateAsset(index, path);
            }

            index.FrameworkKey = proj.MetaName;
            LoadFrameworkIntoIndex(proj, index);

            EditorUtility.SetDirty(index);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return index;
        }

        public static FrameworkIndex BuildOrUpdateIndex(FrameworkProject proj)
        {
            ForgePaths.EnsureDirPath(ForgePaths.IndexRootFolderName);
            var path = ForgePaths.IndexAssetPath(proj.MetaName);

            var index = AssetDatabase.LoadAssetAtPath<FrameworkIndex>(path);
            if (index == null)
            {
                index = ScriptableObject.CreateInstance<FrameworkIndex>();
                AssetDatabase.CreateAsset(index, path);
            }

            index.FrameworkKey = proj.MetaName;
            LoadFrameworkIntoIndex(proj, index);

            EditorUtility.SetDirty(index);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return index;
        }

        static void LoadFrameworkIntoIndex(FrameworkProject fp, FrameworkIndex index)
        {
            index.Attributes.Clear();
            foreach (var d in fp.Attributes) SafeAddItem(index.Attributes, d, EDataType.Attribute);
            
            index.Effects.Clear();
            foreach (var d in fp.Effects) SafeAddItem(index.Effects, d, EDataType.Effect);
            
            index.Entities.Clear();
            foreach (var d in fp.Entities) SafeAddItem(index.Entities, d, EDataType.Entity);
            
            index.Abilities.Clear();
            foreach (var d in fp.Abilities) SafeAddItem(index.Abilities, d, EDataType.Ability);
            
            index.AttributeSets.Clear();
            foreach (var d in fp.AttributeSets) SafeAddItem(index.AttributeSets, d, EDataType.AttributeSet);
            
            index.Tags.Clear();
            foreach (var d in fp.Tags) SafeAddItem(index.Tags, d, EDataType.Tag);
            
            // Settings
            index.Settings = ForgeStores.LoadLocalSettings(fp.MetaName);

            void SafeAddItem(IList<DataEntry> target, ForgeDataNode node, EDataType type)
            {
                if (!ForgeTags.IsValidComplete(node)) return;
                target.Add(new DataEntry{ Id = node.Id, Name = node.Name, Type = type });
            }
        }
        
        public static void BuildAll()
        {
            ForgePaths.EnsureDirPath(ForgePaths.IndexRootFolderName);
            var frameworksRoot = ForgePaths.FrameworksRoot; // authoring frameworks folder
            if (!Directory.Exists(frameworksRoot)) return;

            foreach (var dir in Directory.GetDirectories(frameworksRoot))
            {
                var key = Path.GetFileName(dir);
                BuildOrUpdateIndex(key);
            }
        }
    }
}
#endif