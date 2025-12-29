using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class BaseForgeObject : ScriptableObject, Taggable
    {
        public abstract HashSet<Tag> GetAllTags();
        //public abstract Texture2D GetPrimaryTexture();
    }

    public interface Taggable
    {
        public HashSet<Tag> GetAllTags();
    }
}
