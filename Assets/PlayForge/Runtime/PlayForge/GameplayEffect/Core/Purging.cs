using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>A tier of a project's purge levels (FN-16): the tag an effect carries to be at it, and whether a death keeps it.</summary>
    [Serializable]
    public class PurgeTier
    {
        [Tooltip("The tag an effect carries, as its asset tag or a context tag, to be at this tier")]
        [ForgeTagContext(ForgeContext.ContextTag, ForgeContext.AssetTag)]
        public Tag Tag;

        [Tooltip("A death keeps the effects at this tier, where its Death keeps what its project does")]
        public bool KeptOnDeath;
    }

    /// <summary>The kinds of effect a death can keep whatever their tiers (FN-16, Decision 80).</summary>
    [Flags]
    public enum EEffectCategory
    {
        None = 0,

        /// <summary>An ability's cooldown on its caster.</summary>
        Cooldown = 1,

        /// <summary>
        /// One its source lends while it lasts: an equipped item's, an aura's, a set bonus's, or a passive ability's on its
        /// holder, an ability that activates as it's granted.
        /// </summary>
        Lent = 2,

        /// <summary>The effect an ability toggled on its caster.</summary>
        Toggle = 4,

        /// <summary>One the entity applied to itself.</summary>
        Own = 8,
    }

    /// <summary>Whom a purge takes the effects of, by who applied each, against the one purged (FN-16, Decision 80).</summary>
    [Flags]
    public enum EPurgeRelation
    {
        None = 0,

        /// <summary>Effects the one purged applied to itself.</summary>
        Self = 1,

        /// <summary>Effects its allies applied: its buffs, as a foe's purge takes.</summary>
        Allies = 2,

        /// <summary>Effects its enemies applied: its debuffs, as a friend's purge takes.</summary>
        Enemies = 4,

        Any = Self | Allies | Enemies,
    }

    /// <summary>
    /// A project's purge levels (FN-16, Decision 79): tiers of tags, weakest first, an effect at the first whose tag its
    /// asset tag or a context tag matches; a purge of a tier takes the effects at it or under it, and an effect at none is
    /// never purged, but by death (Decision 80). And the categories a death keeps whatever their tiers.
    /// </summary>
    [Serializable]
    public class PurgeLevels
    {
        [Tooltip("The tiers, weakest first: an effect is at the first whose tag it carries, as its asset tag or a context tag, " +
                 "and a purge of a tier takes those at it or under it. One at none is never purged, but by death")]
        public List<PurgeTier> Tiers = new();

        [Tooltip("The categories a death keeps whatever their effects' tiers, where its Death keeps what its project does: an " +
                 "ability's cooldown, what an item, an aura, a set bonus or a passive lends, a toggle, the entity's own")]
        public EEffectCategory KeptOnDeath = EEffectCategory.Cooldown | EEffectCategory.Lent | EEffectCategory.Toggle;

        /// <summary>The tier of a tag, its place among the tiers, or -1 where no tier is it.</summary>
        public int TierOf(Tag tag)
        {
            if (!tag.IsValid || Tiers == null) return -1;
            for (int i = 0; i < Tiers.Count; i++)
                if (Tiers[i] != null && Tiers[i].Tag.IsValid && tag.MatchesOrIsChildOf(Tiers[i].Tag)) return i;
            return -1;
        }

        /// <summary>The tier an effect is at: the first whose tag its asset tag or a context tag matches, or -1 for none.</summary>
        public int TierOf(GameplayEffect effect)
        {
            if (effect == null || Tiers == null) return -1;
            for (int i = 0; i < Tiers.Count; i++)
                if (Tiers[i] != null && Tiers[i].Tag.IsValid && ForgePurge.Carries(effect, Tiers[i].Tag)) return i;
            return -1;
        }
    }

    /// <summary>
    /// A purge (FN-16, Decision 79): its strength, a tier of the project's purge levels, taking the effects at that tier or
    /// under it; only those of the kinds it names, by their tags, as Status.Debuff; only those applied by whom it says,
    /// against the one purged; and only those its filters take.
    /// </summary>
    [Serializable]
    public class EffectPurge
    {
        [Tooltip("Its strength, a tier's tag in the project's Purge Levels: it takes the effects at that tier or under it. None, it purges nothing")]
        [ForgeTagContext(ForgeContext.ContextTag, ForgeContext.AssetTag)]
        public Tag Strength;

        [Tooltip("Only effects carrying one of these, as their asset tag or a context tag, as Status.Debuff. None, any")]
        [ForgeTagContext(ForgeContext.AssetTag, ForgeContext.ContextTag)]
        public List<Tag> Kinds = new();

        [Tooltip("Only effects applied by these, against the one purged: itself, its allies (its buffs) or its enemies (its debuffs)")]
        public EPurgeRelation AppliedBy = EPurgeRelation.Any;

        [Tooltip("Filters of its own an effect must pass to be taken, as one that spares the tags it names")]
        [SerializeReference]
        public List<AbstractPurgeFilter> Filters = new();

        /// <summary>Whether it purges at all: a strength named.</summary>
        public bool Purges => Strength.IsValid;
    }

    /// <summary>What a purge's filter reads an effect with (FN-16).</summary>
    public readonly struct PurgeContext
    {
        /// <summary>The effect as it's on the one purged.</summary>
        public readonly AbstractEffectContainer Container;

        /// <summary>The one purged.</summary>
        public readonly IGameplayAbilitySystem Purged;

        /// <summary>Who purges, where an effect or code names one.</summary>
        public readonly ISource Purger;

        public PurgeContext(AbstractEffectContainer container, IGameplayAbilitySystem purged, ISource purger)
        {
            Container = container;
            Purged = purged;
            Purger = purger;
        }
    }

    /// <summary>A purge's filter of a project's own (FN-16): whether the purge takes an effect, beside its strength, kinds and relation.</summary>
    [Serializable]
    public abstract class AbstractPurgeFilter
    {
        /// <summary>Whether the purge takes the effect; every one, by default.</summary>
        public virtual bool Takes(in PurgeContext ctx) => true;
    }

    /// <summary>Spares the effects carrying one of its tags, as their asset tag or a context tag, as a purge that leaves stuns (FN-16).</summary>
    [Serializable]
    public sealed class ExceptTaggedPurgeFilter : AbstractPurgeFilter
    {
        [Tooltip("Effects carrying one of these, as their asset tag or a context tag, are spared")]
        [ForgeTagContext(ForgeContext.AssetTag, ForgeContext.ContextTag)]
        public List<Tag> Tags = new();

        public override bool Takes(in PurgeContext ctx) =>
            ctx.Container?.Spec?.Base is not { } effect || Tags == null || !ForgePurge.CarriesAny(effect, Tags);
    }

    /// <summary>
    /// What purges an entity's effects (FN-16, Decision 79): those a purge takes, by the current project's purge levels,
    /// each coming off as a removal does.
    /// </summary>
    public static class ForgePurge
    {
        /// <summary>The current project's purge levels, or null where no project is current.</summary>
        public static PurgeLevels Levels => ForgeProject.Current?.Settings?.PurgeLevels;

        /// <summary>
        /// Purges <paramref name="purged"/> by <paramref name="purge"/>: each effect on it at a tier at or under the purge's
        /// strength, of its kinds, applied by whom it says and through its filters, comes off. How many came off.
        /// </summary>
        public static int Purge(IGameplayAbilitySystem purged, EffectPurge purge, ISource purger = null) => Purge(purged, purge, purger, null);

        /// <summary>Purges by a strength alone, every kind, as <see cref="Purge(IGameplayAbilitySystem, EffectPurge, ISource)"/>.</summary>
        public static int Purge(IGameplayAbilitySystem purged, Tag strength, ISource purger = null) =>
            Purge(purged, new EffectPurge { Strength = strength }, purger, null);

        // The effect doing the purge, where one does, is never purged by itself
        internal static int Purge(IGameplayAbilitySystem purged, EffectPurge purge, ISource purger, GameplayEffect sparing)
        {
            if (purged?.GetEffectSystem() is not { } effects || purge is not { Purges: true } || Levels is not { } levels) return 0;
            int strength = levels.TierOf(purge.Strength);
            if (strength < 0) return 0;
            return effects.Purge(container => !ReferenceEquals(container.Spec?.Base, sparing) && Takes(container, purge, strength, levels, purged, purger));
        }

        // Whether a purge takes an effect: at a tier at or under its strength, of its kinds, applied by whom it takes,
        // through each of its filters. One at no tier, never (Decision 80)
        private static bool Takes(AbstractEffectContainer container, EffectPurge purge, int strength, PurgeLevels levels,
            IGameplayAbilitySystem purged, ISource purger)
        {
            var effect = container?.Spec?.Base;
            if (effect == null) return false;
            int tier = levels.TierOf(effect);
            if (tier < 0 || tier > strength) return false;
            if (purge.Kinds is { Count: > 0 } && !CarriesAny(effect, purge.Kinds)) return false;
            if (purge.AppliedBy != EPurgeRelation.Any && (RelationOf(container, purged) & purge.AppliedBy) == 0) return false;
            if (purge.Filters != null)
            {
                var ctx = new PurgeContext(container, purged, purger);
                foreach (var filter in purge.Filters)
                    if (filter != null && !filter.Takes(in ctx)) return false;
            }
            return true;
        }

        /// <summary>
        /// Who applied an effect, against the one it's on: itself; an ally, sharing a side with it; or an enemy, sharing
        /// none, as one of no sides does (Decision 80).
        /// </summary>
        public static EPurgeRelation RelationOf(AbstractEffectContainer container, IGameplayAbilitySystem on)
        {
            var source = container?.Spec?.Origin?.GetSource();
            if (source == null || ReferenceEquals(source, on)) return EPurgeRelation.Self;
            var mine = on?.GetAffiliation();
            var theirs = (source as ITarget)?.GetAffiliation();
            if (mine == null || theirs == null) return EPurgeRelation.Enemies;
            for (int i = 0; i < mine.Count; i++)
                if (theirs.Contains(mine[i])) return EPurgeRelation.Allies;
            return EPurgeRelation.Enemies;
        }

        /// <summary>Whether an effect carries a tag, or a child of it, as its asset tag or a context tag (Decision 80).</summary>
        public static bool Carries(GameplayEffect effect, Tag tag)
        {
            if (effect == null || !tag.IsValid) return false;
            if (effect.Tags.AssetTag.MatchesOrIsChildOf(tag)) return true;
            var context = effect.Tags.ContextTags;
            if (context == null) return false;
            for (int i = 0; i < context.Count; i++)
                if (context[i].MatchesOrIsChildOf(tag)) return true;
            return false;
        }

        /// <summary>Whether an effect carries any of the tags, as <see cref="Carries"/> reads each.</summary>
        public static bool CarriesAny(GameplayEffect effect, List<Tag> tags)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++)
                if (Carries(effect, tags[i])) return true;
            return false;
        }
    }
}
