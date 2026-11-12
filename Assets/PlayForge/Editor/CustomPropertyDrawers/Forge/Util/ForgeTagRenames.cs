using System.Collections.Generic;
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

        // Whether an asset but the one renamed has the old name as its own tag
        private static bool AnotherOwns(string name, List<Object> assets, Object except)
        {
            foreach (var asset in assets)
            {
                if (asset == null || asset == except) continue;
                var own = asset switch
                {
                    Ability ability => ability.Tags.AssetTag,
                    GameplayEffect effect => effect.Tags.AssetTag,
                    Item item => item.Tags.AssetTag,
                    EntityIdentity entity => entity.AssetTag,
                    AttributeSet set => set.AssetTag,
                    _ => default,
                };
                if (own.Name == name) return true;
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
