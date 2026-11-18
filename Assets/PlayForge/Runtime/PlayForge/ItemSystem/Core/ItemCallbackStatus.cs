namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What an item system's callback is handed: the item's slot (<c>Container</c>) and <c>Spec</c>, the item system,
    /// and whether what it tells succeeded (<c>Success</c>), as equipping an item already worn doesn't; <c>Index</c>,
    /// <c>Item</c>, <c>Level</c> and <c>IsEquipped</c> read the slot.
    /// </summary>
    public struct ItemCallbackStatus
    {
        public readonly ItemSpec Spec;
        public readonly ItemSpecContainer Container;
        public readonly ItemSystemComponent ItemSystem;
        public readonly bool Success;

        public int Index => Container?.Index ?? -1;
        public Item Item => Spec?.Base;
        public int Level => Spec?.GetLevel().CurrentValue ?? 0;
        public bool IsEquipped => Container?.IsEquipped ?? false;

        private ItemCallbackStatus(ItemSpec spec, ItemSpecContainer container, ItemSystemComponent itemSystem, bool success)
        {
            Spec = spec;
            Container = container;
            ItemSystem = itemSystem;
            Success = success;
        }

        /// <summary>
        /// Item is given to inventory
        /// </summary>
        public static ItemCallbackStatus Generate(ItemSpecContainer container, ItemSystemComponent itemSystem, bool success)
        {
            return new ItemCallbackStatus(container?.Spec, container, itemSystem, success);
        }
    }
}