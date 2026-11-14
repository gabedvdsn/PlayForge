using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What an entity's death does (TW-4).</summary>
    public enum EDeathPolicy
    {
        /// <summary>It ends, as an entity always did: its hub ends it at its frame's end.</summary>
        End,

        /// <summary>
        /// It stays, dead, until it revives: still registered, its items, levels, cooldowns and lasting effects kept, but
        /// for those its death ends; its abilities refused; its body gone.
        /// </summary>
        Stay,
    }

    /// <summary>
    /// What an entity does as it dies and as it revives (TW-4, the user's request of 2026-11-11): a MOBA hero, or anyone
    /// who dies and comes back, keeps its entity while its body goes. Where it stays: the effects its death ends, by tag,
    /// as buffs; the effects it takes as it dies, as a respawn timer; the effects it takes as it revives, as its health
    /// back; and its body, a MonoBehaviour process it spawns where it is, as it starts and as it revives, and ends as it
    /// dies. A runtime entity takes its position from its body (Decision 46).
    /// </summary>
    [Serializable]
    public class EntityDeath
    {
        [Tooltip("End, as an entity always did; or Stay, dead, until it revives")]
        public EDeathPolicy Policy = EDeathPolicy.End;

        [Tooltip("Dies as the attribute playing the project's Health role reaches 0, where no death watcher of its own " +
                 "watches for its death. Off, as for a training dummy, that attribute doesn't kill it (TW-28, TW-29)")]
        public bool DiesAtZeroHealth = true;

        [Tooltip("Where it stays: as it dies, it purges its effects, all but those it keeps, as the games' deaths do; off, it " +
                 "keeps them all (FN-16)")]
        public bool Purges = true;

        [Tooltip("It keeps what its project's Purge Levels keep on death, their tiers and categories, beside its own below; " +
                 "with no project, cooldowns, lent effects and toggles")]
        public bool KeepsAsProject = true;

        [Tooltip("The tiers it keeps besides, by their tags in its project's Purge Levels")]
        [ForgeTagContext(ForgeContext.ContextTag, ForgeContext.AssetTag)]
        public List<Tag> KeepsTiers = new();

        [Tooltip("The categories it keeps besides, whatever their effects' tiers: an ability's cooldown, what an item, an aura, " +
                 "a set bonus or a passive lends, a toggle, its own")]
        public EEffectCategory KeepsCategories = EEffectCategory.None;

        [Tooltip("Where it stays: the effects it takes as it dies, as a respawn timer, whose end revives it " +
                 "(Revive On Removal)")]
        public List<GameplayEffect> OnDeath = new();

        [Tooltip("The effects it takes as it revives, as its health back")]
        public List<GameplayEffect> OnRevive = new();

        [Tooltip("Its body: a MonoBehaviour process it spawns where it is as it starts and as it revives, and ends as it " +
                 "dies. A runtime entity takes its position from it")]
        public AbstractMonoProcess Body;

        /// <summary>Whether an entity with this death stays when it dies.</summary>
        public static bool Stays(EntityDeath death) => death != null && death.Policy == EDeathPolicy.Stay;
    }
}
