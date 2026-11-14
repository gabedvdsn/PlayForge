using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public interface IHasReadableDefinition
    {
        public string GetName();
        public string GetDescription();
        public Texture2D GetDefaultIcon();
    }

    /// <summary>
    /// What holds textures under tags, as an asset's Textures: an ability, an item, an effect, an entity or a running one.
    /// An effect that inherits its texture takes its origin's under a tag (FN-25).
    /// </summary>
    public interface IHasTextures
    {
        /// <summary>Its textures, each under its tag; null or empty where it has none.</summary>
        public IReadOnlyList<TextureItem> GetTextureItems();
    }
}
