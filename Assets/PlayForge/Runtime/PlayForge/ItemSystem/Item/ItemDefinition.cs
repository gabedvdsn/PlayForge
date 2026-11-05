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

        /// <summary>
        /// How many of the item one slot holds. A give fills the item's stack first; 1 or less, and each is a slot of its
        /// own. Nothing read it, and the stack count was never set (FEAT-14, GAS-51).
        /// </summary>
        [Tooltip("How many of the item one slot holds. 1 or less, and each is a slot of its own")]
        public int MaxStackSize;

        /// <summary>How many of the item one slot holds: its Max Stack Size, or 1 (FEAT-14).</summary>
        public int StackSize => MaxStackSize > 1 ? MaxStackSize : 1;
    }
}
