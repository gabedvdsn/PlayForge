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
    /// the set keeps it in. The values are the runtime's (<see cref="AttributeStartValues"/>): each attribute at each of
    /// the levels, where sets overlap the one the runtime settles on, and in the set the set is previewed in. It follows
    /// the set as it changes, and the sets and scalers it reads. The Attribute Set inspector listed the attributes in a
    /// dialog, and the Entity inspector showed none of them (ED-37); a value a scaler set read "Scaled" (ED-49).
    /// </summary>
    internal sealed class ForgeAttributeTable : VisualElement
    {
        public const string TableClass = "pf-table";

        private readonly VisualElement content;
        private AttributeSet shown;
        // The entity whose levels the values are at, or null for the set's own and the set it's previewed in
        private BaseForgeLevelProvider levelsFrom;
        private VisualElement tracker;

        public ForgeAttributeTable()
        {
            name = "AttributeTable";
            content = new VisualElement { name = "AttributeTableContent" };
            Add(content);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                AttributeStartValues.PreviewChanged += PreviewChanged;
                ObjectChangeEvents.changesPublished += OthersChanged;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                AttributeStartValues.PreviewChanged -= PreviewChanged;
                ObjectChangeEvents.changesPublished -= OthersChanged;
            });
        }

        /// <summary>How many attributes the table shows.</summary>
        public int Count { get; private set; }

        /// <summary>The set the table shows, or null.</summary>
        public AttributeSet Set => shown;

        /// <summary>The levels its values are at, as it last drew them.</summary>
        public (int First, int Last) Levels { get; private set; }

        /// <summary>Raised each time the table is redrawn, as its set changes.</summary>
        public event Action Changed;

        /// <summary>
        /// Shows a set's attributes, or "None", and follows the set's changes from then on: at the set's levels, in the set
        /// it's previewed in; or, given an entity, at the entity's levels, as the entity starts with them.
        /// </summary>
        public void Show(AttributeSet set, BaseForgeLevelProvider entity = null)
        {
            levelsFrom = entity;
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

        private void PreviewChanged(AttributeSet set)
        {
            if (set == shown && levelsFrom == null) Rebuild();
        }

        // A change elsewhere to a set, a Scaler asset or an attribute can change what the set's attributes start at
        private void OthersChanged(ref ObjectChangeEventStream stream)
        {
            if (shown == null) return;
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
                stream.GetChangeAssetObjectPropertiesEvent(i, out var change);
                if (EditorUtility.InstanceIDToObject(change.instanceId) is AttributeSet or ScalerAsset or Attribute)
                {
                    Rebuild();
                    return;
                }
            }
        }

        private void Rebuild()
        {
            content.Clear();
            var rows = RowsOf(shown, levelsFrom);
            Levels = AttributeStartValues.LevelsOf(levelsFrom != null ? levelsFrom : AttributeStartValues.PreviewOf(shown));
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

        // Each attribute the set gives, in the order the runtime first meets it: this set, then each sub-set, depth first;
        // its values as the set it's previewed in starts it, or the entity
        private static List<Row> RowsOf(AttributeSet set, BaseForgeLevelProvider entity)
        {
            var rows = new List<Row>();
            if (set == null) return rows;

            var order = new List<Attribute>();
            var first = new Dictionary<Attribute, AttributeSetElement>();
            foreach (var each in set.SelfAndSubSets())
            {
                if (each.Attributes == null) continue;
                foreach (var element in each.Attributes)
                {
                    // An element with no attribute gives nothing
                    if (element?.Attribute == null || first.ContainsKey(element.Attribute)) continue;
                    first[element.Attribute] = element;
                    order.Add(element.Attribute);
                }
            }

            AttributeStartValues.Result result;
            if (entity != null)
            {
                var (from, to) = AttributeStartValues.LevelsOf(entity);
                result = AttributeStartValues.Shown(set, from, to);
            }
            else result = AttributeStartValues.Shown(set);

            foreach (var attribute in order)
            {
                var start = result.Of(attribute);
                // The bounds the runtime keeps are those of the element it keeps, or the first it adds up
                var kept = start?.From.FirstOrDefault() ?? first[attribute];
                string text, tooltip;
                if (result.Problem != null)
                {
                    text = "—";
                    tooltip = $"The values can't be worked out: {result.Problem}";
                }
                else if (start == null)
                {
                    text = "—";
                    tooltip = "";
                }
                else
                {
                    text = AttributeStartValues.CurrentText(start);
                    tooltip = Join(AttributeStartValues.Detail(start), How(start, result));
                }
                rows.Add(new Row(AttributeStartValues.NameOf(attribute), text, tooltip, RangeOf(kept)));
            }
            return rows;
        }

        // Where elements overlap, whose value is used, or that they add up
        private static string How(AttributeStartValues.Start start, AttributeStartValues.Result result)
        {
            if (start.Elements.Count <= 1) return null;
            var given = $"{start.Elements.Count} elements give it";
            if (start.From.Count > 1)
                return start.From.All(part => part.CollisionPolicy == EAttributeElementCollisionPolicy.Combine)
                    ? $"{given}, and they add up"
                    : $"{given}, and they're averaged";
            var used = start.From.FirstOrDefault();
            return used == null ? given : $"{given}, and {AttributeStartValues.NameOf(result.SetOf(used))}'s is used";
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
