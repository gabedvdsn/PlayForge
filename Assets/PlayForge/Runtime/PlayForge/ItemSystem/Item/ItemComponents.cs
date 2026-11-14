using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Uses: how many times an item's ability can be used, each use one that paid what it costs, as a potion's sips or a
    /// wand's charges (FN-14, Decision 77). With none left, one of the item goes: a stack of it, its uses whole again for
    /// the next, or the item with its last. A load gives its uses whole. An item holds one at most.
    /// </summary>
    [Serializable]
    public sealed class UsesItemComponent : AbstractItemComponent
    {
        [Tooltip("How many times its ability can be used before one of it goes, read at the item's level")]
        public ScalableInt Uses = 3;

        public override bool AllowsSeveral => false;

        // How many of its uses an entity's item has spent
        private sealed class Spent
        {
            public int Count;
        }

        /// <summary>How many uses an entity's item has left, of its stack's current one.</summary>
        public int Left(ItemSpecContainer container) =>
            container == null ? 0 : Math.Max(0, UsesOf(container) - container.ComponentStates.Of<Spent>(this).Count);

        private int UsesOf(ItemSpecContainer container) =>
            Uses == null ? 0 : Uses.Evaluate(new NumberContext(container.Spec.Source, null, container.Spec.GetLevel(), true, container.Spec));

        public override void Used(ItemComponentContext ctx)
        {
            var container = ctx.Container;
            if (container?.Spec == null) return;
            var spent = ctx.GetState<Spent>(this);
            if (++spent.Count < UsesOf(container)) return;

            // Its uses run out: a stack goes, or the item with its last, at the frame's end, its ability's end done
            spent.Count = 0;
            var queue = container.Spec.Source.ToGAS()?.GetActionQueue();
            if (queue != null) Queue(queue, container);
            else Spend(container);
        }

        // Queued in a method of its own, so its closure is made only as the uses run out
        private static void Queue(ActionQueue queue, ItemSpecContainer container) =>
            queue.Enqueue(new LambdaAction(_ => Spend(container), "Uses spent"));

        // One of the item gone: a stack, or the item with its last, while its holder still holds it
        private static void Spend(ItemSpecContainer container)
        {
            if (!container.Spec.Source.FindItemSystem(out var items)
                || !items.TryGetItemContainer(container.Index, out var held) || !ReferenceEquals(held, container)) return;
            if (container.ItemStacks > 1) container.ChangeStacks(-1);
            else items.RemoveItem(container.Index);
        }
    }

    /// <summary>
    /// Set Bonus: an effect its holder has while it has enough of a set's pieces equipped, as two pieces of a set giving
    /// fire resistance and four a flame aura (FN-14, Decision 77). Each piece names its set by a tag and carries the set's
    /// bonuses; the holder's equipped pieces count each item once, and a bonus holds while they're as many as it needs,
    /// once for the holder however many pieces carry it. An item holds one for each of its set's bonuses.
    /// </summary>
    [Serializable]
    public sealed class SetBonusItemComponent : AbstractItemComponent
    {
        [Tooltip("The set the item is a piece of, by its tag")]
        [ForgeTagContext(ForgeContext.ContextTag)]
        public Tag Set;

        [Tooltip("How many of the set's pieces its holder needs equipped for the bonus, each item counted once")]
        [Min(1)] public int Pieces = 2;

        [Tooltip("The effect its holder has while it has that many equipped")]
        public GameplayEffect Bonus;

        public override void Equipped(ItemComponentContext ctx)
        {
            if (!ctx.Restoring) ForgeSets.Refresh(ctx.Container, Set, null);
        }

        public override void Unequipped(ItemComponentContext ctx)
        {
            if (!ctx.Restoring) ForgeSets.Refresh(ctx.Container, Set, ctx.Container);
        }
    }

    /// <summary>What weighs a holder's set bonuses against the set's pieces it has equipped (FN-14).</summary>
    internal static class ForgeSets
    {
        /// <summary>
        /// Each bonus of a set on the holder of <paramref name="from"/> as its equipped pieces now call for, but for
        /// <paramref name="leaving"/>, being unequipped. A bonus is the effect from one of the pieces, which moves to another
        /// piece as that one leaves while the bonus holds.
        /// </summary>
        internal static void Refresh(ItemSpecContainer from, Tag set, ItemSpecContainer leaving)
        {
            var holder = from?.Spec?.Source;
            if (holder == null || !set.IsValid || !holder.FindItemSystem(out var items)) return;

            // The set's pieces the holder has equipped, each item once, and the bonuses any piece it holds carries
            var pieces = new List<ItemSpecContainer>();
            var counted = new HashSet<Item>();
            var tiers = new List<SetBonusItemComponent>();
            foreach (var container in items.GetItemContainers())
            {
                if (container?.Spec?.Base?.Components == null) continue;
                bool piece = false;
                foreach (var component in container.Spec.Base.Components)
                {
                    if (component is not SetBonusItemComponent bonus || !bonus.Set.Equals(set) || bonus.Bonus == null) continue;
                    piece = true;
                    if (!tiers.Exists(tier => tier.Pieces == bonus.Pieces && tier.Bonus == bonus.Bonus)) tiers.Add(bonus);
                }
                if (piece && container.IsEquipped && !ReferenceEquals(container, leaving) && counted.Add(container.Spec.Base))
                    pieces.Add(container);
            }

            var shelf = holder.ToGAS()?.GetEffectSystem()?.Shelf;
            foreach (var tier in tiers)
            {
                bool wanted = pieces.Count >= tier.Pieces;
                var held = Held(shelf, tier, set);
                if (held != null && (!wanted || leaving != null && ReferenceEquals(held.Spec.Origin, leaving.Spec)))
                {
                    holder.RemoveGameplayEffect(tier.Bonus, held.Spec.Origin);
                    held = null;
                }
                if (wanted && held == null) holder.ApplyCarrier(tier.Bonus.Generate(pieces[0].Spec, holder));
            }
        }

        /// <summary>Each set the holder's items are pieces of, weighed once, as after a load (FN-14).</summary>
        internal static void RefreshAll(ItemSystemComponent items)
        {
            if (items == null) return;
            var sets = new List<(Tag Set, ItemSpecContainer From)>();
            foreach (var container in items.GetItemContainers())
            {
                if (container?.Spec?.Base?.Components == null) continue;
                foreach (var component in container.Spec.Base.Components)
                    if (component is SetBonusItemComponent bonus && bonus.Set.IsValid && !sets.Exists(each => each.Set.Equals(bonus.Set)))
                        sets.Add((bonus.Set, container));
            }
            foreach (var (set, from) in sets) Refresh(from, set, null);
        }

        // A tier's bonus on its holder, from one of the set's pieces, or null
        private static AbstractEffectContainer Held(IReadOnlyList<AbstractEffectContainer> shelf, SetBonusItemComponent tier, Tag set)
        {
            if (shelf == null) return null;
            foreach (var container in shelf)
            {
                if (!ReferenceEquals(container?.Spec?.Base, tier.Bonus) || container.Spec.Origin is not ItemSpec piece || piece.Base?.Components == null) continue;
                foreach (var component in piece.Base.Components)
                    if (component is SetBonusItemComponent other && other.Set.Equals(set) && other.Pieces == tier.Pieces && other.Bonus == tier.Bonus)
                        return container;
            }
            return null;
        }
    }
}
