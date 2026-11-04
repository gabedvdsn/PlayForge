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

        // The slot this item's grant took, or -1 when it granted nothing; unequipping takes back only that (GAS-48)
        private int grantedAbilityIndex = -1;
        public bool IsEquipped
        {
            get => Spec.IsEquipped;
            private set => Spec.IsEquipped = value;
        }

        // Always 0 until stacking is built in Phase 5 (FEAT-14, GAS-51)
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
            Spec.Base.InitWorkers(Spec.Source);
            // Spec.Base.WorkerGroup?.ProvideWorkersTo(Spec.Source);
            
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
            Spec.Base.RemoveWorkers(Spec.Source);
            // Spec.Base.WorkerGroup?.RemoveWorkersFrom(Spec.Source);
            
            // Ahead of the compile, which is what reads it
            IsEquipped = false;

            // Recompile tags
            Spec.Source.CompileGrantedTags();

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
                bool applied = Spec.Source.ApplyCarrier(effect.Generate(Spec, Spec.Source));
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
                if (effect == null) continue;

                // Only what this item applied: the same effect can be on the entity from somewhere else
                bool removed = Spec.Source.RemoveGameplayEffect(effect, Spec);
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
            // Most items have none, and are worn for their effects
            if (Spec.Base.ActiveAbility == null) return;

            if (Spec.Source.FindAbilitySystem(out var abilitySystem))
            {
                // Grant the ability at the item's level, and note the slot it took
                var success = abilitySystem.GiveAbility(Spec.Base.ActiveAbility, Spec.GetLevel().CurrentValue, out int index);
                grantedAbilityIndex = success ? index : -1;
                if (Spec.Source.FindItemSystem(out var isc))
                {
                    isc.Callbacks.ItemAbilityGranted(ItemCallbackStatus.Generate(this, isc, success));
                } 
            }
        }

        private void RemoveActiveAbility()
        {
            if (Spec.Base.ActiveAbility == null) return;

            if (Spec.Source.FindAbilitySystem(out var abilitySystem))
            {
                // Only the copy this item granted. A failed grant took nothing, and the entity keeps a copy it had
                // already; it lost that copy when the item came off (GAS-48)
                var success = grantedAbilityIndex >= 0
                              && abilitySystem.RemoveAbilityAt(grantedAbilityIndex, Spec.Base.ActiveAbility);
                grantedAbilityIndex = -1;
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
