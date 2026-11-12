using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Whom an On Hit's effects land on (TW-6).</summary>
    public enum EOnHitTo
    {
        /// <summary>The one hit.</summary>
        Target,

        /// <summary>The holder, who dealt the hit, as a barrier on a kill.</summary>
        Holder,
    }

    /// <summary>
    /// Effects that an item, an ability or an effect applies as its holder's hits land, or on the hit that kills (TW-6):
    /// Desolator's armor, a dagger's bleed, a boon's Weak, a barrier on a kill. A hit is a loss to its Attribute, as Health,
    /// that the holder deals another entity: only of its impact types, where it names any, so a bleed's ticks, typed
    /// apart from attacks, set off nothing; and only while its condition holds. Each effect's own Chance to Apply rolls as
    /// it lands, and its hits set the entry off no more, so a shock it applies doesn't shock again. An item's hold while
    /// it's equipped, an ability's while it's known, an effect's while it lasts.
    /// </summary>
    [Serializable]
    public class OnHitEntry
    {
        [Tooltip("The attribute a hit lowers, as Health")]
        public Attribute Attribute;

        [Tooltip("The effects it applies")]
        public List<GameplayEffect> Effects = new();

        [Tooltip("Whom they land on: the one hit, or the holder, as a barrier on a kill")]
        public EOnHitTo To = EOnHitTo.Target;

        [Tooltip("Only on the hit that takes the attribute from above 0 to 0 or below: On Kill")]
        public bool OnlyOnKill;

        [Tooltip("Only hits of one of these types, as attacks. None, any")]
        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactTypes = new();

        [Tooltip("Only while this holds, read at the hit: the holder its caster, the one hit its target")]
        [SerializeReference] public GameplayCondition When;

        [NonSerialized] private OnHitImpactWorker worker;

        // The impact worker that runs it for its holder's hits, one for the entry, whoever holds it
        internal OnHitImpactWorker Worker => worker ??= new OnHitImpactWorker(this);
    }

    /// <summary>Gives a holder the On Hits it has, and takes them back (TW-6).</summary>
    public static class OnHits
    {
        /// <summary>Gives <paramref name="holder"/> each entry's worker, which runs for the hits it deals.</summary>
        public static void Provide(List<OnHitEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0 || holder == null || !holder.FindAbilitySystem(out var abilities) || abilities == null) return;
            foreach (var entry in entries)
                if (entry?.Attribute != null) abilities.ProvideWorker(entry.Worker);
        }

        /// <summary>Takes back what <see cref="Provide"/> gave.</summary>
        public static void Remove(List<OnHitEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0 || holder == null || !holder.FindAbilitySystem(out var abilities) || abilities == null) return;
            foreach (var entry in entries)
                if (entry?.Attribute != null) abilities.RemoveWorker(entry.Worker);
        }
    }

    /// <summary>The impact worker an On Hit gives its holder: it runs for the hits the holder deals (TW-6).</summary>
    internal sealed class OnHitImpactWorker : AbstractImpactWorker
    {
        private readonly OnHitEntry entry;

        public OnHitImpactWorker(OnHitEntry entry) => this.entry = entry;

        public override bool PreValidateWorkFor(ImpactData impactData) =>
            entry.Attribute != null && impactData.Attribute != null && impactData.Attribute.Equals(entry.Attribute);

        public override bool ValidateWorkFor(ImpactData impactData)
        {
            var dealer = impactData.SourcedModifier.Source;
            if (dealer == null || ReferenceEquals(dealer, impactData.Target)) return false;
            if (impactData.RealImpact.CurrentValue >= 0f) return false;

            // Its own effects' hits set it off no more, so a shock it applies doesn't shock again, and again
            var carrier = impactData.SourcedModifier.Carrier;
            if (carrier is GameplayEffectSpec spec && entry.Effects != null && entry.Effects.Contains(spec.Base)) return false;

            if (entry.ImpactTypes is { Count: > 0 })
            {
                var types = carrier?.GetImpactTypes();
                if (types == null || !ForgeHelper.ValidateImpactTypes(false, types, entry.ImpactTypes)) return false;
            }
            return !entry.OnlyOnKill || (impactData.OldValue.CurrentValue > 0f && impactData.LiveValue.CurrentValue <= 0f);
        }

        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            var actions = new List<IRootAction>();
            if (entry.Effects == null || entry.Effects.Count == 0) return actions;
            if (entry.When != null && !entry.When.Holds(NumberContext.Of(in ctx))) return actions;

            var holder = ctx.System;
            var onto = entry.To == EOnHitTo.Holder ? holder as ITarget : ctx.ImpactData.Target;
            if (holder == null || onto == null) return actions;
            var origin = IEffectOrigin.GenerateSourceDerivation(holder);
            foreach (var effect in entry.Effects)
                if (effect != null) actions.Add(new ApplyEffectAction(onto, effect, origin));
            return actions;
        }

        public override void SubscribeToCache(ImpactWorkerCache cache) => cache.ProvideWorker(entry.Attribute, this);

        public override void UnsubscribeFromCache(ImpactWorkerCache cache) => cache.RemoveWorker(entry.Attribute, this);
    }
}
