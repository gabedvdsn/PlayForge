using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

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
        public int Index;
        public bool IsEquipped { get; private set; }
        public int ItemStacks { get; private set; }

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructor
        // ═══════════════════════════════════════════════════════════════════════════

        public ItemSpecContainer(ItemSpec spec, int index)
        {
            Spec = spec;
            Index = index;
            
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
            if (IsEquipped)
            {
                if (Spec.Source.FindItemSystem(out var _isc))
                {
                    _isc.Callbacks.ItemEquipped(ItemCallbackStatus.Generate(this, _isc, false));
                }                
                return false;
            }
            
            IsEquipped = true;
            
            // Apply granted effects (must come before ability so effects are active)
            ApplyGrantedEffects();
            
            // Apply active ability
            ApplyActiveAbility();
            
            // Provide workers
            Spec.Base.WorkerGroup?.ProvideWorkersTo(Spec.Source);
            
            // Recompile tags (effects already added their tags, but this ensures consistency)
            Spec.Source.CompileGrantedTags();

            if (Spec.Source.FindItemSystem(out var isc))
            {
                isc.Callbacks.ItemEquipped(ItemCallbackStatus.Generate(this, isc, true));
            }
            
            return true;
        }

        /// <summary>
        /// Unequips the item, removing all granted effects, abilities, and tags.
        /// </summary>
        public bool Unequip()
        {
            if (!IsEquipped)
            {
                if (Spec.Source.FindItemSystem(out var _isc))
                {
                    _isc.Callbacks.ItemUnequipped(ItemCallbackStatus.Generate(this, _isc, false));
                }                
                return false;
            }
            
            
            // Remove active ability first
            RemoveActiveAbility();
            
            // Remove granted effects
            RemoveGrantedEffects();
            
            // Remove workers
            Spec.Base.WorkerGroup?.RemoveWorkersFrom(Spec.Source);
            
            // Recompile tags
            Spec.Source.CompileGrantedTags();
            
            IsEquipped = false;
            
            if (Spec.Source.FindItemSystem(out var isc))
            {
                isc.Callbacks.ItemUnequipped(ItemCallbackStatus.Generate(this, isc, true));
            } 
            
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
                bool applied = Spec.Source.ApplyGameplayEffect(effect.Generate(Spec, Spec.Source));
                if (Spec.Source.FindItemSystem(out var isc))
                {
                    isc.Callbacks.ItemEffectsApplied(ItemCallbackStatus.Generate(this, isc, applied));
                } 
            }
        }

        private void RemoveGrantedEffects()
        {
            foreach (var effect in Spec.Base.GrantedEffects)
            {
                bool removed = Spec.Source.RemoveGameplayEffect(effect);
                if (Spec.Source.FindItemSystem(out var isc))
                {
                    isc.Callbacks.ItemEffectsRemoved(ItemCallbackStatus.Generate(this, isc, removed));
                } 
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Ability Application
        // ═══════════════════════════════════════════════════════════════════════════

        private void ApplyActiveAbility()
        {
            if (Spec.Source.FindAbilitySystem(out var abilitySystem))
            {
                // Grant the ability at the item's level
                var success = abilitySystem.GiveAbility(Spec.Base.ActiveAbility, Spec.GetLevel().CurrentValue, out _);
                if (Spec.Source.FindItemSystem(out var isc))
                {
                    isc.Callbacks.ItemAbilityGranted(ItemCallbackStatus.Generate(this, isc, success));
                } 
            }
        }

        private void RemoveActiveAbility()
        {
            if (Spec.Source.FindAbilitySystem(out var abilitySystem))
            {
                var success = abilitySystem.RemoveAbility(Spec.Base.ActiveAbility);
                if (Spec.Source.FindItemSystem(out var isc))
                {
                    isc.Callbacks.ItemAbilityRevoked(ItemCallbackStatus.Generate(this, isc, success));
                } 
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
            
            // Passive granted tags
            foreach (var tag in Spec.Base.Tags.PassiveGrantedTags)
            {
                if (tag != null)
                    yield return tag;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString()
        {
            string status = IsEquipped ? "Equipped" : "Inventory";
            return $"ItemContainer[{Spec.Base.GetName()} Lv.{Spec.GetLevel()} ({status})]";
        }
    }
}
