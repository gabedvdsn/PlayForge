using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Which of an attribute's retention groups a read takes (TW-3).</summary>
    public enum ERetentionGroupRead
    {
        /// <summary>Every group: the attribute's whole value.</summary>
        All,

        /// <summary>The groups named, each with the groups under it.</summary>
        OnlyThese,

        /// <summary>Every group but the ones named.</summary>
        AllButThese,
    }

    /// <summary>
    /// Which retention groups' parts an attribute read takes (TW-3, the user's note of 2026-11-11). An attribute's value
    /// is the sum of its groups' parts: the base its set gives, what items add in their group, what a buff adds in its.
    /// × Attribute, an Attribute condition, a Math worker's Relative To and a Threshold worker read the whole value
    /// unless they say otherwise: only these groups, as a bonus of the items' strength reads their group; or all but
    /// these, as a bonus of a hero's own damage leaves its items' out. A group names the groups under it too. Group Impact
    /// reads one group's part, as it did.
    /// </summary>
    [Serializable]
    public class RetentionGroups
    {
        [Tooltip("Which groups' parts it reads: all of them, the whole value; only these; or all but these")]
        public ERetentionGroupRead Read = ERetentionGroupRead.All;

        [Tooltip("The groups, each with the groups under it")]
        [ForgeTagContext(ForgeContext.RetentionGroup)]
        [ForgeShowWhen(nameof(Read), ERetentionGroupRead.OnlyThese, ERetentionGroupRead.AllButThese)]
        public List<Tag> Groups = new();

        /// <summary>Whether a read reads the whole value: all of it, or no group, or none named to leave out.</summary>
        public static bool ReadsAll(RetentionGroups groups) =>
            groups == null || groups.Read == ERetentionGroupRead.All
                           || (groups.Read == ERetentionGroupRead.AllButThese && (groups.Groups == null || groups.Groups.Count == 0));

        /// <summary>The part of <paramref name="cached"/> it reads.</summary>
        public AttributeValue Of(CachedAttributeValue cached)
        {
            if (cached == null) return default;
            if (ReadsAll(this)) return cached.ActiveValue;
            var named = cached.GetGroupsValue(Groups);
            return Read == ERetentionGroupRead.OnlyThese ? named : cached.ActiveValue - named;
        }

        /// <summary>
        /// The part of <paramref name="entity"/>'s attribute that <paramref name="groups"/> reads, or its whole value where
        /// there's none to say; false where the entity hasn't the attribute.
        /// </summary>
        public static bool TryRead(RetentionGroups groups, ITarget entity, IAttribute attribute, out AttributeValue value)
        {
            value = default;
            if (entity == null || attribute == null) return false;
            if (ReadsAll(groups)) return entity.TryGetAttributeValue(attribute, out value);
            if (!entity.FindAttributeSystem(out var attributes) || !attributes.TryGetCachedValue(attribute, out CachedAttributeValue cached))
                return false;
            value = groups.Of(cached);
            return true;
        }

        /// <summary>How a read says its groups, after the attribute's name: nothing for all of them, " (Bonus)", " (all but Bonus)".</summary>
        public static string Describe(RetentionGroups groups)
        {
            if (ReadsAll(groups)) return "";
            var names = new List<string>();
            foreach (var tag in groups.Groups) if (tag.IsValid) names.Add(tag.ToString());
            if (names.Count == 0) return groups.Read == ERetentionGroupRead.OnlyThese ? " (no group)" : "";
            string list = string.Join(", ", names);
            return groups.Read == ERetentionGroupRead.OnlyThese ? $" ({list})" : $" (all but {list})";
        }
    }
}
