using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Carries an asset's new tag to every asset that stored its old one (ED-84, the user's report of 2026-11-11). A tag is
    /// stored by its name, so whatever named an asset by its tag, as Asset Part's Tagged, a requirement's tag query,
    /// Removes or Blocks Effects Tagged, or a trigger, kept the old name when the Identity section's Update or the
    /// Problems window's fix changed the asset's tag, and found the asset no more: an attack that read Fury Swipes's stacks
    /// read none. The tag goes on in every PlayForge asset that names the old one, with Undo, unless another asset still
    /// has the old tag as its own, as the asset it was duplicated from does: that one's tag, and what names it, stay.
    /// </summary>
    internal static class ForgeTagRenames
    {
        // The assets that can store a tag, by their type: the five that have one of their own, attributes, and the assets
        // their numbers and calculations live in
        private static readonly string[] Types =
        {
            "t:Ability", "t:GameplayEffect", "t:Item", "t:EntityIdentity", "t:AttributeSet", "t:Attribute", "t:ScalerAsset",
            "t:ValuePipeline", "t:RequirementTemplate",
        };

        /// <summary>
        /// Renames each stored tag named <paramref name="from"/> to <paramref name="toName"/> and
        /// <paramref name="toDisplay"/>, in every PlayForge asset but <paramref name="except"/>, with Undo; how many it
        /// renamed. None where another asset still has <paramref name="from"/> as its own tag.
        /// </summary>
        public static int Rename(string from, string toName, string toDisplay, Object except)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(toName) || from == toName) return 0;
            var assets = Assets();
            if (AnotherOwns(from, assets, except)) return 0;

            int renamed = 0;
            foreach (var asset in assets)
            {
                if (asset == null || asset == except) continue;
                var serialized = new SerializedObject(asset);
                var property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.Generic || property.type != nameof(Tag)) continue;
                    var name = property.FindPropertyRelative(nameof(Tag.Name));
                    if (name == null || name.stringValue != from) continue;
                    name.stringValue = toName;
                    var display = property.FindPropertyRelative(nameof(Tag.DisplayName));
                    if (display != null) display.stringValue = toDisplay;
                    changed = true;
                    renamed++;
                }
                if (!changed) continue;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
            return renamed;
        }

        /// <summary>
        /// Why a tag can't be renamed to a path, or null where it can (FN-9): a path the registry can't take, one of
        /// PlayForge's own tags' text (TAG-2), a path another tag has; or the tag, or one under it, is an asset's own tag,
        /// which follows its asset's name, or one of PlayForge's own, which code writes.
        /// </summary>
        public static string WhyNotRename(Tag tag, string path)
        {
            if (!tag.IsValid) return "Choose a tag to rename.";
            if (string.IsNullOrWhiteSpace(path)) return "A tag needs a path.";
            if (!TagRegistry.IsValidPath(path)) return "A path is names of letters, numbers and underscores, joined by dots.";
            if (path == tag.Path) return "That is its path already.";
            if (ForgeNewTagWindow.IsCoreText(path, out var core)) return $"{core} is one of PlayForge's own tags' text.";
            if (ForgeTagRegistry.GetAllTags().Any(each => each.Path == path)) return $"A tag of the path {path} is there already.";
            var assets = Assets();
            foreach (var each in Renamed(tag))
            {
                if (ForgeNewTagWindow.IsCoreName(each.Name)) return $"{each} is one of PlayForge's own tags, which code writes.";
                var owner = OwnerOf(each.Name, assets);
                if (owner != null) return $"{each} is {owner.name}'s own tag, which follows its name: rename the asset.";
            }
            return null;
        }

        /// <summary>
        /// Renames a tag, and the tags under it, to a new path, as one Undo step (FN-9): in every PlayForge asset that names
        /// one, as an asset's new tag is carried (ED-84), and in the Tag Library entry that declares it. A unique tag stays
        /// unique, its name the new path's; a plain tag stays plain. The tag it became, or the tag itself where it can't be
        /// renamed, as <see cref="WhyNotRename"/> says.
        /// </summary>
        public static Tag RenameTag(Tag tag, string path)
        {
            path = path?.Trim();
            if (WhyNotRename(tag, path) != null) return tag;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Rename the Tag {tag}");
            int group = Undo.GetCurrentGroup();
            var old = tag.Path;
            Tag became = tag;
            // The tag first, then those under it, each a tag of its kind at its new path
            foreach (var each in Renamed(tag).ToList())
            {
                var newPath = path + each.Path.Substring(old.Length);
                var made = each.Path != each.Name ? Tag.GenerateAsUnique(newPath) : Tag.Generate(newPath);
                Rename(each.Name, made.Name, made.ToString(), null);
                RenameInLibraries(each.Name, made);
                if (each == tag) became = made;
            }
            Undo.CollapseUndoOperations(group);
            ForgeTagRegistry.MarkDirty();
            return became;
        }

        // The tag and the tags under its path that the registry knows, it first
        private static IEnumerable<Tag> Renamed(Tag tag)
        {
            yield return tag;
            foreach (var each in ForgeTagRegistry.GetAllTags().Where(each => each.IsChildOf(tag)).OrderBy(each => each.Path.Length))
                yield return each;
        }

        // Each Tag Library entry that names a tag names its new one, with Undo
        private static void RenameInLibraries(string from, Tag to)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(ForgeTagLibrary)}"))
            {
                var library = AssetDatabase.LoadAssetAtPath<ForgeTagLibrary>(AssetDatabase.GUIDToAssetPath(guid));
                if (library?.Entries == null) continue;
                var entries = library.Entries.Where(entry => entry != null && entry.Name?.Trim() == from).ToList();
                if (entries.Count == 0) continue;
                Undo.RecordObject(library, "Rename a Tag");
                foreach (var entry in entries)
                {
                    entry.Name = to.Name;
                    entry.DisplayName = to.ToString();
                }
                EditorUtility.SetDirty(library);
            }
        }

        // The asset whose own tag a name is, or null
        private static Object OwnerOf(string name, List<Object> assets)
        {
            foreach (var asset in assets)
                if (asset != null && OwnOf(asset).Name == name) return asset;
            return null;
        }

        // An asset's own tag, or none
        private static Tag OwnOf(Object asset) => asset switch
        {
            Ability ability => ability.Tags.AssetTag,
            GameplayEffect effect => effect.Tags.AssetTag,
            Item item => item.Tags.AssetTag,
            EntityIdentity entity => entity.AssetTag,
            AttributeSet set => set.AssetTag,
            _ => default,
        };

        // Whether an asset but the one renamed has the old name as its own tag
        private static bool AnotherOwns(string name, List<Object> assets, Object except)
        {
            foreach (var asset in assets)
            {
                if (asset == null || asset == except) continue;
                if (OwnOf(asset).Name == name) return true;
            }
            return false;
        }

        private static List<Object> Assets()
        {
            var assets = new List<Object>();
            var seen = new HashSet<string>();
            foreach (var type in Types)
            {
                foreach (var guid in AssetDatabase.FindAssets(type))
                {
                    if (!seen.Add(guid)) continue;
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset != null && !(asset is UnityEditor.DefaultAsset)) assets.Add(asset);
                }
            }
            return assets;
        }
    }
}
