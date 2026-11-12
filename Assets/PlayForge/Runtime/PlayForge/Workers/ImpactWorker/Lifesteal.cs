using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An effect's own lifesteal (TW-15, Decision 53): its caster heals a share of each loss the effect deals another on an
    /// attribute, while a condition holds, as a vampiric strike does.
    /// </summary>
    [Serializable]
    public class EffectLifesteal
    {
        [Tooltip("The share of each loss it deals that its caster heals, as 0.25; 0, none")]
        public ScalableFloat Share = 0f;

        [Tooltip("The attribute whose losses count and its caster heals, as Health. None, the one playing the project's Health role")]
        public Attribute Attribute;

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
        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactTypes = new();

        [Tooltip("Only while this holds, read at the hit: the holder its caster, the one hit its target")]
        [SerializeReference] public GameplayCondition When;

        [NonSerialized] private LifestealImpactWorker worker;

        // The impact worker that runs it for its holder's hits, one for the entry, whoever holds it
        internal LifestealImpactWorker Worker => worker ??= new LifestealImpactWorker(this);

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

    /// <summary>The impact worker a lifesteal gives its holder: it runs for the hits the holder deals (TW-15).</summary>
    internal sealed class LifestealImpactWorker : AbstractImpactWorker
    {
        private readonly LifestealEntry entry;

        // The attribute each cache took it under, so it leaves the one it joined, though the project's Health role change
        private readonly Dictionary<ImpactWorkerCache, Attribute> joined = new();

        public LifestealImpactWorker(LifestealEntry entry) => this.entry = entry;

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

        /// <summary>
        /// An effect's own lifesteal for a hit it dealt: its caster heals a share of the loss, where its condition holds.
        /// </summary>
        public static void Steal(IGameplayAbilitySystem caster, ImpactData impact, EffectLifesteal steal)
        {
            if (caster is not ITarget healer || steal == null) return;
            var attribute = steal.Attribute ? steal.Attribute : ForgeRoles.AttributeFor(Tags.ROLE_HEALTH);
            var context = NumberContext.Of(impact.SourcedModifier.Carrier).WithTarget(impact.Target);
            if (steal.When != null && !steal.When.Holds(context)) return;
            float amount = HealFor(impact, attribute, steal.Share?.Evaluate(context) ?? 0f);
            if (amount > 0f) caster.GetActionQueue()?.Enqueue(Heal(healer, attribute, amount));
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
