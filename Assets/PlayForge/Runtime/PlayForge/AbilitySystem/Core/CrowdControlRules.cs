using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What a crowd control rule stops (TW-16).</summary>
    public enum ECrowdControlStops
    {
        /// <summary>Every ability, as a stun does.</summary>
        EveryAbility,

        /// <summary>The abilities carrying one of its tags, as Silenced stops spells and Disarmed attacks.</summary>
        AbilitiesTagged,
    }

    /// <summary>One crowd control rule (TW-16): while its holder carries a tag, it can't use what the rule stops.</summary>
    [Serializable]
    public class CrowdControlRule
    {
        [Tooltip("The tag its holder carries, as Stunned")]
        public Tag Tag;

        [Tooltip("What it stops: every ability, or those carrying one of its tags")]
        public ECrowdControlStops Stops = ECrowdControlStops.EveryAbility;

        [Tooltip("The abilities it stops, by an asset or context tag of theirs, or one under it, as Spell")]
        public List<Tag> AbilityTags = new();
    }

    /// <summary>
    /// Crowd control (TW-16, Decision 54): the rules an entity's abilities follow, each a tag and what it stops, so no
    /// ability has to list Stunned among the tags it avoids. While its holder carries a rule's tag, an ability the rule
    /// stops is refused, and one running is interrupted, as a stun breaks a channel; an ability can ignore a rule by its tag.
    /// An entity names its rules, or takes the project's.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Crowd Control Rules", fileName = "Crowd Control Rules", order = 44)]
    public class CrowdControlRules : ScriptableObject
    {
        [Tooltip("Each rule: a tag its holder carries, and what that stops")]
        public List<CrowdControlRule> Rules = new();

        /// <summary>
        /// The first rule that stops <paramref name="ability"/> for a holder with <paramref name="tags"/>, or null: the holder
        /// carries the rule's tag, or one under it; the ability doesn't ignore it; and the rule stops every ability, or
        /// those carrying one of its tags, which the ability does. Allocates nothing.
        /// </summary>
        public CrowdControlRule Stopping(Ability ability, TagCache tags)
        {
            if (ability == null || tags == null || Rules == null) return null;
            var ignores = ability.Definition?.IgnoresCrowdControl;
            for (int i = 0; i < Rules.Count; i++)
            {
                var rule = Rules[i];
                if (rule == null || !rule.Tag.IsValid || !tags.HasTag(rule.Tag, ETagMatchMode.IncludeChildren)) continue;
                if (Ignores(ignores, rule.Tag)) continue;
                if (rule.Stops == ECrowdControlStops.EveryAbility || AbilityTags.Names(rule.AbilityTags, ability)) return rule;
            }
            return null;
        }

        // Whether an ability ignores the rule's tag, by it or a tag above it, so ignoring a parent ignores all under it
        private static bool Ignores(List<Tag> ignores, Tag tag)
        {
            if (ignores == null) return false;
            for (int i = 0; i < ignores.Count; i++)
                if (ignores[i].IsValid && tag.MatchesOrIsChildOf(ignores[i])) return true;
            return false;
        }
    }
}
