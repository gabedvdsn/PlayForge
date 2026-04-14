using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [System.Serializable]
    public class ItemTags
    {
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        [Tooltip("Unique identifier tag for this item.")]
        public Tag AssetTag;
        
        [ForgeTagContext(ForgeContext.ContextIdentifier)]
        [Tooltip("Context tags for categorization and queries.")]
        public List<Tag> ContextTags = new();
        
        [ForgeTagContext(ForgeContext.Granted)]
        [Tooltip("Tags that are granted to the entity while this item is equipped.")]
        public List<Tag> PassiveGrantedTags = new();
    }
}
