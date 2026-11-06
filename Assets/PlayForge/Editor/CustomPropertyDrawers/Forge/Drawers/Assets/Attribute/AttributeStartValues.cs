using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What each attribute an entity built from an attribute set starts at, at each of the set's levels, worked out as the
    /// runtime works it out (ED-49): a stand-in entity at the level, whose attribute system is set up from the set as the
    /// GAS sets one up, with the set's sub-sets and collisions, and each element's scalers, bounds and rounding. Nothing
    /// else acts on the stand-in, no worker, effect or item, so a value is the one the set gives before anything changes
    /// it. A set that another holds starts as part of it, so its values can be worked out in each set that holds it, and
    /// the attribute set inspector previews a set in one of them (<see cref="PreviewIn"/>).
    /// </summary>
    public static class AttributeStartValues
    {
        /// <summary>The most levels worked out for a set, from its first.</summary>
        public const int MostLevels = 100;

        /// <summary>One attribute's start: its value at each level, and the elements that give it.</summary>
        public sealed class Start
        {
            private readonly AttributeValue[] values;

            internal Start(Attribute attribute, int firstLevel, AttributeValue[] values, List<AttributeSetElement> elements,
                List<AttributeSetElement> from)
            {
                Attribute = attribute;
                FirstLevel = firstLevel;
                this.values = values;
                Elements = elements;
                From = from;
            }

            public Attribute Attribute { get; }

            /// <summary>The level its first value is at.</summary>
            public int FirstLevel { get; }

            /// <summary>Its value at each level, from the first.</summary>
            public IReadOnlyList<AttributeValue> ByLevel => values;

            /// <summary>Its value at a level: below the first level, the first's, and past the last, the last's.</summary>
            public AttributeValue At(int level) => values[Mathf.Clamp(level - FirstLevel, 0, values.Length - 1)];

            /// <summary>Each element that gives it, in the order the runtime meets them: the set's, then each sub-set's.</summary>
            public IReadOnlyList<AttributeSetElement> Elements { get; }

            /// <summary>
            /// The elements its value comes from: the one its collision keeps, or those that add up, as Combine and Use
            /// Average add them.
            /// </summary>
            public IReadOnlyList<AttributeSetElement> From { get; }

            /// <summary>Whether its value differs between levels.</summary>
            public bool Varies => values.Any(value => !Same(value, values[0]));

            /// <summary>Whether its Current and Base differ at any level.</summary>
            public bool CurrentDiffers => values.Any(value => !Mathf.Approximately(value.CurrentValue, value.BaseValue));
        }

        /// <summary>What a set's attributes start at, over its levels.</summary>
        public sealed class Result
        {
            private readonly Dictionary<Attribute, Start> byAttribute;
            private readonly Dictionary<AttributeSetElement, AttributeSet> sets;

            internal Result(AttributeSet root, int firstLevel, int lastLevel, int levels, List<Start> attributes,
                Dictionary<AttributeSetElement, AttributeSet> sets, string problem)
            {
                Root = root;
                FirstLevel = firstLevel;
                LastLevel = lastLevel;
                Levels = levels;
                Attributes = attributes;
                byAttribute = attributes.ToDictionary(start => start.Attribute);
                this.sets = sets;
                Problem = problem;
            }

            /// <summary>The set an entity is built from.</summary>
            public AttributeSet Root { get; }

            /// <summary>The first level worked out: the set's starting level.</summary>
            public int FirstLevel { get; }

            /// <summary>The last level worked out: the set's last, or the <see cref="MostLevels"/>th from its first.</summary>
            public int LastLevel { get; }

            /// <summary>The set's own last level, which may be past the last worked out.</summary>
            public int Levels { get; }

            /// <summary>Each attribute the set gives, in the order the runtime meets them.</summary>
            public IReadOnlyList<Start> Attributes { get; }

            /// <summary>Why the values couldn't be worked out, as what a scaler threw; or null.</summary>
            public string Problem { get; }

            /// <summary>An attribute's start, or null when the set doesn't give it.</summary>
            public Start Of(Attribute attribute) => attribute != null && byAttribute.TryGetValue(attribute, out var start) ? start : null;

            /// <summary>Whether the set gives an attribute.</summary>
            public bool Gives(Attribute attribute) => attribute != null && byAttribute.ContainsKey(attribute);

            /// <summary>The set an element is in, or null when the set and its sub-sets don't hold it.</summary>
            public AttributeSet SetOf(AttributeSetElement element) => element != null && sets.TryGetValue(element, out var set) ? set : null;

            /// <summary>Whether an element is one of the set's or its sub-sets'.</summary>
            public bool Holds(AttributeSetElement element) => element != null && sets.ContainsKey(element);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The values
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>What an entity built from a set starts with, over the set's levels.</summary>
        public static Result Of(AttributeSet root)
        {
            var (first, last) = LevelsOf(root);
            return Of(root, first, last);
        }

        /// <summary>What an entity built from a set starts with, at each level from the first to the last.</summary>
        public static Result Of(AttributeSet root, int firstLevel, int lastLevel)
        {
            firstLevel = Mathf.Max(1, firstLevel);
            lastLevel = Mathf.Max(firstLevel, lastLevel);
            int worked = Mathf.Min(lastLevel, firstLevel + MostLevels - 1);
            var sets = new Dictionary<AttributeSetElement, AttributeSet>();
            if (root == null) return new Result(null, firstLevel, worked, lastLevel, new List<Start>(), sets, null);

            // Each element, in the order the runtime meets them: the set's, then each sub-set's, depth first (GAS-44)
            var order = new List<Attribute>();
            var elements = new Dictionary<Attribute, List<AttributeSetElement>>();
            foreach (var set in root.SelfAndSubSets())
            {
                if (set.Attributes == null) continue;
                foreach (var element in set.Attributes)
                {
                    // An element with no attribute gives nothing (GAS-54)
                    if (element?.Attribute == null || sets.ContainsKey(element)) continue;
                    sets[element] = set;
                    if (!elements.TryGetValue(element.Attribute, out var list))
                    {
                        elements[element.Attribute] = list = new List<AttributeSetElement>();
                        order.Add(element.Attribute);
                    }
                    list.Add(element);
                }
            }

            var values = order.ToDictionary(attribute => attribute, _ => new AttributeValue[worked - firstLevel + 1]);
            var from = new Dictionary<Attribute, List<AttributeSetElement>>();
            string problem = null;
            // The stand-in is the runtime's own entity, so what it says as it starts it would say on every redraw: below an
            // error, it says nothing
            var quiet = ForgeLog.MinimumLevel;
            if (quiet < ForgeLog.ELevel.Error) ForgeLog.MinimumLevel = ForgeLog.ELevel.Error;
            try
            {
            for (int level = firstLevel; level <= worked; level++)
            {
                AttributeSystemComponent system;
                try
                {
                    system = StandIn(root, level, lastLevel);
                }
                catch (Exception thrown)
                {
                    // The runtime would throw as it built the entity, so no value stands
                    problem = $"{thrown.GetType().Name}: {thrown.Message}";
                    break;
                }
                foreach (var attribute in order)
                {
                    if (!system.TryGetCachedValue(attribute, out var cached) || cached == null) continue;
                    values[attribute][level - firstLevel] = cached.ActiveValue;
                    // The collision settles on the elements' magnitudes, so the same elements at every level
                    if (!from.ContainsKey(attribute))
                        from[attribute] = cached.Blueprint?.Parts.Select(part => part.SetElement).Where(element => element != null).ToList()
                                          ?? new List<AttributeSetElement>();
                }
            }
            }
            finally
            {
                ForgeLog.MinimumLevel = quiet;
            }

            var starts = order.Select(attribute => new Start(attribute, firstLevel, values[attribute], elements[attribute],
                from.TryGetValue(attribute, out var parts) ? parts : new List<AttributeSetElement>())).ToList();
            return new Result(root, firstLevel, worked, lastLevel, starts, sets, problem);
        }

        // A stand-in entity at a level, whose attribute system is set up from the set as the GAS sets one up. It gets no
        // set of its own, so the GAS gives it none of the set's workers, which could act on more than the stand-in
        private static AttributeSystemComponent StandIn(AttributeSet root, int level, int lastLevel)
        {
            var identity = new RuntimeEntityIdentity
            {
                Name = $"{NameOf(root)} Preview",
                AssetTag = root.AssetTag,
                GrantedTags = new List<Tag>(),
                ContextTags = new List<Tag>(),
                Affiliation = new List<Tag>(),
                StartingLevel = level,
                MaxLevel = lastLevel,
            };
            var entity = new RuntimeGameplayAbilitySystem(identity);
            if (!entity.FindAttributeSystem(out var system) || system == null)
                throw new InvalidOperationException("The stand-in entity has no attribute system.");
            system.Setup(root);
            system.Initialize();
            return system;
        }

        /// <summary>
        /// The levels a set's values are worked out over: its starting level to its last, its level provider's while it's
        /// linked to one, through each link in a chain.
        /// </summary>
        public static (int First, int Last) LevelsOf(BaseForgeLevelProvider provider)
        {
            for (int hop = 0; provider != null && provider.IsLinked && hop < 8; hop++) provider = provider.LinkedProvider;
            if (provider == null) return (1, 1);
            int last = Mathf.Max(1, provider.GetMaxLevel());
            return (Mathf.Clamp(provider.GetStartingLevel(), 1, last), last);
        }

        /// <summary>
        /// The attributes an element's scalers read that the set it starts in doesn't give, each of which reads 0. A cached
        /// attribute scaler reads the attribute it captures, a scaler group its members', and a shared one its asset's.
        /// </summary>
        public static List<Attribute> MissingReads(AttributeSetElement element, Result result)
        {
            var missing = new List<Attribute>();
            if (element == null || result == null) return missing;
            foreach (var read in Reads(element))
                if (read != null && !result.Gives(read) && !missing.Contains(read)) missing.Add(read);
            return missing;
        }

        // The attributes an element's sides read through their scalers
        private static IEnumerable<Attribute> Reads(AttributeSetElement element)
        {
            var sides = element.LinkCurrentToBase ? new[] { element.Base } : new[] { element.Current, element.Base };
            foreach (var side in sides)
            {
                if (side == null || side.BypassScaling) continue;
                foreach (var read in ReadsOf(side.Scaling, 0)) yield return read;
            }
        }

        private static IEnumerable<Attribute> ReadsOf(AbstractCachedScaler scaler, int depth)
        {
            if (scaler == null || depth > 8) yield break;
            if (scaler is CachedAttributeScaler captured) yield return captured.CaptureAttribute;
            if (scaler is CachedScalerGroup group && group.Calculations != null)
                foreach (var member in group.Calculations)
                foreach (var read in ReadsOf(member.Calculation, depth + 1))
                    yield return read;
            if (scaler is SharedCachedScaler shared && shared.Asset != null)
                foreach (var read in ReadsOf(shared.Asset.Scaler as AbstractCachedScaler, depth + 1))
                    yield return read;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // How a start reads
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The most levels a series shows one by one, as "100/120/140/160"; more read as a range.</summary>
        public const int MostInSeries = 6;

        /// <summary>
        /// A start as the element and the table read it: its value at each level, as "100/120/140/160", one value when
        /// every level reads the same, or a range past <see cref="MostInSeries"/> levels; and Current and Base apart, as
        /// "Current 80 · Base 100", when they differ.
        /// </summary>
        public static string Text(Start start)
        {
            if (start == null || start.ByLevel.Count == 0) return "—";
            var bases = start.ByLevel.Select(value => value.BaseValue).ToList();
            if (!start.CurrentDiffers) return Series(bases);
            return $"Current {Series(start.ByLevel.Select(value => value.CurrentValue).ToList())} · Base {Series(bases)}";
        }

        /// <summary>A start's Current alone, as the attribute table's column reads it.</summary>
        public static string CurrentText(Start start) =>
            start == null || start.ByLevel.Count == 0 ? "—" : Series(start.ByLevel.Select(value => value.CurrentValue).ToList());

        /// <summary>A start's Current and Base at each level, as a tooltip spells it out.</summary>
        public static string Detail(Start start)
        {
            if (start == null || start.ByLevel.Count == 0) return "";
            var current = Series(start.ByLevel.Select(value => value.CurrentValue).ToList(), all: true);
            var bases = Series(start.ByLevel.Select(value => value.BaseValue).ToList(), all: true);
            var levels = LevelsText(start.FirstLevel, start.FirstLevel + start.ByLevel.Count - 1);
            return $"Current {current}, Base {bases}, at {levels.ToLowerInvariant()}";
        }

        /// <summary>A range of levels, as "Levels 1–4", or "Level 3" for one.</summary>
        public static string LevelsText(int first, int last) => first == last ? $"Level {first}" : $"Levels {first}–{last}";

        private static string Series(IReadOnlyList<float> values, bool all = false)
        {
            if (values.Count == 0) return "—";
            if (values.All(value => Mathf.Approximately(value, values[0]))) return Number(values[0]);
            if (all || values.Count <= MostInSeries) return string.Join("/", values.Select(Number));
            return ScalerFields.RangeText(values, null);
        }

        private static bool Same(AttributeValue a, AttributeValue b) =>
            Mathf.Approximately(a.CurrentValue, b.CurrentValue) && Mathf.Approximately(a.BaseValue, b.BaseValue);

        private static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);

        internal static string NameOf(AttributeSet set) =>
            set == null ? "" : string.IsNullOrEmpty(set.Name) ? set.name : set.Name;

        internal static string NameOf(Attribute attribute) =>
            attribute == null ? "" : string.IsNullOrEmpty(attribute.GetName()) ? attribute.name : attribute.GetName();

        // ═══════════════════════════════════════════════════════════════════════════
        // The sets a set starts as part of
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The sets in the project that hold a set, directly or through another, in name order.</summary>
        public static List<AttributeSet> HoldersOf(AttributeSet set)
        {
            if (set == null) return new List<AttributeSet>();
            return AssetDatabase.FindAssets($"t:{nameof(AttributeSet)}")
                .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Select(AssetDatabase.LoadAssetAtPath<AttributeSet>)
                .Where(candidate => candidate != null && candidate != set && candidate.SelfAndSubSets().Contains(set))
                .OrderBy(NameOf, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // The set each set is previewed in, kept until the editor closes, through script reloads: an asset keeps its
        // instance ID for the editor session
        private static string PreviewKey(AttributeSet set) => $"PlayForge_PreviewIn_{set.GetInstanceID()}";

        /// <summary>Raised as the set a set is previewed in changes, with the set.</summary>
        public static event Action<AttributeSet> PreviewChanged;

        /// <summary>The set a set's values are previewed in: a set that holds it, or itself.</summary>
        public static AttributeSet PreviewOf(AttributeSet set)
        {
            if (set == null) return null;
            int id = SessionState.GetInt(PreviewKey(set), 0);
            if (id != 0 && EditorUtility.InstanceIDToObject(id) is AttributeSet holder && holder != set &&
                holder.SelfAndSubSets().Contains(set))
                return holder;
            return set;
        }

        /// <summary>Previews a set's values as part of a set that holds it, or alone when that's null or itself.</summary>
        public static void PreviewIn(AttributeSet set, AttributeSet holder)
        {
            if (set == null) return;
            var was = PreviewOf(set);
            if (holder == null || holder == set) SessionState.EraseInt(PreviewKey(set));
            else SessionState.SetInt(PreviewKey(set), holder.GetInstanceID());
            if (PreviewOf(set) != was) PreviewChanged?.Invoke(set);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // For the inspector: one working out a frame
        // ═══════════════════════════════════════════════════════════════════════════

        private static readonly Dictionary<(int root, int first, int last, int stamp), Result> shown = new();
        private static bool clearing;

        // How many times the set and its sub-sets have changed, so a change in the frame a value was worked out in works
        // it out again
        private static int StampOf(AttributeSet root)
        {
            if (root == null) return 0;
            unchecked
            {
                int stamp = 17;
                foreach (var set in root.SelfAndSubSets()) stamp = stamp * 31 + EditorUtility.GetDirtyCount(set) * 7 + set.GetInstanceID();
                return stamp;
            }
        }

        /// <summary>
        /// What the inspector shows of a set: its values in the set it's previewed in, over that set's levels. The
        /// elements and the table of one set each ask in the same frame, so the values are worked out once a frame.
        /// </summary>
        internal static Result Shown(AttributeSet set)
        {
            var root = PreviewOf(set);
            var (first, last) = LevelsOf(root);
            return Shown(root, first, last);
        }

        /// <summary>A set's values over the levels given, as an entity's, worked out once a frame.</summary>
        internal static Result Shown(AttributeSet root, int first, int last)
        {
            var key = (root != null ? root.GetInstanceID() : 0, first, last, StampOf(root));
            if (shown.TryGetValue(key, out var result)) return result;
            result = Of(root, first, last);
            shown[key] = result;
            if (!clearing)
            {
                clearing = true;
                EditorApplication.delayCall += () =>
                {
                    shown.Clear();
                    clearing = false;
                };
            }
            return result;
        }
    }
}
