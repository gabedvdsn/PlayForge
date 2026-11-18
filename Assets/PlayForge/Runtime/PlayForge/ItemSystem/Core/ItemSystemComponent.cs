using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An entity's inventory: the items it holds, each in a slot (an <c>ItemSpecContainer</c>) with its level and
    /// stacks, and which of them it wears. <c>GiveItem</c>, <c>EquipItem</c>, <c>UnequipItem</c>, <c>RemoveItem</c> and
    /// <c>ConsumeItem</c> change it, within its entity's item limits and duplicate rules; a worn item puts on its
    /// Granted Effects and Granted Tags, lends its Active Ability and adds its workers, and takes them back as it comes
    /// off. Code reaches it through the entity's hub, <c>GetItemSystem()</c>, and hears each change through its
    /// <c>Callbacks</c>.
    /// </summary>
    public class ItemSystemComponent : DeferredContextSystem
    {
        public readonly IGameplayAbilitySystem Self;
        public readonly ItemSystemCallbacks Callbacks = new();

        private readonly List<ItemSpecContainer> ItemShelf = new();

        private bool allowDuplicateItems;
        private bool allowDuplicateEquippedItems;
        private ScalableInt maxItemsOperation;
        private ScalableInt maxEquippedItemsOperation;
        
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

        public void Setup(bool allowDuplicates, bool allowDuplicateEquipped, ScalableInt maxItemsOperation, ScalableInt maxEquippedItemsOperation)
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
            if (ItemCount >= maxItemsOperation.Evaluate(IAttributeImpactCarrier.GenerateLevelerDerivation(Self, Self.GetLevel()))) return false;

            return true;
        }

        public bool CanEquipItem(Item item)
        {
            if (!allowDuplicateEquippedItems && IsItemEquipped(item)) return false;
            if (EquippedItemCount >=
                maxEquippedItemsOperation.Evaluate(IAttributeImpactCarrier.GenerateLevelerDerivation(Self, Self.GetLevel()))) return false;

            return true;
        }
        
        public bool GiveItem(Item item, out int itemIndex)
        {
            return GiveItem(item, item.StartingLevel, out itemIndex);
        }
        public bool GiveItem(Item item, int level, out int itemIndex)
        {
            itemIndex = -1;

            // Into a stack of it with room first, which takes no new slot
            if (item != null && TryStack(item, out var stack))
            {
                itemIndex = stack.Index;
                Callbacks.ItemGiven(ItemCallbackStatus.Generate(stack, this, true));
                return true;
            }

            if (!CanGiveItem(item)) return false;

            itemIndex = ItemShelf.Count;
            var container = new ItemSpecContainer(item.Generate(Self, level), itemIndex);

            ItemShelf.Add(container);
            container.HearGiven();
            // Every held item's tag is its holder's, so the tags compile as it comes: they waited for the next
            Self.CompileGrantedTags();

            Callbacks.ItemGiven(ItemCallbackStatus.Generate(container, this, true));
            
            return true;
        }
        
        // Adds one to the first stack of the item with room, if it stacks
        private bool TryStack(Item item, out ItemSpecContainer stack)
        {
            stack = null;
            // A slot of it with room, by the stack size of its own level, which 1 or less leaves none
            foreach (var container in ItemShelf)
            {
                if (container.Spec.Base != item || !container.HasRoom) continue;
                container.ChangeStacks(1);
                stack = container;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gives up to <paramref name="count"/> of the item: into its stacks first, up to its Max Stack Size, then, as
        /// Allow Duplicate Items lets it, into new slots. How many it took.
        /// </summary>
        public int GiveItem(Item item, int count)
        {
            if (item == null) return 0;
            int taken = 0;
            while (taken < count && GiveItem(item, item.StartingLevel, out _)) taken++;
            return taken;
        }

        /// <summary>
        /// Takes up to <paramref name="count"/> of the item off, from its last slot first; a slot whose last stack comes
        /// off goes, as <see cref="RemoveItem(int)"/> takes it. How many came off.
        /// </summary>
        public int RemoveItem(Item item, int count)
        {
            int removed = 0;
            for (int i = ItemShelf.Count - 1; i >= 0 && removed < count; i--)
            {
                var container = ItemShelf[i];
                if (container.Spec.Base != item) continue;
                int off = System.Math.Min(container.ItemStacks, count - removed);
                if (off >= container.ItemStacks) RemoveItem(i);
                else container.ChangeStacks(-off);
                removed += off;
            }
            return removed;
        }

        /// <summary>
        /// Uses up to <paramref name="count"/> of the item, as a potion's use, taken off as <see cref="RemoveItem(Item, int)"/>
        /// takes them, and tells its callbacks' Item Consumed how many went; how many it used.
        /// </summary>
        public int ConsumeItem(Item item, int count)
        {
            if (item == null || count <= 0) return 0;
            int used = RemoveItem(item, count);
            if (used > 0) Callbacks.ItemConsumed(item, used);
            return used;
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
            container.HearRemoved();
            // Its tag goes with it: unequipping compiled while it was still held
            Self.CompileGrantedTags();

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

        /// <summary>Whether a worn item lent the ability in the slot, which comes back with the item.</summary>
        internal bool Lends(int slot)
        {
            if (ItemShelf == null) return false;
            foreach (var container in ItemShelf)
                if (container.IsEquipped && container.GrantedAbilitySlot == slot) return true;
            return false;
        }

        /// <summary>
        /// Lets go of every item at once, with nothing told. Their effects went with the shelf's, and their abilities with
        /// the ability system's, as a restore clears them.
        /// </summary>
        internal void ClearForRestore()
        {
            if (ItemShelf == null) return;
            foreach (var container in ItemShelf)
            {
                container.ForgetForRestore();
                container.HearRemoved();
            }
            ItemShelf.Clear();
        }

        /// <summary>
        /// Puts an item back as a save left it, in the next slot: its level and stacks, and worn where it was, with its
        /// ability and workers. Its effects come back from the save with the rest, so wearing it applies none, and nothing
        /// is told.
        /// </summary>
        internal ItemSpecContainer Restore(Item item, int level, int stacks, bool equipped)
        {
            if (ItemShelf == null || item == null) return null;

            var container = new ItemSpecContainer(item.Generate(Self, level), ItemShelf.Count);
            container.Spec.Stacks = System.Math.Clamp(stacks, 1, item.Definition.StackSizeOf(container.Spec));
            ItemShelf.Add(container);
            container.HearGiven();
            if (equipped) container.RestoreEquipped();
            return container;
        }

        /// <summary>
        /// Clears all items from inventory. Unequips all equipped items first.
        /// </summary>
        public void ClearAllItems()
        {
            // From the end, because removing an item moves the ones after it down. Counting up skipped every
            // other item, and clearing the shelf dropped those with their effects still on
            for (int i = ItemShelf.Count - 1; i >= 0; i--)
            {
                RemoveItem(i);
            }
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
