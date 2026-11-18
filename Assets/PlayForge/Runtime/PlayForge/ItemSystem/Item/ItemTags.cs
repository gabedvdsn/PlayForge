using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An item's Tags: its <c>AssetTag</c>, made from its Name, which its holder carries while the item is in its
    /// inventory, worn or not, and keeps the item's level under; its <c>ContextTags</c>, which the changes its effects
    /// make carry as context; and its Granted Tags (<c>PassiveGrantedTags</c>), on its holder while it's equipped.
    /// </summary>
    [System.Serializable]
    public class ItemTags
    {
        [ForgeTagContext(ForgeContext.AssetTag)]
        [Tooltip("Unique identifier tag for this item.")]
        public Tag AssetTag;
        
        [ForgeTagContext(ForgeContext.ContextTag)]
        [Tooltip("Context tags for categorization and queries.")]
        public List<Tag> ContextTags = new();
        
        [ForgeTagContext(ForgeContext.Granted)]
        [Tooltip("Tags that are granted to the entity while this item is equipped.")]
        public List<Tag> PassiveGrantedTags = new();
    }
}
