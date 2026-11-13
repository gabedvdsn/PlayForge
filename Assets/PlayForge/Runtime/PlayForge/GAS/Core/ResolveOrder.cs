using System;
using System.Collections.Generic;
using UnityEngine;
using FarEmerald.PlayForge.Extended;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One entry of a project's resolve order (TW-31, Decision 60): those carrying its tag, or one under it, resolve at its
    /// Order among an area's hits, lower first.
    /// </summary>
    [Serializable]
    public class ResolveOrderEntry
    {
        [Tooltip("The tag an entity carries, or one under it, as a totem's, or one an aura grants its holder while it holds")]
        [ForgeTagContext(ForgeContext.Granted, ForgeContext.AssetTag)]
        public Tag Tag;

        [Tooltip("Where those carrying it resolve among an area's hits, lower first; one carrying none of the entries' tags is at 0")]
        public int Order;
    }

    /// <summary>
    /// The project's resolve order (TW-31, Decision 60): the order an area's effects land on those it takes, by the tags
    /// they carry, so specific behaviour can be coded. An ability's Apply Effects and Apply/Remove Effects, an effect's
    /// Area, with its target among those it takes, and an aura's check follow it; code of your own can too.
    /// </summary>
    public static class ForgeResolveOrder
    {
        /// <summary>The current project's entries, or null where there's no project.</summary>
        public static List<ResolveOrderEntry> Entries => ForgeProject.Current is { } project ? project.Settings?.ResolveOrder : null;

        /// <summary>Whether the current project orders anything: a project, with an entry that names a tag.</summary>
        public static bool Orders
        {
            get
            {
                var entries = Entries;
                if (entries == null) return false;
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i] != null && entries[i].Tag.IsValid) return true;
                return false;
            }
        }

        /// <summary>
        /// Where <paramref name="target"/> resolves: the Order of the first entry whose tag it carries, or one under it, as
        /// it carries them now; or 0, where it carries none.
        /// </summary>
        public static int OrderOf(ITarget target) => OrderOf(target, Entries);

        private static int OrderOf(ITarget target, List<ResolveOrderEntry> entries)
        {
            if (target == null || entries == null) return 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.Tag.IsValid && Carries(target, entry.Tag)) return entry.Order;
            }
            return 0;
        }

        // Its tag cache, which counts a tag under another, or else the tags it says it has
        private static bool Carries(ITarget target, Tag tag)
        {
            if (target is ISource source && source.GetTagCache() is { } cache) return cache.HasTag(tag, ETagMatchMode.IncludeChildren);
            var applied = target.GetAppliedTags();
            if (applied == null) return false;
            for (int i = 0; i < applied.Count; i++)
                if (applied[i].MatchesOrIsChildOf(tag)) return true;
            return false;
        }

        /// <summary>
        /// An activation's targets, as its targeting gave them, in the project's resolve order where
        /// <paramref name="ordered"/> says: a list of its own, which leaves the data's order and its primary target alone.
        /// </summary>
        public static List<ITarget> TargetsOf(ProcessDataPacket data, bool ordered = true)
        {
            int count = data?.Count(Tags.TARGET_REAL) ?? 0;
            var targets = new List<ITarget>(count);
            for (int i = 0; i < count; i++)
                if (data.TryGetAt(Tags.TARGET_REAL, i, out ITarget target) && target is not null) targets.Add(target);
            if (ordered) Sort(targets);
            return targets;
        }

        /// <summary>
        /// Sorts <paramref name="targets"/> by where each resolves, lower first, those placed alike as they were, each read
        /// once. Nothing where the project's resolve order has no entries.
        /// </summary>
        public static void Sort(List<ITarget> targets)
        {
            if (targets == null || targets.Count < 2 || !Orders) return;
            var entries = Entries;
            var orders = new int[targets.Count];
            for (int i = 0; i < orders.Length; i++) orders[i] = OrderOf(targets[i], entries);

            // An insertion sort, which keeps those placed alike in the order they came, and moves nothing where all are
            for (int i = 1; i < targets.Count; i++)
            {
                var target = targets[i];
                int order = orders[i];
                int j = i - 1;
                while (j >= 0 && orders[j] > order)
                {
                    targets[j + 1] = targets[j];
                    orders[j + 1] = orders[j];
                    j--;
                }
                targets[j + 1] = target;
                orders[j + 1] = order;
            }
        }
    }
}
