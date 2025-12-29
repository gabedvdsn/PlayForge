using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class TextureItem
    {
        [ForgeTagContext(ForgeContext.Texture)]
        public Tag Tag;
        public Texture2D Texture;
    }
}