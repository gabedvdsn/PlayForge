using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [System.Serializable]
    public class ItemDefinition
    {
        [Tooltip("Display name of the item.")]
        public string Name;
        
        [TextArea(2, 4)]
        [Tooltip("Description shown in UI.")]
        public string Description;
        
        [ForgeTagContext(ForgeContext.Visibility)] 
        [Tooltip("Visibility/category tag for filtering.")]
        public Tag Visibility;
        
        [Tooltip("Icons and textures for this item.")]
        public List<TextureItem> Textures = new();
        
        [Tooltip("If true, multiple instances of this item can exist in the same inventory.")]
        public bool AllowDuplicates;

        [Tooltip("Maximum stack size")]
        public int MaxStackSize;
    }
}
