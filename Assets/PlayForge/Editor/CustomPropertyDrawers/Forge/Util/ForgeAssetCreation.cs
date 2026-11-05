using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Makes a PlayForge asset where The Forge makes its type: in the folder The Forge's settings name for the type, with
    /// its file prefix and suffix, named, and with the asset tag its name gives. A reference field's New uses it (ED-38).
    /// </summary>
    internal static class ForgeAssetCreation
    {
        /// <summary>Makes an asset of a type, named, where The Forge makes that type.</summary>
        public static T Create<T>(string name) where T : BaseForgeAsset => (T)Create(typeof(T), name);

        /// <summary>Makes an asset of a type, named, where The Forge makes that type.</summary>
        public static BaseForgeAsset Create(Type type, string name) => Save((BaseForgeAsset)ScriptableObject.CreateInstance(type), name);

        /// <summary>
        /// Saves an asset not yet in the project, such as a copy of a template, where The Forge makes its type: named, with
        /// the tag its name gives, and its file named with The Forge's prefix and suffix.
        /// </summary>
        public static T Save<T>(T asset, string name) where T : BaseForgeAsset
        {
            var type = asset.GetType();
            Name(asset, name);
            var folder = FolderFor(type);
            EnsureFolder(folder);
            var prefix = EditorPrefs.GetString(TheForge.PREFS_PREFIX + "Prefix_" + type.Name, "");
            var suffix = EditorPrefs.GetString(TheForge.PREFS_PREFIX + "Postfix_" + type.Name, "");
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}{FileNameOf(name)}{suffix}.asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>The folder The Forge's settings name for a type, or the type's default.</summary>
        public static string FolderFor(Type type)
        {
            var key = TheForge.PREFS_PREFIX + "Path_" + type.Name;
            if (EditorPrefs.HasKey(key)) return EditorPrefs.GetString(key).TrimEnd('/');
            return TheForge.DefaultPaths.TryGetValue(type, out var path) ? path.TrimEnd('/') : "Assets/Data";
        }

        /// <summary>
        /// The asset tag a name gives: its words, without punctuation, capitalised and joined, after the type's name, as
        /// in "Item.IronSword". A name with no words gives none.
        /// </summary>
        public static (string result, string display, bool isUnknown) AssetTagFor(string assetName, string typeName)
        {
            const string unnamed = "Unnamed";
            if (string.IsNullOrEmpty(assetName)) return ($"{unnamed} {typeName}!", unnamed, true);

            // Only letters, digits and spaces, then each word capitalised and joined (PascalCase)
            var cleaned = Regex.Replace(assetName, @"[^a-zA-Z0-9\s]", "");
            var words = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpper(word[0]) + (word.Length > 1 ? word.Substring(1) : ""))
                .ToArray();

            var result = $"{typeName}.{string.Join("", words)}";
            return string.IsNullOrEmpty(result) ? ($"{unnamed} {typeName}!", unnamed, true) : (result, string.Join("", words), false);
        }

        // Names an asset and stores the tag its name gives, as its inspector does on a rename (ED-12)
        private static void Name(BaseForgeAsset asset, string name)
        {
            switch (asset)
            {
                case Ability ability:
                {
                    ability.Definition.Name = name;
                    var tags = ability.Tags;
                    tags.AssetTag = TagFor(name, "Ability");
                    ability.Tags = tags;
                    break;
                }
                case GameplayEffect effect:
                {
                    effect.Definition.Name = name;
                    var tags = effect.Tags;
                    tags.AssetTag = TagFor(name, "Effect");
                    effect.Tags = tags;
                    break;
                }
                case Item item:
                {
                    item.Definition.Name = name;
                    var tags = item.Tags;
                    tags.AssetTag = TagFor(name, "Item");
                    item.Tags = tags;
                    break;
                }
                case EntityIdentity entity:
                    entity.Name = name;
                    entity.AssetTag = TagFor(name, "Entity");
                    break;
                case AttributeSet set:
                    set.Name = name;
                    set.AssetTag = TagFor(name, "AttributeSet");
                    break;
                case Attribute attribute:
                    attribute.Name = name;
                    break;
            }
        }

        private static Tag TagFor(string name, string typeName)
        {
            var (result, display, isUnknown) = AssetTagFor(name, typeName);
            var tag = default(Tag);
            if (isUnknown) return tag;
            tag.Name = result;
            tag.DisplayName = display;
            return tag;
        }

        // A file name from a display name: its letters and digits, the spaces dropped, as The Forge's Create does
        internal static string FileNameOf(string name)
        {
            var file = Regex.Replace(name ?? "", @"\s+", "");
            foreach (var invalid in Path.GetInvalidFileNameChars()) file = file.Replace(invalid.ToString(), "");
            return string.IsNullOrEmpty(file) ? "Asset" : file;
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
