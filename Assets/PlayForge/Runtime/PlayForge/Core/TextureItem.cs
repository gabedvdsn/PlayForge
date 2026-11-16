using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class TextureItem
    {
        [ForgeTagContext(ForgeContext.TextureSlot)]
        public Tag Tag;
        public Texture2D Texture;
    }
}