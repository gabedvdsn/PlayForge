using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What a try reads against, drawn: a block for the caster and one for the target, each an Entity field that fills it
    /// and a row for each attribute read of it, whether the side has it, its current value and its base; then a block for
    /// the keys a caller sets, whether each is set and its value. A Scaler asset's try and a Value Pipeline's draw their
    /// numbers with it. A row comes as a read first asks for its attribute or key, so a view follows each read with
    /// <see cref="Follow"/>; what's typed here reads at once.
    /// </summary>
    internal sealed class TryNumbersView : VisualElement
    {
        private readonly ScalerTry tried;
        private readonly Action changed;
        private readonly string reader;
        private string drawnRows;

        /// <param name="tried">The sides and the caller it draws.</param>
        /// <param name="changed">Reads again, as a value here changes.</param>
        /// <param name="reader">What reads a caller's key, as "the scaler", for a key's tooltip.</param>
        public TryNumbersView(ScalerTry tried, Action changed, string reader)
        {
            this.tried = tried;
            this.changed = changed;
            this.reader = reader;
            name = "TrySides";
        }

        /// <summary>
        /// Draws its rows again where the last read asked for an attribute or a key it hadn't, and only then, so a field
        /// being typed in stays.
        /// </summary>
        public void Follow()
        {
            var rows = RowsKey();
            if (rows == drawnRows) return;
            drawnRows = rows;
            Clear();
            Add(SideBlock(tried.Caster, "Caster", "the caster"));
            Add(SideBlock(tried.Target, "Target", "the target"));
            Add(CallerBlock());
        }

        private string RowsKey()
        {
            var key = new System.Text.StringBuilder();
            foreach (var side in new[] { tried.Caster, tried.Target })
            {
                key.Append(side.Which).Append(side.Entity != null ? side.Entity.GetInstanceID() : 0).Append(':');
                foreach (var row in side.Attributes) key.Append(row.Attribute != null ? row.Attribute.GetInstanceID() : 0).Append(',');
            }
            key.Append("|");
            foreach (var row in tried.Caller) key.Append(row.Key.Name).Append(',');
            return key.ToString();
        }

        private VisualElement SideBlock(ScalerTry.Side side, string title, string what)
        {
            var block = new VisualElement { name = $"Try{title}" };
            block.AddToClassList("pf-try__side");
            var fill = new ObjectField(title)
            {
                name = $"{title} Entity",
                objectType = typeof(EntityIdentity),
                allowSceneObjects = false,
                value = side.Entity,
                tooltip = $"An entity to fill {what}'s attributes from: what its attribute set starts each at, at its starting level",
            };
            fill.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
            fill.RegisterValueChangedCallback(change =>
            {
                side.FillFrom(change.newValue as EntityIdentity);
                drawnRows = null;
                schedule.Execute(() => changed?.Invoke());
            });
            block.Add(fill);

            if (side.Attributes.Count == 0)
            {
                var none = new Label($"It reads nothing of {what}.");
                none.AddToClassList("pf-note");
                block.Add(none);
                return block;
            }
            foreach (var row in side.Attributes) block.Add(AttributeRow(row, title, what));
            return block;
        }

        private VisualElement AttributeRow(ScalerTry.AttributeRow row, string title, string what)
        {
            string attribute = row.Attribute.GetName();
            var line = new VisualElement { name = $"{title} {attribute}" };
            line.AddToClassList("pf-try__row");
            var label = new Label(attribute) { tooltip = $"{attribute}, as {what} holds it for the read" };
            label.AddToClassList("pf-try__name");
            line.Add(label);

            var has = new Toggle { name = $"{title} {attribute} Has", value = row.Has, tooltip = $"Whether {what} has {attribute}: without it, it reads as an entity without the attribute" };
            has.AddToClassList("pf-try__has");
            var current = new FloatField { name = $"{title} {attribute} Current", value = row.Current, isDelayed = true, tooltip = $"{attribute}'s current value" };
            current.AddToClassList("pf-try__value");
            var baseValue = new FloatField { name = $"{title} {attribute} Base", value = row.Base, isDelayed = true, tooltip = $"{attribute}'s base value" };
            baseValue.AddToClassList("pf-try__value");
            current.SetEnabled(row.Has);
            baseValue.SetEnabled(row.Has);

            has.RegisterValueChangedCallback(change =>
            {
                row.Has = change.newValue;
                current.SetEnabled(row.Has);
                baseValue.SetEnabled(row.Has);
                changed?.Invoke();
            });
            current.RegisterValueChangedCallback(change =>
            {
                // A base typed as the current was keeps in step with it, until it's typed apart
                if (Mathf.Approximately(row.Base, row.Current)) row.Base = change.newValue;
                row.Current = change.newValue;
                baseValue.SetValueWithoutNotify(row.Base);
                changed?.Invoke();
            });
            baseValue.RegisterValueChangedCallback(change =>
            {
                row.Base = change.newValue;
                changed?.Invoke();
            });
            line.Add(has);
            line.Add(current);
            line.Add(baseValue);
            return line;
        }

        private VisualElement CallerBlock()
        {
            var block = new VisualElement { name = "TryCaller" };
            block.AddToClassList("pf-try__side");
            var title = new Label("Caller") { tooltip = "The values a caller sets, by key, as Set By Caller reads them" };
            title.AddToClassList("pf-try__title");
            block.Add(title);
            if (tried.Caller.Count == 0)
            {
                var none = new Label("It reads no caller's value.");
                none.AddToClassList("pf-note");
                block.Add(none);
                return block;
            }
            foreach (var row in tried.Caller)
            {
                var each = row;
                string key = each.Key.ToString();
                var line = new VisualElement { name = $"Caller {key}" };
                line.AddToClassList("pf-try__row");
                var label = new Label(key) { tooltip = $"{key}: unset, {reader} takes its default" };
                label.AddToClassList("pf-try__name");
                line.Add(label);
                var set = new Toggle { name = $"Caller {key} Set", value = each.Set, tooltip = $"Whether the caller sets {key}" };
                set.AddToClassList("pf-try__has");
                var value = new FloatField { name = $"Caller {key} Value", value = each.Value, isDelayed = true, tooltip = $"The value the caller sets {key} to" };
                value.AddToClassList("pf-try__value");
                value.SetEnabled(each.Set);
                set.RegisterValueChangedCallback(change =>
                {
                    each.Set = change.newValue;
                    value.SetEnabled(each.Set);
                    changed?.Invoke();
                });
                value.RegisterValueChangedCallback(change =>
                {
                    each.Value = change.newValue;
                    changed?.Invoke();
                });
                line.Add(set);
                line.Add(value);
                block.Add(line);
            }
            return block;
        }
    }
}
