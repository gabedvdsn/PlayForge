using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What has a name, a description and an icon to show: <c>GetName</c>, <c>GetDescription</c> and
    /// <c>GetDefaultIcon</c>. Every PlayForge asset, an attribute, a tag and every target, a GAS hub among them, is
    /// one, and an origin or a carrier gives the one behind it through <c>GetReadableDefinition</c>, as a combat log
    /// names what dealt a hit.
    /// </summary>
    public interface IHasReadableDefinition
    {
        public string GetName();
        public string GetDescription();
        public Texture2D GetDefaultIcon();
    }

    /// <summary>
    /// What holds textures under tags, as an asset's Textures: an ability, an item, an effect, an entity or a running one.
    /// An effect that inherits its texture takes its origin's under a tag.
    /// </summary>
    public interface IHasTextures
    {
        /// <summary>Its textures, each under its tag; null or empty where it has none.</summary>
        public IReadOnlyList<TextureItem> GetTextureItems();
    }
}
