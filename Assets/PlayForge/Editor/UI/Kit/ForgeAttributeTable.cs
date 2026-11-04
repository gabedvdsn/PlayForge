using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The attributes an attribute set gives an entity, its sub-sets' among them, as the Entity mockup's Attributes
    /// section draws them: a table of each attribute's type icon and name, the value its Current starts at, and the range
    /// the set keeps it in. Where sets overlap, the value is the one the runtime settles on, by the same rules
    /// (AttributeSetMeta), and a value a scaler sets reads "Scaled". It follows the set as it changes. The Attribute Set
    /// inspector listed the attributes in a dialog, and the Entity inspector showed none of them (ED-37).
    /// </summary>
    internal sealed class ForgeAttributeTable : VisualElement
    {
        public const string TableClass = "pf-table";

        private readonly VisualElement content;
        private AttributeSet shown;
        private VisualElement tracker;

        public ForgeAttributeTable()
        {
            name = "AttributeTable";
            content = new VisualElement { name = "AttributeTableContent" };
            Add(content);
        }

        /// <summary>How many attributes the table shows.</summary>
        public int Count { get; private set; }

        /// <summary>The set the table shows, or null.</summary>
        public AttributeSet Set => shown;

        /// <summary>Raised each time the table is redrawn, as its set changes.</summary>
        public event Action Changed;

        /// <summary>Shows a set's attributes, or "None", and follows the set's changes from then on.</summary>
        public void Show(AttributeSet set)
        {
            if (set != shown || tracker == null)
            {
                shown = set;
                // A child of its own follows the set, so showing another set ends this one's tracking with it
                tracker?.RemoveFromHierarchy();
                tracker = null;
                if (set != null)
                {
                    tracker = new VisualElement { name = "AttributeTableTracker" };
                    Add(tracker);
                    tracker.TrackSerializedObjectValue(new SerializedObject(set), _ => Rebuild());
                }
            }
            Rebuild();
        }

        private void Rebuild()
        {
            content.Clear();
            var rows = RowsOf(shown);
            Count = rows.Count;
            content.EnableInClassList(TableClass, rows.Count > 0);
            if (rows.Count == 0)
            {
                var none = new Label("None");
                none.AddToClassList("pf-note");
                content.Add(none);
                Changed?.Invoke();
                return;
            }

            var header = new VisualElement();
            header.AddToClassList("pf-table__header");
            header.Add(Cell("Attribute", "pf-table__cell--name"));
            header.Add(Cell("Starts at", "pf-table__cell--number"));
            header.Add(Cell("Range", "pf-table__cell--range"));
            content.Add(header);

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var line = new VisualElement();
                line.AddToClassList("pf-table__row");
                line.EnableInClassList("pf-table__row--alternate", i % 2 == 1);

                var nameCell = new VisualElement();
                nameCell.AddToClassList("pf-table__cell");
                nameCell.AddToClassList("pf-table__cell--name");
                nameCell.Add(new ForgeTypeIcon(typeof(Attribute)));
                var name = new Label(row.Name);
                name.AddToClassList("pf-table__name");
                nameCell.Add(name);
                line.Add(nameCell);

                var start = Cell(row.Start, "pf-table__cell--number");
                start.AddToClassList("pf-table__start");
                start.tooltip = row.StartTooltip;
                line.Add(start);

                var range = Cell(row.Range, "pf-table__cell--range");
                range.AddToClassList("pf-table__range");
                range.tooltip = row.RangeTooltip;
                line.Add(range);

                content.Add(line);
            }
            Changed?.Invoke();
        }

        private static Label Cell(string text, string kind)
        {
            var cell = new Label(text);
            cell.AddToClassList("pf-table__cell");
            cell.AddToClassList(kind);
            return cell;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What each attribute starts at, settled as the runtime settles it
        // ═══════════════════════════════════════════════════════════════════════════

        private readonly struct Row
        {
            public readonly string Name;
            public readonly string Start;
            public readonly string StartTooltip;
            public readonly string Range;
            public readonly string RangeTooltip;

            public Row(string name, string start, string startTooltip, (string text, string tooltip) range)
            {
                Name = name;
                Start = start;
                StartTooltip = startTooltip;
                Range = range.text;
                RangeTooltip = range.tooltip;
            }
        }

        private enum Settle { One, Sum, Average }

        // Each attribute the set gives, in the order the runtime first meets it: this set, then each sub-set, depth first
        private static List<Row> RowsOf(AttributeSet set)
        {
            var rows = new List<Row>();
            if (set == null) return rows;

            var order = new List<Attribute>();
            var elements = new Dictionary<Attribute, List<AttributeSetElement>>();
            foreach (var each in set.SelfAndSubSets())
            {
                if (each.Attributes == null) continue;
                foreach (var element in each.Attributes)
                {
                    // An element with no attribute gives nothing
                    if (element?.Attribute == null) continue;
                    if (!elements.TryGetValue(element.Attribute, out var list))
                    {
                        elements[element.Attribute] = list = new List<AttributeSetElement>();
                        order.Add(element.Attribute);
                    }
                    list.Add(element);
                }
            }

            foreach (var attribute in order) rows.Add(RowOf(attribute, elements[attribute], set.CollisionResolutionPolicy));
            return rows;
        }

        // The runtime takes the elements that use the set's setting, or else those set to Use This, Combine, then Use
        // Existing, and settles them by the root set's policy (AttributeSetMeta.InitializeAttributeSystem)
        private static Row RowOf(Attribute attribute, List<AttributeSetElement> elements, EValueCollisionPolicy resolution)
        {
            List<AttributeSetElement> Of(EAttributeElementCollisionPolicy policy) => elements.Where(element => element.CollisionPolicy == policy).ToList();

            var group = Of(EAttributeElementCollisionPolicy.UseSetCollisionSetting);
            if (group.Count == 0) group = Of(EAttributeElementCollisionPolicy.UseThis);
            if (group.Count == 0)
            {
                var combined = Of(EAttributeElementCollisionPolicy.Combine);
                if (combined.Count > 0)
                    return Settled(attribute, combined, Settle.Sum, combined.Count > 1 ? $"{combined.Count} elements give it, and they add up" : null);
                group = Of(EAttributeElementCollisionPolicy.UseExisting);
            }
            if (group.Count == 1) return Settled(attribute, group, Settle.One, null);

            var how = $"{group.Count} elements give it, and {ObjectNames.NicifyVariableName(resolution.ToString())} settles it";
            // Use Maximum and Use Minimum compare the elements' Base magnitudes, through the runtime's own helpers
            return resolution switch
            {
                EValueCollisionPolicy.UseAverage => Settled(attribute, group, Settle.Average, how),
                EValueCollisionPolicy.UseMaximum => Settled(attribute, new List<AttributeSetElement> { group[group.IndexMax(element => element.Base.Magnitude)] }, Settle.One, how),
                EValueCollisionPolicy.UseMinimum => Settled(attribute, new List<AttributeSetElement> { group[group.IndexMin(element => element.Base.Magnitude)] }, Settle.One, how),
                EValueCollisionPolicy.UseLast => Settled(attribute, new List<AttributeSetElement> { group[group.Count - 1] }, Settle.One, how),
                _ => Settled(attribute, new List<AttributeSetElement> { group[0] }, Settle.One, how),
            };
        }

        // The value the parts start Current at, and the range of the first, whose bounds the runtime keeps
        private static Row Settled(Attribute attribute, List<AttributeSetElement> parts, Settle settle, string how)
        {
            var name = string.IsNullOrEmpty(attribute.GetName()) ? attribute.name : attribute.GetName();
            var range = RangeOf(parts[0]);
            if (parts.Any(part => !part.EffectiveCurrent.BypassScaling))
                return new Row(name, "Scaled", Join("A scaler sets its Current as the entity starts", how), range);

            var current = parts.Sum(part => part.EffectiveCurrent.Magnitude);
            var baseValue = parts.Sum(part => part.Base.Magnitude);
            if (settle == Settle.Average)
            {
                current /= parts.Count;
                baseValue /= parts.Count;
            }
            return new Row(name, Number(current), Join($"Current {Number(current)}, Base {Number(baseValue)}", how), range);
        }

        private static string Join(string fact, string how) => how == null ? fact : $"{fact}. {how}.";

        // The range the set keeps an element's Current in
        private static (string text, string tooltip) RangeOf(AttributeSetElement element) =>
            RangeOf(element.Overflow, element.Constraints == null || element.Constraints.AutoClamp);

        /// <summary>
        /// The range bounds keep Current in (AttributeOverflowData), as in "0–max", where max is the Base value, and "—"
        /// when nothing bounds it or Auto Clamp is off. The element drawer's line shows it too.
        /// </summary>
        internal static (string text, string tooltip) RangeOf(AttributeOverflowData overflow, bool clamped)
        {
            if (!clamped) return ("—", "Not clamped: Auto Clamp is off");
            var floor = Number(overflow.Floor.CurrentValue);
            var ceil = Number(overflow.Ceil.CurrentValue);
            return overflow.Policy switch
            {
                EAttributeOverflowPolicy.ZeroToBase => ("0–max", "From 0 to its Base value"),
                EAttributeOverflowPolicy.FloorToBase => ($"{floor}–max", $"From {floor} to its Base value"),
                EAttributeOverflowPolicy.ZeroToCeil => ($"0–{ceil}", $"From 0 to {ceil}"),
                EAttributeOverflowPolicy.FloorToCeil => ($"{floor}–{ceil}", $"From {floor} to {ceil}"),
                _ => ("—", "Unbounded"),
            };
        }

        private static string Number(float value) => value.ToString("0.##", CultureInfo.CurrentCulture);
    }
}
