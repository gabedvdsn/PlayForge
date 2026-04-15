using UnityEngine;

namespace FarEmerald.PlayForge
{
    public interface IHasReadableDefinition
    {
        public string GetName();
        public string GetDescription();
        public Texture2D GetDefaultIcon();
    }
}
