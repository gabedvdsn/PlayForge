using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A holder's lifesteal entry as a save before FN-15 kept it, in an item's, an ability's or an entity's Lifesteal list,
    /// which heals no more: a holder's lifesteal is its Lifesteal attribute now, which an effect on it raises, and the
    /// Lifesteal worker heals by it (Decision 78). Kept so an older save's list loads, for the Problems view to name.
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
    }

    /// <summary>
    /// The Lifesteal worker (FN-15, Decision 78): it heals its holder a share of each workable loss it deals another, the
    /// value of its holder's Lifesteal, the attribute playing the project's Lifesteal role or the one it names, which an
    /// effect on the holder raises while it should. An effect has no lifesteal of its own (FN-22). Only hits of the impact
    /// types it names count, as attacks, so a second worker on another attribute makes a spell vamp.
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
        /// The share it heals for a hit: its dealer's Lifesteal. An effect's own Lifesteal added to it for that effect's
        /// hits, which an effect raising its caster's Lifesteal while it should does now (FN-22).
        /// </summary>
        public float ShareOf(in ImpactData impact)
        {
            var dealer = impact.SourcedModifier.Source;
            return dealer != null && ShareAttribute is { } attribute && dealer.TryGetAttributeValue(attribute, out var value)
                ? value.CurrentValue
                : 0f;
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
