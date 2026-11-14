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

        /// <summary>How many of the item the slot holds, up to its Max Stack Size. It was never set (FEAT-14, GAS-51).</summary>
        public int ItemStacks => Spec.Stacks;

        /// <summary>Whether the slot has room for another of its item (FEAT-14).</summary>
        public bool HasRoom => Spec.Stacks < Spec.Base.Definition.StackSizeOf(Spec);

        /// <summary>
        /// Adds or takes off stacks, within 1 and the item's Max Stack Size, and how many it changed by. An equipped item's
        /// effects stay applied once, and their modifiers read the count again, so a scaler on it takes the new one.
        /// </summary>
        internal int ChangeStacks(int by)
        {
            int before = Spec.Stacks;
            Spec.Stacks = System.Math.Clamp(before + by, 1, Spec.Base.Definition.StackSizeOf(Spec));
            int changed = Spec.Stacks - before;
            if (changed != 0 && IsEquipped) Spec.Source.ToGAS()?.GetEffectSystem()?.RefreshEffectsFrom(Spec);
            return changed;
        }

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
            HearEquipped();
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
            HearUnequipped();
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
                Lend(abilitySystem);
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
        // Restore (FEAT-9)
        // ═══════════════════════════════════════════════════════════════════════════

        // The ability it granted knows it lends it, so a use of it is the item's (FN-14)
        private void Lend(AbilitySystemComponent abilitySystem)
        {
            if (grantedAbilityIndex >= 0 && abilitySystem.TryGetAbilityContainer(grantedAbilityIndex, out var lent) && lent?.Spec != null)
                lent.Spec.LentBy = this;
        }

        /// <summary>The slot the item's ability took, or -1 where it granted none, which a save leaves to the item.</summary>
        internal int GrantedAbilitySlot => grantedAbilityIndex;

        /// <summary>
        /// Worn again as a save left it: its ability and workers back, and its tags counted in the restore's compile. Its
        /// effects come back from the save with their time left, so this applies none, and nothing is told.
        /// </summary>
        internal void RestoreEquipped()
        {
            if (IsEquipped) return;

            IsEquipped = true;
            if (Spec.Base.ActiveAbility != null && Spec.Source.FindAbilitySystem(out var abilitySystem))
            {
                grantedAbilityIndex = abilitySystem.GiveQuietly(Spec.Base.ActiveAbility, Spec.GetLevel().CurrentValue, out int index)
                    ? index
                    : -1;
                Lend(abilitySystem);
            }
            Spec.Base.InitWorkers(Spec.Source);
            HearEquipped(restoring: true);
        }

        private ComponentStates componentStates;

        /// <summary>What its item's components keep for this entity's item (FN-14).</summary>
        public ComponentStates ComponentStates => componentStates ??= new ComponentStates();

        // Its item's components, each hearing a moment of it (FN-14)
        internal void HearGiven() => Hear(component => component.Given(new ItemComponentContext(this)));
        internal void HearRemoved() => Hear(component => component.Removed(new ItemComponentContext(this)));
        internal void HearUsed() => Hear(component => component.Used(new ItemComponentContext(this)));
        private void HearEquipped(bool restoring = false) => Hear(component => component.Equipped(new ItemComponentContext(this, restoring)));
        private void HearUnequipped(bool restoring = false) => Hear(component => component.Unequipped(new ItemComponentContext(this, restoring)));

        private void Hear(System.Action<AbstractItemComponent> moment)
        {
            var components = Spec?.Base?.Components;
            if (components == null) return;
            foreach (var component in components)
                if (component != null) moment(component);
        }

        /// <summary>Taken off by a restore, with nothing told. Its effects and its ability went with the rest.</summary>
        internal void ForgetForRestore()
        {
            if (!IsEquipped) return;

            Spec.Base.RemoveWorkers(Spec.Source);
            HearUnequipped(restoring: true);
            grantedAbilityIndex = -1;
            IsEquipped = false;
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
