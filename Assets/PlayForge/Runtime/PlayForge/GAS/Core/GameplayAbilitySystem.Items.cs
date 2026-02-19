using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Partial class handling item management for GameplayAbilitySystem.
    /// Provides inventory management with equip/unequip functionality.
    /// Items grant effects, abilities, and tags when equipped.
    /// </summary>
    public partial class GameplayAbilitySystem
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Item Storage
        // ═══════════════════════════════════════════════════════════════════════════
        
        private List<ItemSpecContainer> ItemShelf;
        
        /// <summary>
        /// Initializes the item system. Call from Awake.
        /// </summary>
        private void InitializeItemSystem()
        {
            ItemShelf ??= new List<ItemSpecContainer>();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Item Queries
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks if the entity has a specific item in inventory (equipped or not).
        /// </summary>
        public bool HasItem(Item item)
        {
            return ItemShelf?.Any(c => c.Spec.Base == item) ?? false;
        }
        
        public bool HasItem(ItemSpec spec)
        {
            return ItemShelf?.Any(c => c.Spec == spec) ?? false;
        }

        /// <summary>
        /// Checks if the entity has any item with the specified asset tag.
        /// </summary>
        public bool HasItemWithTag(Tag assetTag)
        {
            return ItemShelf?.Any(c => c.Spec.Base.Tags.AssetTag == assetTag) ?? false;
        }

        /// <summary>
        /// Checks if a specific item is currently equipped.
        /// </summary>
        public bool IsItemEquipped(Item item)
        {
            return ItemShelf?.Any(c => c.Spec.Base == item && c.IsEquipped) ?? false;
        }

        /// <summary>
        /// Gets the total number of items in inventory.
        /// </summary>
        public int GetItemCount()
        {
            return ItemShelf?.Count ?? 0;
        }

        /// <summary>
        /// Gets the number of currently equipped items.
        /// </summary>
        public int GetEquippedItemCount()
        {
            return ItemShelf?.Count(c => c.IsEquipped) ?? 0;
        }

        /// <summary>
        /// Gets all item containers (read-only access).
        /// </summary>
        public IReadOnlyList<ItemSpecContainer> GetItemContainers()
        {
            return ItemShelf ?? new List<ItemSpecContainer>();
        }

        /// <summary>
        /// Gets all currently equipped item containers.
        /// </summary>
        public IEnumerable<ItemSpecContainer> GetEquippedItems()
        {
            return ItemShelf?.Where(c => c.IsEquipped) ?? Enumerable.Empty<ItemSpecContainer>();
        }

        /// <summary>
        /// Tries to get a specific item container by item reference.
        /// </summary>
        public bool TryGetItemContainer(Item item, out ItemSpecContainer container)
        {
            container = ItemShelf?.FirstOrDefault(c => c.Spec.Base == item);
            return container != null;
        }

        /// <summary>
        /// Tries to get a specific item container by inventory index.
        /// </summary>
        public bool TryGetItemContainer(int index, out ItemSpecContainer container)
        {
            if (ItemShelf != null && index >= 0 && index < ItemShelf.Count)
            {
                container = ItemShelf[index];
                return true;
            }
            container = null;
            return false;
        }

        /// <summary>
        /// Tries to get an item container by asset tag.
        /// </summary>
        public bool TryGetItemContainerByTag(Tag assetTag, out ItemSpecContainer container)
        {
            container = ItemShelf?.FirstOrDefault(c => c.Spec.Base.Tags.AssetTag == assetTag);
            return container != null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Give Item
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gives an item to the entity at a specific level. Does not auto-equip.
        /// </summary>
        /// <param name="item">The item to give</param>
        /// <param name="level">The level of the item</param>
        /// <param name="itemIndex">Output: index of the item in inventory (-1 if failed)</param>
        /// <returns>True if item was given, false if duplicate and not allowed</returns>
        public bool GiveItem(Item item, int level, out int itemIndex)
        {
            itemIndex = -1;
            InitializeItemSystem();
            
            if (item == null) return false;
            
            // Check duplicate rules
            bool allowDuplicates = Data.AllowDuplicateItems && item.Definition.AllowDuplicates;
            if (!allowDuplicates && HasItem(item))
            {
                return false;
            }
            
            // Create spec and container
            var spec = item.Generate(this, level);
            var container = new ItemSpecContainer(spec);
            
            itemIndex = ItemShelf.Count;
            ItemShelf.Add(container);
            
            return true;
        }

        /// <summary>
        /// Gives an item at its starting level.
        /// </summary>
        public bool GiveItem(Item item, out int itemIndex)
        {
            return GiveItem(item, item?.StartingLevel ?? 1, out itemIndex);
        }

        /// <summary>
        /// Gives an item and immediately equips it.
        /// </summary>
        public bool GiveAndEquipItem(Item item, int level, out int itemIndex)
        {
            if (GiveItem(item, level, out itemIndex))
            {
                return EquipItem(itemIndex);
            }
            return false;
        }

        /// <summary>
        /// Gives an item at starting level and immediately equips it.
        /// </summary>
        public bool GiveAndEquipItem(Item item, out int itemIndex)
        {
            return GiveAndEquipItem(item, item?.StartingLevel ?? 1, out itemIndex);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Equip / Unequip
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Equips an item by inventory index.
        /// Applies all granted effects, abilities, and tags.
        /// </summary>
        public bool EquipItem(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            
            return container.Equip();
        }

        /// <summary>
        /// Equips an item by reference.
        /// </summary>
        public bool EquipItem(Item item)
        {
            if (!TryGetItemContainer(item, out var container))
                return false;
            
            return container.Equip();
        }

        /// <summary>
        /// Unequips an item by inventory index.
        /// Removes all granted effects, abilities, and tags.
        /// </summary>
        public bool UnequipItem(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            
            return container.Unequip();
        }

        /// <summary>
        /// Unequips an item by reference.
        /// </summary>
        public bool UnequipItem(Item item)
        {
            if (!TryGetItemContainer(item, out var container))
                return false;
            
            return container.Unequip();
        }

        /// <summary>
        /// Toggles an item's equipped state.
        /// </summary>
        public bool ToggleItemEquipped(Item item)
        {
            if (!TryGetItemContainer(item, out var container))
                return false;
            
            return container.IsEquipped ? container.Unequip() : container.Equip();
        }

        /// <summary>
        /// Toggles an item's equipped state by index.
        /// </summary>
        public bool ToggleItemEquipped(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            
            return container.IsEquipped ? container.Unequip() : container.Equip();
        }

        /// <summary>
        /// Unequips all currently equipped items.
        /// </summary>
        public void UnequipAllItems()
        {
            if (ItemShelf == null) return;
            
            foreach (var container in ItemShelf.Where(c => c.IsEquipped).ToList())
            {
                container.Unequip();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Remove Item
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Removes an item from inventory by index. Automatically unequips if equipped.
        /// </summary>
        public bool RemoveItem(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            
            // Unequip first if equipped
            if (container.IsEquipped)
            {
                container.Unequip();
            }
            
            ItemShelf.RemoveAt(itemIndex);
            return true;
        }

        /// <summary>
        /// Removes an item by reference. Automatically unequips if equipped.
        /// </summary>
        public bool RemoveItem(Item item)
        {
            if (!TryGetItemContainer(item, out var container))
                return false;
            
            // Unequip first if equipped
            if (container.IsEquipped)
            {
                container.Unequip();
            }
            
            return ItemShelf.Remove(container);
        }

        /// <summary>
        /// Removes all items with the specified asset tag.
        /// </summary>
        /// <returns>Number of items removed</returns>
        public int RemoveItemsByTag(Tag assetTag)
        {
            if (ItemShelf == null) return 0;
            
            var toRemove = ItemShelf.Where(c => c.Spec.Base.Tags.AssetTag == assetTag).ToList();
            
            foreach (var container in toRemove)
            {
                if (container.IsEquipped)
                {
                    container.Unequip();
                }
                ItemShelf.Remove(container);
            }
            
            return toRemove.Count;
        }

        /// <summary>
        /// Clears all items from inventory. Unequips all equipped items first.
        /// </summary>
        public void ClearAllItems()
        {
            UnequipAllItems();
            ItemShelf?.Clear();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Item Level Management
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the level of an item by inventory index.
        /// </summary>
        public bool SetItemLevel(int itemIndex, int level)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            
            container.SetLevel(level);
            return true;
        }

        /// <summary>
        /// Sets the level of an item by reference.
        /// </summary>
        public bool SetItemLevel(Item item, int level)
        {
            if (!TryGetItemContainer(item, out var container))
                return false;
            
            container.SetLevel(level);
            return true;
        }

        /// <summary>
        /// Levels up an item by inventory index.
        /// </summary>
        /// <returns>True if level was increased, false if already at max</returns>
        public bool LevelUpItem(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            
            return container.LevelUp();
        }

        /// <summary>
        /// Levels up an item by reference.
        /// </summary>
        public bool LevelUpItem(Item item)
        {
            if (!TryGetItemContainer(item, out var container))
                return false;
            
            return container.LevelUp();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tag Compilation Integration
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Compiles tags from all equipped items.
        /// Called from main CompileGrantedTags method.
        /// </summary>
        private void CompileItemTags()
        {
            if (ItemShelf == null) return;
            
            foreach (var container in ItemShelf.Where(c => c.IsEquipped))
            {
                foreach (var tag in container.GetGrantedTags())
                {
                    TagCache.AddTag(tag);
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Item System Accessors
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Finds the item system (for interface parity with ability/attribute systems).
        /// </summary>
        public bool FindItemSystem(out List<ItemSpecContainer> itemContainers)
        {
            InitializeItemSystem();
            itemContainers = ItemShelf;
            return ItemShelf != null;
        }
    }
}