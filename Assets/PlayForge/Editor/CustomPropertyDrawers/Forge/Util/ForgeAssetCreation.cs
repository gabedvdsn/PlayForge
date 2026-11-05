using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Makes a PlayForge asset where The Forge makes its type: in the folder Project Settings → PlayForge names for the
    /// type, with its file prefix and suffix, named, and with the asset tag its name gives. A reference field's New uses it
    /// (ED-38).
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
            var prefix = PlayForgeSettings.PrefixFor(type);
            var suffix = PlayForgeSettings.SuffixFor(type);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{prefix}{FileNameOf(name)}{suffix}.asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>The folder Project Settings → PlayForge names for a type, or the type's default.</summary>
        public static string FolderFor(Type type) => PlayForgeSettings.FolderFor(type);

        /// <summary>
        /// Makes a new asset of any type The Forge lists, as its Create does: a copy of the type's New Asset default when
        /// Project Settings names one, named, with the tag its name gives, in the folder given or the type's, with its file
        /// prefix and suffix. One factory makes every new asset (Q20); the copy is the default's values, not a link to it
        /// (Q22).
        /// </summary>
        public static ScriptableObject CreateNew(Type type, string name, string folder = null)
        {
            var asset = ScriptableObject.CreateInstance(type);
            var copied = PlayForgeSettings.NewAssetDefault(type);
            if (copied != null) EditorUtility.CopySerialized(copied, asset);
            NameAny(asset, name);
            folder = string.IsNullOrWhiteSpace(folder) ? FolderFor(type) : folder.Trim().TrimEnd('/');
            EnsureFolder(folder);
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{PlayForgeSettings.PrefixFor(type)}{FileNameOf(name)}{PlayForgeSettings.SuffixFor(type)}.asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>A copy of an asset beside it, named as a copy, with the tag that name gives. The Forge's Duplicate kept the name and tag.</summary>
        public static ScriptableObject Duplicate(ScriptableObject asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return null;
            var copyPath = AssetDatabase.GenerateUniqueAssetPath(path);
            if (!AssetDatabase.CopyAsset(path, copyPath)) return null;
            var copy = AssetDatabase.LoadAssetAtPath<ScriptableObject>(copyPath);
            if (copy == null) return null;
            NameAny(copy, $"{TheForge.NameOf(asset)} Copy");
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            return copy;
        }

        // Names any asset The Forge lists: a PlayForge asset as its inspector does, a Scaler asset by its scaler, and a
        // requirement template by its group
        private static void NameAny(ScriptableObject asset, string name)
        {
            switch (asset)
            {
                case BaseForgeAsset forge:
                    Name(forge, name);
                    break;
                case ScalerAsset scaler:
                    scaler.Scaler ??= ScalerFields.Make(typeof(SimpleScaler), 1f);
                    scaler.Scaler.Name = name;
                    break;
                case RequirementTemplate requirement:
                    requirement.Requirements ??= new AvoidRequireTagGroup();
                    requirement.Requirements.Name = name;
                    break;
            }
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
