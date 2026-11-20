using System;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>An asset type's icon, in the type's color, which the style sheet sets.</summary>
    internal class ForgeTypeIcon : ForgeGlyph
    {
        public const string TypeIconClass = "pf-type-icon";

        public ForgeTypeIcon(Type assetType) : base(ForgeAssetTypes.GlyphOf(assetType))
        {
            AddToClassList(TypeIconClass);
            var modifier = ForgeAssetTypes.ModifierOf(assetType);
            if (modifier != null) AddToClassList($"{TypeIconClass}--{modifier}");
        }
    }

    /// <summary>
    /// Each asset type's glyph, its class modifier in the style sheet, and its name: the six asset types' own, and
    /// PlayForge's anvil for every other PlayForge asset.
    /// </summary>
    internal static class ForgeAssetTypes
    {
        public static ForgeGlyphShape GlyphOf(Type type) => Describe(type).Glyph;
        public static string ModifierOf(Type type) => Describe(type).Modifier;
        public static string NameOf(Type type) => Describe(type).Name;

        private static (ForgeGlyphShape Glyph, string Modifier, string Name) Describe(Type type)
        {
            if (type == null) return (ForgeGlyphShape.None, null, "");
            if (typeof(Ability).IsAssignableFrom(type)) return (ForgeGlyphShape.Ability, "ability", "Ability");
            if (typeof(GameplayEffect).IsAssignableFrom(type)) return (ForgeGlyphShape.Effect, "effect", "Gameplay Effect");
            if (typeof(EntityIdentity).IsAssignableFrom(type)) return (ForgeGlyphShape.Entity, "entity", "Entity");
            if (typeof(Item).IsAssignableFrom(type)) return (ForgeGlyphShape.Item, "item", "Item");
            if (typeof(AttributeSet).IsAssignableFrom(type)) return (ForgeGlyphShape.AttributeSet, "attribute-set", "Attribute Set");
            if (typeof(Attribute).IsAssignableFrom(type)) return (ForgeGlyphShape.Attribute, "attribute", "Attribute");
            // Every other PlayForge asset, as Crowd Control Rules or a Value Pipeline, takes PlayForge's anvil
            if (typeof(ScriptableObject).IsAssignableFrom(type) && type.Assembly == typeof(Tag).Assembly)
                return (ForgeGlyphShape.PlayForge, "playforge", ObjectNames.NicifyVariableName(type.Name));
            return (ForgeGlyphShape.None, null, ObjectNames.NicifyVariableName(type.Name));
        }
    }
}
