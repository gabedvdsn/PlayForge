using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An effect's own lifesteal (TW-15; FN-15, Decision 78): a share its caster's Lifesteal rises by for the effect's own
    /// hits alone, while a condition holds, as a vampiric strike's, as though the attribute rose for the hit and went back.
    /// The Lifesteal worker heals by it, the one its project gives every entity or the caster's own, so an effect with no
    /// worker to read it heals nothing.
    /// </summary>
    [Serializable]
    public class EffectLifesteal
    {
        [Tooltip("The share its caster's Lifesteal rises by for its own hits, as 0.25, which the Lifesteal worker heals by; 0, none")]
        public ScalableFloat Share = 0f;

        [Tooltip("Only while this holds, read at the hit: the caster its caster, the one hit its target")]
        [SerializeReference] public GameplayCondition When;

        /// <summary>Whether it steals at all: a share that's more than a plain 0.</summary>
        public bool Steals => Share != null && (Share.Magnitude > 0f || !AbstractScaler.IsNone(Share.Scaler));
    }

    /// <summary>
    /// A holder's lifesteal (TW-15): its holder heals a share of each loss it deals another on an attribute, of some impact
    /// types where it names any, while a condition holds: an item's while it's equipped, an ability's while it's known, an
    /// entity's as it lives, as a vampiric mask does for its wearer's attacks.
    /// </summary>
    [Serializable]
    public class LifestealEntry
    {
        [Tooltip("The share of each loss its holder deals that it heals, as 0.15")]
        public ScalableFloat Share = 0.1f;

        [Tooltip("The attribute whose losses count and its holder heals, as Health. None, the one playing the project's Health role")]
        public Attribute Attribute;

        [Tooltip("Only hits of one of these types, as attacks. None, any")]
        [ForgeTagContext(ForgeContext.ImpactType)]
        public List<Tag> ImpactTypes = new();

        [Tooltip("Only while this holds, read at the hit: the holder its caster, the one hit its target")]
        [SerializeReference] public GameplayCondition When;

        [NonSerialized] private HolderLifestealWorker worker;

        // The impact worker that runs it for its holder's hits, one for the entry, whoever holds it
        internal HolderLifestealWorker Worker => worker ??= new HolderLifestealWorker(this);

        /// <summary>The attribute whose losses count: its own, or else the Health role's in the project, or null for none.</summary>
        public Attribute StealsFrom => Attribute ? Attribute : ForgeRoles.AttributeFor(Tags.ROLE_HEALTH);
    }

    /// <summary>Gives a holder the lifesteals it has, and takes them back (TW-15).</summary>
    public static class Lifesteals
    {
        /// <summary>Gives <paramref name="holder"/> each entry's worker, which runs for the hits it deals.</summary>
        public static void Provide(List<LifestealEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0 || holder == null || !holder.FindAbilitySystem(out var abilities) || abilities == null) return;
            foreach (var entry in entries)
                if (entry?.StealsFrom != null) abilities.ProvideWorker(entry.Worker);
        }

        /// <summary>Takes back what <see cref="Provide"/> gave.</summary>
        public static void Remove(List<LifestealEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0 || holder == null || !holder.FindAbilitySystem(out var abilities) || abilities == null) return;
            foreach (var entry in entries)
                if (entry != null) abilities.RemoveWorker(entry.Worker);
        }
    }

    /// <summary>The impact worker a lifesteal entry gives its holder: it runs for the hits the holder deals (TW-15).</summary>
    internal sealed class HolderLifestealWorker : AbstractImpactWorker
    {
        private readonly LifestealEntry entry;

        // The attribute each cache took it under, so it leaves the one it joined, though the project's Health role change
        private readonly Dictionary<ImpactWorkerCache, Attribute> joined = new();

        public HolderLifestealWorker(LifestealEntry entry) => this.entry = entry;

        public override bool PreValidateWorkFor(ImpactData impactData) =>
            entry.StealsFrom is { } attribute && impactData.Attribute != null && impactData.Attribute.Equals(attribute);

        public override bool ValidateWorkFor(ImpactData impactData)
        {
            if (!ForgeLifesteal.Counts(impactData)) return false;
            if (entry.ImpactTypes is { Count: > 0 })
            {
                var types = impactData.SourcedModifier.Carrier?.GetImpactTypes();
                if (types == null || !ForgeHelper.ValidateImpactTypes(false, types, entry.ImpactTypes)) return false;
            }
            return true;
        }

        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            var context = NumberContext.Of(in ctx);
            if (entry.When != null && !entry.When.Holds(context)) yield break;
            var attribute = entry.StealsFrom;
            float amount = ForgeLifesteal.HealFor(ctx.ImpactData, attribute, entry.Share?.Evaluate(context) ?? 0f);
            if (amount <= 0f || ctx.System is not ITarget healer) yield break;
            yield return ForgeLifesteal.Heal(healer, attribute, amount);
        }

        public override void SubscribeToCache(ImpactWorkerCache cache)
        {
            var attribute = entry.StealsFrom;
            if (attribute == null) return;
            joined[cache] = attribute;
            cache.ProvideWorker(attribute, this);
        }

        public override void UnsubscribeFromCache(ImpactWorkerCache cache)
        {
            if (!joined.Remove(cache, out var attribute)) attribute = entry.StealsFrom;
            if (attribute != null) cache.RemoveWorker(attribute, this);
        }
    }

    /// <summary>
    /// The Lifesteal worker (FN-15, Decision 78): it heals its holder a share of each workable loss it deals another, the
    /// value of its holder's Lifesteal, the attribute playing the project's Lifesteal role or the one it names; and for an
    /// effect's own hits, that effect's Lifesteal besides, as though the attribute rose for the hit and went back. Only
    /// hits of the impact types it names count, as attacks, so a second worker on another attribute makes a spell vamp.
    /// A project gives it to every entity through its Forge Project's Workers, and an entity can hold one of its own;
    /// one that holds two heals a hit once. It allocates nothing, healing or not.
    /// </summary>
    [Serializable]
    public class LifestealImpactWorker : AbstractImpactWorker
    {
        [Tooltip("The attribute whose value is the share it heals, as 0.15. None, the one playing the project's Lifesteal role")]
        public Attribute Share;

        [Tooltip("The attribute whose losses count and it heals, as Health. None, the one playing the project's Health role")]
        public Attribute Heals;

        [Tooltip("Only hits of one of these types, as attacks. None, any")]
        [ForgeTagContext(ForgeContext.ImpactType)]
        public List<Tag> ImpactTypes = new();

        /// <summary>An entity that holds two, its project's and its own, heals a hit once.</summary>
        public override bool OncePerHit => true;

        // What it keeps for its heals as it runs: a carrier for each healer, the actions it queues, back here once the queue
        // has run them, and the list it hands one back in, as the relative-operation worker's lifesteal does (GAS-P9)
        [NonSerialized] private SourceCarriers carriers;
        [NonSerialized] private Stack<ModifyAttributeAction> spareActions;
        [NonSerialized] private IRootAction[] oneAction;

        /// <summary>The attribute whose value is its share: its own, or else the Lifesteal role's, or null for none.</summary>
        public Attribute ShareAttribute => Share ? Share : ForgeRoles.AttributeFor(Tags.ROLE_LIFESTEAL);

        /// <summary>The attribute whose losses count and it heals: its own, or else the Health role's, or null for none.</summary>
        public Attribute HealsAttribute => Heals ? Heals : ForgeRoles.AttributeFor(Tags.ROLE_HEALTH);

        public override bool PreValidateWorkFor(ImpactData impactData) =>
            HealsAttribute is { } heals && impactData.Attribute != null && impactData.Attribute.Equals(heals);

        // A hit it heals for: a workable loss dealt another, of its types, with a share to heal; one it doesn't heal for
        // makes no action
        public override bool ValidateWorkFor(ImpactData impactData)
        {
            if (!ForgeLifesteal.Counts(impactData)) return false;
            if (ImpactTypes is { Count: > 0 })
            {
                var types = impactData.SourcedModifier.Carrier?.GetImpactTypes();
                if (types == null || !ForgeHelper.ValidateImpactTypes(false, types, ImpactTypes)) return false;
            }
            return ShareOf(impactData) > 0f;
        }

        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            var impact = ctx.ImpactData;
            var heals = HealsAttribute;
            float amount = ForgeLifesteal.HealFor(impact, heals, ShareOf(impact));
            if (amount <= 0f || ctx.System is not ITarget healer || ctx.System is not ISource source) return Array.Empty<IRootAction>();

            // A workable gain, so what scales its holder's heals scales it (TW-15)
            var carrier = (carriers ??= new SourceCarriers()).For(source, heals, Tags.IgnoreRetention, null);
            oneAction ??= new IRootAction[1];
            oneAction[0] = ModifyAttributeAction.Rent(spareActions ??= new Stack<ModifyAttributeAction>(), healer, heals,
                new SourcedModifiedAttributeValue(carrier, amount, 0f, true), true);
            return oneAction;
        }

        /// <summary>
        /// The share it heals for a hit: its dealer's Lifesteal, and for an effect's own hits that effect's Lifesteal
        /// besides, where its condition holds.
        /// </summary>
        public float ShareOf(in ImpactData impact)
        {
            float share = 0f;
            var dealer = impact.SourcedModifier.Source;
            if (dealer != null && ShareAttribute is { } attribute && dealer.TryGetAttributeValue(attribute, out var value))
                share += value.CurrentValue;
            if (impact.SourcedModifier.Carrier is GameplayEffectSpec spec && spec.Base?.Lifesteal is { Steals: true } own)
            {
                var context = NumberContext.Of(spec).WithTarget(impact.Target);
                if (own.When == null || own.When.Holds(context)) share += own.Share.Evaluate(context);
            }
            return share;
        }

        // Under the attribute whose losses count as it joins; it leaves the one that counts as it leaves
        public override void SubscribeToCache(ImpactWorkerCache cache)
        {
            if (HealsAttribute is { } heals) cache.ProvideWorker(heals, this);
        }

        public override void UnsubscribeFromCache(ImpactWorkerCache cache)
        {
            if (HealsAttribute is { } heals) cache.RemoveWorker(heals, this);
        }
    }

    /// <summary>Lifesteal's one rule, an effect's and a holder's (TW-15).</summary>
    public static class ForgeLifesteal
    {
        /// <summary>
        /// Whether a hit counts for lifesteal: a workable loss its dealer dealt another, not returned damage.
        /// </summary>
        public static bool Counts(ImpactData impact)
        {
            var dealer = impact.SourcedModifier.Source;
            if (!impact.SourcedModifier.ImpactIsWorkable || dealer == null || ReferenceEquals(dealer, impact.Target)) return false;
            return impact.RealImpact.CurrentValue < 0f && !Returned(impact.SourcedModifier.Carrier);
        }

        /// <summary>The heal a hit gives its dealer for a share, where it counts, of a loss to the attribute.</summary>
        public static float HealFor(ImpactData impact, IAttribute attribute, float share)
        {
            if (attribute == null || share <= 0f || impact.Attribute == null || !impact.Attribute.Equals(attribute) || !Counts(impact)) return 0f;
            return -impact.RealImpact.CurrentValue * share;
        }

        /// <summary>The heal itself: a workable gain to the healer, so what scales its heals scales it (TW-15).</summary>
        public static IRootAction Heal(ITarget healer, IAttribute attribute, float amount) => new LambdaAction(
            _ => healer.TryModifyAttribute(attribute, SourcedModifiedAttributeValue.GenerateSimple(healer as ISource, attribute, amount, 0f)),
            $"Lifesteal {amount:0.##} {attribute.GetName()}");

        /// <summary>Whether a change is returned damage, which no return returns again and no lifesteal heals from.</summary>
        public static bool Returned(IAttributeImpactCarrier carrier)
        {
            var types = carrier?.GetImpactTypes();
            if (types == null) return false;
            for (int i = 0; i < types.Count; i++)
                if (types[i].Equals(Tags.RETURNED)) return true;
            return false;
        }
    }
}
