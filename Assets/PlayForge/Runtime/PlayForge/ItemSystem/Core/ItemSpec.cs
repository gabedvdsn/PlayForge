using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runtime specification for an item instance.
    /// Contains the item's current state, level, and provides level information for linked effects/abilities.
    /// </summary>
    public class ItemSpec : BasicEffectOrigin
    { 
        public Item Base { get; private set; }

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════

        public ItemSpec(ISource owner, Item item, int level) : base(owner, new AttributeValueClamped(level, item.MaxLevel), item.Tags.AssetTag)
        {
            Base = item;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ILevelProvider Implementation
        // ═══════════════════════════════════════════════════════════════════════════

        public override int GetLevel() => GetLeveler().Level.CurrentValue;
        public override float GetRelativeLevel() => GetLeveler().Level.Ratio;
        
        public override List<Tag> GetAffiliation()
        {
            return Source.GetAffiliation();
        }
        public override bool IsActive()
        {
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // IAttributeImpactDerivation Implementation
        // ═══════════════════════════════════════════════════════════════════════════

        public ISource GetSource() => Source;
        public IEffectOrigin GetEffectDerivation() => this;
        public override ISource GetOwner()
        {
            return Source;
        }
        public override IHasReadableDefinition GetReadableDefinition()
        {
            return Base;
        }
        public override List<Tag> GetContextTags()
        {
            return Base.Tags.ContextTags;
        }
        public override Tag GetAssetTag() => Base.Tags.AssetTag;

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString()
        {
            return $"ItemSpec[{Base.GetName()} Lv.{GetLevel()}]";
        }
    }
}