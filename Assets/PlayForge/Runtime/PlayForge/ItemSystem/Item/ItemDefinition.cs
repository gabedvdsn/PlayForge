using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An item's Definition: its <c>Name</c>, <c>Description</c> and <c>Textures</c>; its <c>Visibility</c> tag, which
    /// a game's UI can filter by and the runtime doesn't act on; and its <c>MaxStackSize</c>, how many of it one
    /// inventory slot holds, read at the item's level, 1 or less making each its own slot (<c>StackSizeOf</c>).
    /// </summary>
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
        /// own. Nothing read it, and the stack count was never set.
        /// </summary>
        [Tooltip("How many of the item one slot holds, read at the item's level. 1 or less, and each is a slot of its own")]
        [NumberMin(0f)] public ScalableInt MaxStackSize = 0;

        /// <summary>How many of the item one slot holds, read in a context: its Max Stack Size, or 1.</summary>
        public int StackSizeAt(in NumberContext context)
        {
            int size = MaxStackSize?.Evaluate(context) ?? 0;
            return size > 1 ? size : 1;
        }

        /// <summary>How many of the item a slot of it holds, at the slot's level.</summary>
        public int StackSizeOf(ItemSpec spec) => StackSizeAt(NumberContext.Of(spec));
    }
}
