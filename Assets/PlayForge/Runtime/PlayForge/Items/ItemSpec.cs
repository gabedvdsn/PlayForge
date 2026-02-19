using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runtime specification for an item instance.
    /// Contains the item's current state, level, and provides level information for linked effects/abilities.
    /// </summary>
    public class ItemSpec : IEffectOrigin, IValidationReady
    {
        public IGameplayAbilitySystem Owner { get; private set; }
        public Item Base { get; private set; }
        public int Level { get; private set; }
        
        public float RelativeLevel => Base.MaxLevel > 1 
            ? (Level - 1) / (float)(Base.MaxLevel - 1) 
            : 1f;

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════

        public ItemSpec(IGameplayAbilitySystem owner, Item @base, int level)
        {
            Owner = owner;
            Base = @base;
            Level = Mathf.Clamp(level, @base.StartingLevel, @base.MaxLevel);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Management
        // ═══════════════════════════════════════════════════════════════════════════

        public void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 1, Base.MaxLevel);
        }

        public bool LevelUp()
        {
            if (Level >= Base.MaxLevel) return false;
            Level++;
            return true;
        }

        public bool LevelDown()
        {
            if (Level <= 1) return false;
            Level--;
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ILevelProvider Implementation
        // ═══════════════════════════════════════════════════════════════════════════

        public int GetLevel() => Level;
        public int GetMaxLevel() => Base.MaxLevel;
        public int GetStartingLevel() => Base.StartingLevel;
        public float GetRelativeLevel() => RelativeLevel;
        public string GetName()
        {
            return Base.GetName();
        }
        public List<Tag> GetAffiliation()
        {
            return Owner.GetAffiliation();
        }
        public bool IsActive()
        {
            return false;
            //return Owner.GetItemShelf();
        }
        public string GetProviderName() => Base.GetName();
        public Tag GetProviderTag() => Base.Tags.AssetTag;

        // ═══════════════════════════════════════════════════════════════════════════
        // IAttributeImpactDerivation Implementation
        // ═══════════════════════════════════════════════════════════════════════════

        public ISource GetSource() => Owner;
        public IEffectOrigin GetEffectDerivation() => this;
        public ISource GetOwner()
        {
            return Owner;
        }
        public List<Tag> GetContextTags()
        {
            throw new System.NotImplementedException();
        }
        public Tag GetAssetTag() => Base.Tags.AssetTag;

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString()
        {
            return $"ItemSpec[{Base.GetName()} Lv.{Level}]";
        }
    }
}