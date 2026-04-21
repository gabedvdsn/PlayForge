using System.Collections.Generic;
using System.Linq;
using UnityEditor.Purchasing;

namespace FarEmerald.PlayForge
{
    public class ItemSystemComponent : DeferredContextSystem
    {
        public readonly IGameplayAbilitySystem Self;
        public readonly ItemSystemCallbacks Callbacks = new();

        private List<ItemSpecContainer> ItemShelf;

        private bool allowDuplicateItems;
        private bool allowDuplicateEquippedItems;
        private ScalerIntegerMagnitudeOperation maxItemsOperation;
        private ScalerIntegerMagnitudeOperation maxEquippedItemsOperation;
        
        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        private bool _locked;

        public bool Locked
        {
            get => _locked;
            set => _locked = value;
        }

        public ItemSystemComponent(IGameplayAbilitySystem self)
        {
            Self = self;
        }

        public void Setup(bool allowDuplicates, bool allowDuplicateEquipped, ScalerIntegerMagnitudeOperation maxItemsOperation, ScalerIntegerMagnitudeOperation maxEquippedItemsOperation)
        {
            allowDuplicateItems = allowDuplicates;
            allowDuplicateEquippedItems = allowDuplicateEquipped;
            this.maxItemsOperation = maxItemsOperation;
            this.maxEquippedItemsOperation = maxEquippedItemsOperation;
        }

        public void Initialize(List<StartingItemContainer> startingItems)
        {
            foreach (var item in startingItems)
            {
                if (item.EquipOnInit) GiveAndEquipItem(item.Item, out _);
                else GiveItem(item.Item, out _);
            }
        }

        public bool CanGiveItem(Item item)
        {
            if (item is null) return false;
            if (!allowDuplicateItems && HasItem(item)) return false;
            if (ItemCount >= maxItemsOperation.Evaluate(IAttributeImpactDerivation.GenerateLevelerDerivation(Self, Self.GetLevel()))) return false;

            return true;
        }

        public bool CanEquipItem(Item item)
        {
            if (!allowDuplicateEquippedItems && IsItemEquipped(item)) return false;
            if (EquippedItemCount >=
                maxEquippedItemsOperation.Evaluate(IAttributeImpactDerivation.GenerateLevelerDerivation(Self, Self.GetLevel()))) return false;

            return true;
        }
        
        public bool GiveItem(Item item, out int itemIndex)
        {
            return GiveItem(item, item.StartingLevel, out itemIndex);
        }
        public bool GiveItem(Item item, int level, out int itemIndex)
        {
            itemIndex = -1;
            if (!CanGiveItem(item)) return false;

            itemIndex = ItemShelf.Count;
            var container = new ItemSpecContainer(item.Generate(Self, level), itemIndex);
            
            ItemShelf.Add(container);
            
            Callbacks.ItemGiven(ItemCallbackStatus.Generate(container, this, true));
            
            return true;
        }
        
        public bool GiveAndEquipItem(Item item, out int itemIndex)
        {
            return GiveAndEquipItem(item, item.StartingLevel, out itemIndex);
        }

        public bool GiveAndEquipItem(Item item, int level, out int itemIndex)
        {
            return GiveItem(item, level, out itemIndex) && EquipItem(itemIndex);
        }
        
        public bool EquipItem(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container)) return false;
            if (!CanEquipItem(container.Spec.Base)) return false;
            return container.Equip();
        }
        
        public bool EquipItem(Item item)
        {
            if (!TryGetItemContainer(item, out var container)) return false;
            if (!CanEquipItem(container.Spec.Base)) return false;
            return container.Equip();
        }
        
        public bool UnequipItem(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            
            return container.Unequip();
        }
        
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
            if (!container.IsEquipped && !CanEquipItem(container.Spec.Base)) return false;
            
            return container.IsEquipped ? container.Unequip() : container.Equip();
        }

        /// <summary>
        /// Toggles an item's equipped state by index.
        /// </summary>
        public bool ToggleItemEquipped(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
                return false;
            if (!container.IsEquipped && !CanEquipItem(container.Spec.Base)) return false;
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
        
        /// <summary>
        /// Removes an item from inventory by index. Automatically unequips if equipped.
        /// </summary>
        public bool RemoveItem(int itemIndex)
        {
            if (!TryGetItemContainer(itemIndex, out var container))
            {
                Callbacks.ItemRemoved(ItemCallbackStatus.Generate(container, this, false));
                return false;
            }
            
            // Unequip first if equipped
            if (container.IsEquipped)
            {
                container.Unequip();
            }
            
            ItemShelf.RemoveAt(itemIndex);
            for (int i = itemIndex; i < ItemShelf.Count; i++)
            {
                ItemShelf[i].Index -= 1;
            }
            
            Callbacks.ItemRemoved(ItemCallbackStatus.Generate(container, this, true));

            return true;
        }

        /// <summary>
        /// Removes an item by reference. Automatically unequips if equipped.
        /// </summary>
        public bool RemoveItem(Item item)
        {
            if (!TryGetItemContainer(item, out var container))
                return false;

            return RemoveItem(container.Index);
        }

        /// <summary>
        /// Removes all items with the specified asset tag.
        /// </summary>
        /// <returns>Number of items removed</returns>
        public int RemoveItem(Tag assetTag)
        {
            if (ItemShelf == null) return 0;
            
            var toRemove = ItemShelf.Where(c => c.Spec.Base.Tags.AssetTag == assetTag).ToList();
            
            foreach (var container in toRemove)
            {
                RemoveItem(container.Index);
            }
            
            return toRemove.Count;
        }

        /// <summary>
        /// Clears all items from inventory. Unequips all equipped items first.
        /// </summary>
        public void ClearAllItems()
        {
            for (int i = 0; i < ItemShelf.Count; i++)
            {
                RemoveItem(i);
            }
            
            ItemShelf?.Clear();
        }

        public bool HasItem(Item item)
        {
            return ItemShelf.Any(c => c.Spec.Base == item);
        }
        
        public bool HasItem(ItemSpec spec)
        {
            return ItemShelf?.Any(c => c.Spec == spec) ?? false;
        }

        /// <summary>
        /// Checks if the entity has any item with the specified asset tag.
        /// </summary>
        public bool HasItem(Tag assetTag)
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
        public int ItemCount => ItemShelf?.Count ?? 0;

        /// <summary>
        /// Gets the number of currently equipped items.
        /// </summary>
        public int EquippedItemCount => ItemShelf?.Count(c => c.IsEquipped) ?? 0;
        
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

        public int GetItemIndex(Item item)
        {
            for (int i = 0; i < ItemShelf.Count; i++)
            {
                if (ItemShelf[i].Spec.Base == item) return i;
            }

            return -1;
        }

        public int GetItemIndex(Tag assetTag)
        {
            for (int i = 0; i < ItemShelf.Count; i++)
            {
                if (ItemShelf[i].Spec.Base.Tags.AssetTag == assetTag) return i;
            }

            return -1;
        }
        
        /// <summary>
        /// Tries to get a specific item container by item reference.
        /// </summary>
        public bool TryGetItemContainer(Item item, out ItemSpecContainer container)
        {
            container = ItemShelf?.FirstOrDefault(c => c.Spec.Base == item);
            return container != null;
        }
        
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
        public bool TryGetItemContainer(Tag assetTag, out ItemSpecContainer container)
        {
            container = ItemShelf?.FirstOrDefault(c => c.Spec.Base.Tags.AssetTag == assetTag);
            return container != null;
        }
    }
}
