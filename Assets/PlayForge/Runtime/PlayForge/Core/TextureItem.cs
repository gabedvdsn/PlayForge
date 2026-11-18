using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One of an asset's Textures: a texture under a tag of the Texture Slot context. The texture under Primary is the
    /// asset's icon, and with none there, the first texture is.
    /// </summary>
    [Serializable]
    public class TextureItem
    {
        [ForgeTagContext(ForgeContext.TextureSlot)]
        public Tag Tag;
        public Texture2D Texture;
    }
}