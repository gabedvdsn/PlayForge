using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One impact that moves an amount from one attribute to another (TW-20, Decision 59), each side its own: the target's
    /// Agility to the caster's, as Essence Shift steals it, or one entity's Agility to its Strength, as Attribute Shift moves
    /// it. Only what the giver has moves, so nothing is made from nothing. As its Delivery says, it moves once as the effect
    /// lands, each tick, or for as long as the effect lasts, a modifier on both that gives back as it ends.
    /// </summary>
    [Serializable]
    public class AttributeTransfer
    {
        [Tooltip("When it moves: once as the effect lands, each tick, or for as long as the effect lasts, giving back as it ends")]
        public EImpactDelivery Delivery = EImpactDelivery.OnApply;

        [Tooltip("Who gives: the effect's caster or its target")]
        public NumberSide From = NumberSide.Target;

        [Tooltip("The attribute it gives")]
        public Attribute FromAttribute;

        [Tooltip("Who gets: the effect's caster or its target")]
        public NumberSide To = NumberSide.Caster;

        [Tooltip("The attribute it gets")]
        public Attribute ToAttribute;

        [Tooltip("Which of each attribute's values it moves: its current, as a pool's Mana; its base; or both, as a stat's Strength")]
        public EAttributeTarget Slot = EAttributeTarget.Current;

        [Tooltip("How much it moves, a number that can scale by level; only what the giver has moves")]
        public ScalableFloat Amount = 0f;

        /// <summary>Whether it can move anything: both its attributes named, and an amount that's more than a plain 0.</summary>
        public bool Moves =>
            FromAttribute && ToAttribute && Amount != null && (Amount.Magnitude > 0f || !AbstractScaler.IsNone(Amount.Scaler));
    }

    /// <summary>What a lasting transfer holds while its effect lasts: the amount it read, and a carrier on each side (TW-20).</summary>
    internal sealed class LentTransfer
    {
        public AttributeTransfer Transfer;
        public float Amount;
        public AttributeSystemComponent Giver, Getter;
        public SourceAttributeImpact Giving, Getting;
    }

    /// <summary>
    /// What moves an effect's transfers (TW-20): its OnApply and Periodic ones as commits, permanent, and its Continuous ones
    /// as a modifier on each side that its effect holds and gives back. Each side's change is the system's own, exact: no
    /// worker takes it for a hit, no barrier soaks it and nothing scales it, so the getter gets what the giver gave, as far
    /// as its own bounds let it.
    /// </summary>
    internal static class ForgeTransfers
    {
        /// <summary>Whether an effect holds a transfer, a Transfer component's (TW-20, FN-14).</summary>
        internal static bool Any(GameplayEffect effect)
        {
            var components = effect?.Components;
            if (components == null) return false;
            for (int i = 0; i < components.Count; i++)
                if (components[i] is TransferEffectComponent { Transfer: not null }) return true;
            return false;
        }

        /// <summary>
        /// Moves what each of the effect's transfers of a delivery moves, now: OnApply as it lands, Periodic each tick.
        /// <paramref name="target"/> is the one the effect is on.
        /// </summary>
        internal static void Move(GameplayEffectSpec spec, ITarget target, EImpactDelivery delivery)
        {
            var components = spec?.Base?.Components;
            if (components == null || components.Count == 0) return;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is not TransferEffectComponent { Transfer: { } transfer }) continue;
                if (transfer.Delivery != delivery || !transfer.Moves) continue;
                if (!Sides(spec, target, transfer, out var giver, out var getter)) continue;
                float take = Take(transfer, giver, AmountOf(spec, target, transfer));
                if (take <= 0f) continue;

                // The giver's loss as it lands, which a floor can hold some of back; the getter gets that
                var source = spec.GetOrigin()?.GetSource();
                giver.TryGetValue(transfer.FromAttribute, out var before);
                giver.ModifyAttribute(transfer.FromAttribute, Change(source, spec, transfer.FromAttribute, Delta(transfer.Slot, -take)));
                giver.TryGetValue(transfer.FromAttribute, out var after);
                var moved = Moved(transfer.Slot, before, after);
                if (moved.CurrentValue <= 0f && moved.BaseValue <= 0f) continue;
                getter.ModifyAttribute(transfer.ToAttribute, Change(source, spec, transfer.ToAttribute, moved));
            }
        }

        /// <summary>
        /// Lends the effect's Continuous transfers for as long as it lasts, a modifier on the giver and one on the getter,
        /// each under a carrier of its own, and keeps on the spec what each holds. Read again, as a stack or a level reads
        /// the effect's entries again, one whose amount comes to what it was holds as it is; one that changed gives back and
        /// moves afresh. <paramref name="quietly"/> tells nothing, as a load or a move.
        /// </summary>
        internal static void Lend(GameplayEffectSpec spec, ITarget target, bool quietly = false)
        {
            var components = spec?.Base?.Components;
            if (components == null || components.Count == 0) return;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is not TransferEffectComponent { Transfer: { } transfer }) continue;
                if (transfer.Delivery != EImpactDelivery.Continuous) continue;

                var held = Held(spec, transfer);
                AttributeSystemComponent giver = null, getter = null;
                bool moves = transfer.Moves && Sides(spec, target, transfer, out giver, out getter);
                float amount = moves ? AmountOf(spec, target, transfer) : 0f;
                if (held != null)
                {
                    if (moves && held.Giver == giver && held.Getter == getter && Mathf.Approximately(held.Amount, amount)) continue;
                    GiveBack(held, quietly);
                    spec.TransfersLent.Remove(held);
                }
                if (!moves) continue;

                // Kept though it takes nothing, so a read of the same amount holds as it is
                var lent = new LentTransfer { Transfer = transfer, Amount = amount, Giver = giver, Getter = getter };
                (spec.TransfersLent ??= new List<LentTransfer>()).Add(lent);
                float take = Take(transfer, giver, amount);
                if (take <= 0f) continue;

                var source = spec.GetOrigin()?.GetSource();
                lent.Giving = Carrier(source, spec, transfer.FromAttribute);
                giver.TryGetValue(transfer.FromAttribute, out var before);
                Register(giver, transfer.FromAttribute, lent.Giving, Delta(transfer.Slot, -take), quietly);
                giver.TryGetValue(transfer.FromAttribute, out var after);
                var moved = Moved(transfer.Slot, before, after);

                // A floor held some back: the giver gives only what moved, so it has it all back as the effect ends
                if (!Whole(transfer.Slot, moved, take))
                    Register(giver, transfer.FromAttribute, lent.Giving, new AttributeValue(-moved.CurrentValue, -moved.BaseValue), quietly);
                if (moved.CurrentValue <= 0f && moved.BaseValue <= 0f) continue;

                lent.Getting = Carrier(source, spec, transfer.ToAttribute);
                Register(getter, transfer.ToAttribute, lent.Getting, moved, quietly);
            }
        }

        /// <summary>Gives back what the effect's lasting transfers hold, on both sides, as it ends.</summary>
        /// <param name="quietOn">An attribute system to give back on with nothing told, as one whose entity is going.</param>
        internal static void GiveBack(GameplayEffectSpec spec, bool quietly = false, AttributeSystemComponent quietOn = null)
        {
            var lent = spec?.TransfersLent;
            if (lent == null) return;
            spec.TransfersLent = null;
            for (int i = 0; i < lent.Count; i++) GiveBack(lent[i], quietly, quietOn);
        }

        private static void GiveBack(LentTransfer held, bool quietly, AttributeSystemComponent quietOn = null)
        {
            if (held.Getting != null) Unregister(held.Getter, held.Transfer.ToAttribute, held.Getting, quietly || held.Getter == quietOn);
            if (held.Giving != null) Unregister(held.Giver, held.Transfer.FromAttribute, held.Giving, quietly || held.Giver == quietOn);
        }

        private static LentTransfer Held(GameplayEffectSpec spec, AttributeTransfer transfer)
        {
            var lent = spec.TransfersLent;
            if (lent == null) return null;
            for (int i = 0; i < lent.Count; i++)
                if (ReferenceEquals(lent[i].Transfer, transfer)) return lent[i];
            return null;
        }

        // Each side's attribute system, where it has the attribute named for it. Moving an attribute to itself moves nothing
        private static bool Sides(GameplayEffectSpec spec, ITarget target, AttributeTransfer transfer,
            out AttributeSystemComponent giver, out AttributeSystemComponent getter)
        {
            getter = null;
            return SystemOf(spec, target, transfer.From, transfer.FromAttribute, out giver)
                   && SystemOf(spec, target, transfer.To, transfer.ToAttribute, out getter)
                   && !(giver == getter && transfer.FromAttribute == transfer.ToAttribute);
        }

        private static bool SystemOf(GameplayEffectSpec spec, ITarget target, NumberSide side, IAttribute attribute, out AttributeSystemComponent system)
        {
            system = null;
            var entity = side == NumberSide.Caster ? spec.GetOrigin()?.GetSource() : target;
            return entity != null && entity.FindAttributeSystem(out system) && system != null && system.TryGetValue(attribute, out _);
        }

        private static float AmountOf(GameplayEffectSpec spec, ITarget target, AttributeTransfer transfer) =>
            Mathf.Max(0f, transfer.Amount?.Evaluate(NumberContext.Of(spec).WithTarget(target)) ?? 0f);

        // What the giver gives of an amount: no more than it has of the values it moves
        private static float Take(AttributeTransfer transfer, AttributeSystemComponent giver, float amount)
        {
            if (amount <= 0f || !giver.TryGetValue(transfer.FromAttribute, out var value)) return 0f;
            float has = transfer.Slot switch
            {
                EAttributeTarget.Base => value.BaseValue,
                EAttributeTarget.CurrentAndBase => Mathf.Min(value.CurrentValue, value.BaseValue),
                _ => value.CurrentValue,
            };
            return Mathf.Min(amount, Mathf.Max(0f, has));
        }

        private static AttributeValue Delta(EAttributeTarget slot, float amount) => slot switch
        {
            EAttributeTarget.Base => new AttributeValue(0f, amount),
            EAttributeTarget.CurrentAndBase => new AttributeValue(amount, amount),
            _ => new AttributeValue(amount, 0f),
        };

        // What left the giver of the values it moves, measured; another value its bounds pulled with them isn't given
        private static AttributeValue Moved(EAttributeTarget slot, AttributeValue before, AttributeValue after)
        {
            float current = slot == EAttributeTarget.Base ? 0f : Mathf.Max(0f, before.CurrentValue - after.CurrentValue);
            float baseValue = slot == EAttributeTarget.Current ? 0f : Mathf.Max(0f, before.BaseValue - after.BaseValue);
            return new AttributeValue(current, baseValue);
        }

        private static bool Whole(EAttributeTarget slot, AttributeValue moved, float take) => slot switch
        {
            EAttributeTarget.Base => Mathf.Approximately(moved.BaseValue, take),
            EAttributeTarget.CurrentAndBase => Mathf.Approximately(moved.CurrentValue, take) && Mathf.Approximately(moved.BaseValue, take),
            _ => Mathf.Approximately(moved.CurrentValue, take),
        };

        // A side's carrier: the effect's, as its hits are, with no impact type a worker takes for a hit
        private static SourceAttributeImpact Carrier(ISource source, GameplayEffectSpec spec, IAttribute attribute) =>
            IAttributeImpactCarrier.GenerateSourceDerivation(source, attribute, Tags.IgnoreRetention,
                new List<Tag> { Tags.DisallowImpact }, spec);

        private static SourcedModifiedAttributeValue Change(ISource source, GameplayEffectSpec spec, IAttribute attribute, AttributeValue delta) =>
            new(Carrier(source, spec, attribute), delta.CurrentValue, delta.BaseValue, false);

        private static void Register(AttributeSystemComponent system, IAttribute attribute, SourceAttributeImpact carrier, AttributeValue delta, bool quietly)
        {
            var impacters = Impacters(delta);
            if (quietly) system.RegisterQuietly(attribute, carrier, impacters);
            else system.RegisterModifier(attribute, new SourcedModifiedAttributeValue(carrier, 0f, 0f, false), impacters);
        }

        private static void Unregister(AttributeSystemComponent system, IAttribute attribute, SourceAttributeImpact carrier, bool quietly)
        {
            if (system == null) return;
            if (quietly) system.UnregisterQuietly(attribute, carrier);
            else system.UnregisterModifier(attribute, new SourcedModifiedAttributeValue(carrier, 0f, 0f, false));
        }

        // A delta's Adds, a growing base before the current and a shrinking one after, as a commit's bounds need (PF-005)
        private static IReadOnlyList<IAttributeImpacter> Impacters(AttributeValue delta)
        {
            var impacters = new List<IAttributeImpacter>(2);
            var current = delta.CurrentValue != 0f ? new ConstantImpacter(delta.CurrentValue, ECalculationOperation.Add, EAttributeTarget.Current) : null;
            var baseStep = delta.BaseValue != 0f ? new ConstantImpacter(delta.BaseValue, ECalculationOperation.Add, EAttributeTarget.Base) : null;
            if (delta.BaseValue > 0f) impacters.Add(baseStep);
            if (current != null) impacters.Add(current);
            if (delta.BaseValue < 0f) impacters.Add(baseStep);
            return impacters;
        }
    }
}
