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
        [Tooltip("How many of the item one slot holds, read at the item's level. 1 or less, and each is a slot of its own")]
        [NumberMin(0f)] public ScalableInt MaxStackSize = 0;

        /// <summary>How many of the item one slot holds, read in a context: its Max Stack Size, or 1 (FEAT-14, FN-19).</summary>
        public int StackSizeAt(in NumberContext context)
        {
            int size = MaxStackSize?.Evaluate(context) ?? 0;
            return size > 1 ? size : 1;
        }

        /// <summary>How many of the item a slot of it holds, at the slot's level (FN-19).</summary>
        public int StackSizeOf(ItemSpec spec) => StackSizeAt(NumberContext.Of(spec));
    }
}
