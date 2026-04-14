using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Callbacks for item system events.
    /// Used for monitoring item lifecycle without modifying behavior.
    /// </summary>
    public class ItemSystemCallbacks
    {
        public delegate void ItemSystemCallbackDelegate(ItemCallbackStatus status);
        
        // ═══════════════════════════════════════════════════════════════
        // INVENTORY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Item Given
        private ItemSystemCallbackDelegate _onItemGiven;
        public event ItemSystemCallbackDelegate OnItemGiven
        {
            add => _onItemGiven = AddUnique(_onItemGiven, value);
            remove => _onItemGiven -= value;
        }
        public void ItemGiven(ItemCallbackStatus status) => _onItemGiven?.Invoke(status);
        #endregion
        
        #region On Item Removed
        private ItemSystemCallbackDelegate _onItemRemoved;
        public event ItemSystemCallbackDelegate OnItemRemoved
        {
            add => _onItemRemoved = AddUnique(_onItemRemoved, value);
            remove => _onItemRemoved -= value;
        }
        public void ItemRemoved(ItemCallbackStatus status) => _onItemRemoved?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // EQUIPMENT LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Item Equipped
        private ItemSystemCallbackDelegate _onItemEquipped;
        public event ItemSystemCallbackDelegate OnItemEquipped
        {
            add => _onItemEquipped = AddUnique(_onItemEquipped, value);
            remove => _onItemEquipped -= value;
        }
        public void ItemEquipped(ItemCallbackStatus status) => _onItemEquipped?.Invoke(status);
        #endregion
        
        #region On Item Unequipped
        private ItemSystemCallbackDelegate _onItemUnequipped;
        public event ItemSystemCallbackDelegate OnItemUnequipped
        {
            add => _onItemUnequipped = AddUnique(_onItemUnequipped, value);
            remove => _onItemUnequipped -= value;
        }
        public void ItemUnequipped(ItemCallbackStatus status) => _onItemUnequipped?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // EFFECT LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Item Effects Applied
        private ItemSystemCallbackDelegate _onItemEffectsApplied;
        public event ItemSystemCallbackDelegate OnItemEffectsApplied
        {
            add => _onItemEffectsApplied = AddUnique(_onItemEffectsApplied, value);
            remove => _onItemEffectsApplied -= value;
        }
        public void ItemEffectsApplied(ItemCallbackStatus status) => _onItemEffectsApplied?.Invoke(status);
        #endregion
        
        #region On Item Effects Removed
        private ItemSystemCallbackDelegate _onItemEffectsRemoved;
        public event ItemSystemCallbackDelegate OnItemEffectsRemoved
        {
            add => _onItemEffectsRemoved = AddUnique(_onItemEffectsRemoved, value);
            remove => _onItemEffectsRemoved -= value;
        }
        public void ItemEffectsRemoved(ItemCallbackStatus status) => _onItemEffectsRemoved?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // ABILITY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Item Ability Granted
        private ItemSystemCallbackDelegate _onItemAbilityGranted;
        public event ItemSystemCallbackDelegate OnItemAbilityGranted
        {
            add => _onItemAbilityGranted = AddUnique(_onItemAbilityGranted, value);
            remove => _onItemAbilityGranted -= value;
        }
        public void ItemAbilityGranted(ItemCallbackStatus status) => _onItemAbilityGranted?.Invoke(status);
        #endregion
        
        #region On Item Ability Revoked
        private ItemSystemCallbackDelegate _onItemAbilityRevoked;
        public event ItemSystemCallbackDelegate OnItemAbilityRevoked
        {
            add => _onItemAbilityRevoked = AddUnique(_onItemAbilityRevoked, value);
            remove => _onItemAbilityRevoked -= value;
        }
        public void ItemAbilityRevoked(ItemCallbackStatus status) => _onItemAbilityRevoked?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // HELPER
        // ═══════════════════════════════════════════════════════════════
        
        private static ItemSystemCallbackDelegate AddUnique(ItemSystemCallbackDelegate existing, ItemSystemCallbackDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
    }
}