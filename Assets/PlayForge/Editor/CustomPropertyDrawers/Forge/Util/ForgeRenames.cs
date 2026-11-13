using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Renames an asset as its inspector does, from The Forge or an asset's header (FN-8): a PlayForge asset's name, with
    /// the tag its name gives, as one Undo step; a Scaler asset's scaler and a Requirement template's requirements, whose
    /// names its lists show; and for an asset with no name of its own, as a Value Pipeline, its file alone. The file takes
    /// the name, with its type's prefix and suffix from Project Settings, leaving out its spaces as a new asset's file does,
    /// and follows the name back through Undo and Redo (ED-70). A file that is its asset's only name keeps the spaces, and
    /// its rename Unity doesn't undo.
    /// </summary>
    public static class ForgeRenames
    {
        // The assets renamed here, whose files follow their names through Undo and Redo (ED-70)
        private static readonly HashSet<Object> Renamed = new();

        [InitializeOnLoadMethod]
        private static void FollowUndo() => Undo.undoRedoPerformed += FilesFollowNames;

        /// <summary>Whether an asset can be renamed: a project asset, the main one of its file.</summary>
        public static bool CanRename(Object asset) =>
            asset != null && AssetDatabase.IsMainAsset(asset) && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset));

        /// <summary>An asset's name, as The Forge lists it: its own, or its file's where it has none.</summary>
        public static string NameOf(Object asset) => TheForge.NameOf(asset);

        /// <summary>
        /// Renames an asset: its own name, and the tag that name gives, where it has them, then its file. A name of only
        /// spaces, or the one it has, changes nothing.
        /// </summary>
        public static void Rename(Object asset, string name)
        {
            name = name?.Trim();
            if (!CanRename(asset) || string.IsNullOrEmpty(name) || name == NameOf(asset)) return;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Rename {ForgeAssetTypes.NameOf(asset.GetType())}");
            int group = Undo.GetCurrentGroup();
            Undo.RecordObject(asset, "Rename");
            switch (asset)
            {
                case Ability ability:
                    ability.Definition.Name = name;
                    StoreAssetTag(ability, name, "Ability", ability.Tags.AssetTag, tag =>
                    {
                        var tags = ability.Tags;
                        tags.AssetTag = tag;
                        ability.Tags = tags;
                    });
                    break;
                case GameplayEffect effect:
                    effect.Definition.Name = name;
                    StoreAssetTag(effect, name, "Effect", effect.Tags.AssetTag, tag =>
                    {
                        var tags = effect.Tags;
                        tags.AssetTag = tag;
                        effect.Tags = tags;
                    });
                    break;
                case Item item:
                    item.Definition.Name = name;
                    StoreAssetTag(item, name, "Item", item.Tags.AssetTag, tag =>
                    {
                        var tags = item.Tags;
                        tags.AssetTag = tag;
                        item.Tags = tags;
                    });
                    break;
                case EntityIdentity entity:
                    entity.Name = name;
                    StoreAssetTag(entity, name, "Entity", entity.AssetTag, tag => entity.AssetTag = tag);
                    break;
                case AttributeSet set:
                    set.Name = name;
                    StoreAssetTag(set, name, "AttributeSet", set.AssetTag, tag => set.AssetTag = tag);
                    break;
                case Attribute attribute:
                    attribute.Name = name;
                    break;
                case ScalerAsset scaler when scaler.Scaler != null:
                    scaler.Scaler.Name = name;
                    break;
                case RequirementTemplate template when template.Requirements != null:
                    template.Requirements.Name = name;
                    break;
            }
            EditorUtility.SetDirty(asset);
            if (OwnNameOf(asset) != null)
            {
                Renamed.Add(asset);
                FileFollows(asset, name);
            }
            // A file that is its asset's only name keeps the name's spaces, as Unity's own new files have them
            else FileFollows(asset, name, FileNameKeepingSpaces(name));
            Undo.CollapseUndoOperations(group);
        }

        // The name as a file has it, with only the characters a file can't have left out
        private static string FileNameKeepingSpaces(string name)
        {
            var file = (name ?? "").Trim();
            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars()) file = file.Replace(invalid.ToString(), "");
            return string.IsNullOrEmpty(file) ? "Asset" : file;
        }

        // An asset's own name, where it has a field for one; null for an asset whose file is its name
        private static string OwnNameOf(Object asset) => asset switch
        {
            Ability ability => ability.Definition?.Name ?? "",
            GameplayEffect effect => effect.Definition?.Name ?? "",
            Item item => item.Definition?.Name ?? "",
            EntityIdentity entity => entity.Name ?? "",
            AttributeSet set => set.Name ?? "",
            Attribute attribute => attribute.Name ?? "",
            ScalerAsset { Scaler: { } scaler } => scaler.Name ?? "",
            RequirementTemplate { Requirements: { } requirements } => requirements.Name ?? "",
            _ => null,
        };

        // Each asset renamed here takes its file's name from its own again, as an Undo or a Redo changes it (ED-70)
        private static void FilesFollowNames()
        {
            foreach (var each in Renamed.ToList())
            {
                if (each == null)
                {
                    Renamed.Remove(each);
                    continue;
                }
                var own = OwnNameOf(each);
                if (!string.IsNullOrWhiteSpace(own)) FileFollows(each, own);
            }
        }

        /// <summary>
        /// Stores the tag a name gives, recorded for Undo and marked modified, in the same step as the name's own change,
        /// so one Undo takes back both (ED-12). What stored the old tag stores the new one (ED-84).
        /// </summary>
        internal static void StoreAssetTag(Object asset, string assetName, string typeName, Tag stored, Action<Tag> store)
        {
            var (result, display, isUnknown) = ForgeAssetCreation.AssetTagFor(assetName, typeName);
            if (isUnknown || (stored.Name == result && stored.DisplayName == display)) return;

            Undo.RecordObject(asset, "Update Asset Tag");
            var from = stored.Name;
            stored.Name = result;
            stored.DisplayName = display;
            store(stored);
            EditorUtility.SetDirty(asset);
            ForgeTagRenames.Rename(from, result, display, asset);
        }

        /// <summary>
        /// Renames an asset's file after its name, with its type's prefix and suffix from Project Settings, so the two
        /// don't drift apart (ED-70): a character a file can't have is left out, as a new asset's file leaves it, and a name
        /// another file in its folder has takes a number, as Unity's own duplicate does. An asset not saved yet has no file.
        /// </summary>
        internal static void FileFollows(Object asset, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            FileFollows(asset, name, ForgeAssetCreation.FileNameOf(name));
        }

        private static void FileFollows(Object asset, string name, string fileName)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsMainAsset(asset) || string.IsNullOrWhiteSpace(name)) return;
            var type = asset.GetType();
            var wanted = $"{PlayForgeSettings.PrefixFor(type)}{fileName}{PlayForgeSettings.SuffixFor(type)}";
            var current = System.IO.Path.GetFileNameWithoutExtension(path);
            if (current == wanted) return;
            int slash = path.LastIndexOf('/');
            var folder = slash < 0 ? "Assets" : path.Substring(0, slash);
            var extension = System.IO.Path.GetExtension(path);
            var free = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GenerateUniqueAssetPath($"{folder}/{wanted}{extension}"));
            // The unique name of its own file, where it's the one that has the name with a number already
            if (free == current) return;
            var error = AssetDatabase.RenameAsset(path, free);
            if (!string.IsNullOrEmpty(error)) ForgeLog.Warning($"{asset.name} keeps its file's name: {error}");
        }
    }
}
