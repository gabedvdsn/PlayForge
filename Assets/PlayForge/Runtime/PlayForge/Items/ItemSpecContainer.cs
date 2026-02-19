using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Container that manages an item instance's lifecycle.
    /// Tracks equipped state, applied effects, abilities, and granted tags.
    /// Handles equip/unequip operations with proper cleanup.
    /// </summary>
    public class ItemSpecContainer
    {
        public ItemSpec Spec { get; private set; }
        public bool IsEquipped { get; private set; }
        public int ItemStacks { get; private set; }

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════

        public ItemSpecContainer(ItemSpec spec)
        {
            Spec = spec;
            IsEquipped = false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Equip / Unequip
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Equips the item, applying all granted effects, abilities, and tags.
        /// </summary>
        public bool Equip()
        {
            if (IsEquipped) return false;
            
            IsEquipped = true;
            
            // Apply granted effects (must come before ability so effects are active)
            ApplyGrantedEffects();
            
            // Apply active ability
            ApplyActiveAbility();
            
            // Provide workers
            Spec.Base.WorkerGroup?.ProvideWorkersTo(Spec.Owner);
            
            // Recompile tags (effects already added their tags, but this ensures consistency)
            Spec.Owner.CompileGrantedTags();
            
            return true;
        }

        /// <summary>
        /// Unequips the item, removing all granted effects, abilities, and tags.
        /// </summary>
        public bool Unequip()
        {
            if (!IsEquipped) return false;
            
            // Remove active ability first
            RemoveActiveAbility();
            
            // Remove granted effects
            RemoveGrantedEffects();
            
            // Remove workers
            Spec.Base.WorkerGroup?.RemoveWorkersFrom(Spec.Owner);
            
            // Recompile tags
            Spec.Owner.CompileGrantedTags();
            
            IsEquipped = false;
            
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effect Application
        // ═══════════════════════════════════════════════════════════════════════════

        private void ApplyGrantedEffects()
        {
            foreach (var effect in Spec.Base.GrantedEffects)
            {
                if (effect == null) continue;
                
                // Generate spec with item as the origin (provides level context)
                Spec.Owner.ApplyGameplayEffect(effect.Generate(Spec, Spec.Owner));
            }
        }

        private void RemoveGrantedEffects()
        {
            foreach (var effect in Spec.Base.GrantedEffects)
            {
                Spec.Owner.RemoveGameplayEffect(effect);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Ability Application
        // ═══════════════════════════════════════════════════════════════════════════

        private void ApplyActiveAbility()
        {
            if (Spec.Owner.FindAbilitySystem(out var abilitySystem))
            {
                // Grant the ability at the item's level
                abilitySystem.GiveAbility(Spec.Base.ActiveAbility, Spec.Level, out _);
            }
        }

        private void RemoveActiveAbility()
        {
            if (Spec.Owner.FindAbilitySystem(out var abilitySystem))
            {
                abilitySystem.RemoveAbility(Spec.Base.ActiveAbility);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tag Enumeration (for CompileGrantedTags)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns all tags granted by this item when equipped.
        /// Used by CompileGrantedTags in GAS.
        /// </summary>
        public IEnumerable<Tag> GetGrantedTags()
        {
            if (!IsEquipped) yield break;
            
            // Asset tag
            if (Spec.Base.Tags.AssetTag != null)
                yield return Spec.Base.Tags.AssetTag;
            
            // Passive granted tags
            foreach (var tag in Spec.Base.Tags.PassiveGrantedTags)
            {
                if (tag != null)
                    yield return tag;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Management
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the item's level, refreshing applied effects/abilities if equipped.
        /// </summary>
        public void SetLevel(int newLevel)
        {
            int oldLevel = Spec.Level;
            Spec.SetLevel(newLevel);
            
            if (IsEquipped && oldLevel != Spec.Level)
            {
                RefreshForLevelChange();
            }
        }

        /// <summary>
        /// Levels up the item. Returns true if successful.
        /// </summary>
        public bool LevelUp()
        {
            if (!Spec.LevelUp()) return false;
            
            if (IsEquipped)
            {
                RefreshForLevelChange();
            }
            
            return true;
        }

        private void RefreshForLevelChange()
        {
            if (Spec.Owner.FindAbilitySystem(out var abilitySystem))
            {
                var containers = abilitySystem.GetAbilityContainers();
                var container = containers.FirstOrDefault(c => c.Spec.Base == Spec.Base.ActiveAbility);
                container?.Spec.SetLevel(Spec.Level);
            }
            
            // Note: Effects that are linked to this item will automatically use the updated
            // Spec.Level through the ILevelProvider interface when they evaluate scalers.
            // For effects that need immediate recalculation, you may need to reapply them.
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString()
        {
            string status = IsEquipped ? "Equipped" : "Inventory";
            return $"ItemContainer[{Spec.Base.GetName()} Lv.{Spec.Level} ({status})]";
        }
    }
}
