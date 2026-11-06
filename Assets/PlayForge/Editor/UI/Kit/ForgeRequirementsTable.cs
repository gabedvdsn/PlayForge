using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Tag requirements as the Effect mockup's table draws them (ED-37): a row for each group, such as Applying, While On
    /// and Removed By, with what it requires and what it must not have, each a cell that lists its tags. A cell opens its
    /// list under the table to edit, with its group's template; opening it again closes it. The table follows the asset
    /// as it changes, an Undo among its changes. It replaces a tinted box with colored subsections and a type badge.
    /// A group with no field is the requirements themselves, as a lone requirement group's table of one row (ED-66).
    /// </summary>
    internal sealed class ForgeRequirementsTable : VisualElement
    {
        public const string CellClass = "pf-requirements__cell";
        public const string OpenCellClass = "pf-requirements__cell--open";
        public const string NoneCellClass = "pf-requirements__cell--none";

        /// <summary>
        /// One row: its label, the group's field name in the requirements, or none for the requirements themselves, and
        /// what the group checks.
        /// </summary>
        public readonly struct Group
        {
            public readonly string Label;
            public readonly string Field;
            public readonly string Tooltip;

            public Group(string label, string field, string tooltip)
            {
                Label = label;
                Field = field;
                Tooltip = tooltip;
            }
        }

        private sealed class Row
        {
            public Group Group;
            public Button Requires;
            public Button Avoids;
        }

        private readonly SerializedProperty requirements;
        private readonly List<Row> rows = new();
        private readonly VisualElement editor;
        private Row openRow;
        private bool openRequires;

        public ForgeRequirementsTable(SerializedProperty requirements, string firstColumn, params Group[] groups)
        {
            this.requirements = requirements;
            name = "RequirementsTable";

            var table = new VisualElement { name = "Table" };
            table.AddToClassList("pf-table");
            table.AddToClassList("pf-requirements");
            // A lone group's row is named by its field, which needs the label column's room
            if (groups.Length == 1 && string.IsNullOrEmpty(groups[0].Field)) table.AddToClassList("pf-requirements--lone");
            Add(table);

            var header = new VisualElement { name = "Header" };
            header.AddToClassList("pf-table__header");
            header.Add(Cell(firstColumn, "pf-requirements__when"));
            header.Add(Cell("Requires", "pf-requirements__tags"));
            header.Add(Cell("Must not have", "pf-requirements__tags"));
            table.Add(header);

            for (int i = 0; i < groups.Length; i++)
            {
                var row = new Row { Group = groups[i] };
                var key = KeyOf(groups[i]);
                var element = new VisualElement { name = key, tooltip = groups[i].Tooltip };
                element.AddToClassList("pf-table__row");
                if (i % 2 == 1) element.AddToClassList("pf-table__row--alternate");
                element.Add(Cell(groups[i].Label, "pf-requirements__when"));
                row.Requires = CellButton($"{key}Requires", () => Toggle(row, true));
                row.Avoids = CellButton($"{key}Avoids", () => Toggle(row, false));
                element.Add(row.Requires);
                element.Add(row.Avoids);
                table.Add(element);
                rows.Add(row);
            }

            // The list an open cell edits, under the table
            editor = new VisualElement { name = "RequirementEditor" };
            editor.AddToClassList("pf-requirements__editor");
            Add(editor);

            this.TrackSerializedObjectValue(requirements.serializedObject, _ => Show());
            Show();
        }

        private static Label Cell(string text, string kind)
        {
            var cell = new Label(text);
            cell.AddToClassList("pf-table__cell");
            cell.AddToClassList(kind);
            return cell;
        }

        private static Button CellButton(string name, System.Action open)
        {
            var button = new Button(open) { name = name, focusable = true };
            button.AddToClassList(CellClass);
            return button;
        }

        // A row's name, and its cells' before Requires and Avoids: its group's field, or Group for the requirements themselves
        private static string KeyOf(Group group) => string.IsNullOrEmpty(group.Field) ? "Group" : group.Field;

        // A row's group: the requirements' field it names, or the requirements themselves
        private SerializedProperty GroupOf(Row row) =>
            string.IsNullOrEmpty(row.Group.Field) ? requirements : requirements.FindPropertyRelative(row.Group.Field);

        // A group's list of tag queries: what it requires, or what it must not have
        private SerializedProperty List(Row row, bool requires) => GroupOf(row)?
            .FindPropertyRelative(requires ? nameof(AvoidRequireTagGroup.RequireTags) : nameof(AvoidRequireTagGroup.AvoidTags));

        // Each cell lists its tags, or says None
        private void Show()
        {
            foreach (var row in rows)
            {
                ShowCell(row.Requires, List(row, true), row.Group.Label, "requires");
                ShowCell(row.Avoids, List(row, false), row.Group.Label, "must not have");
            }
        }

        private static void ShowCell(Button cell, SerializedProperty list, string group, string column)
        {
            var text = TagsIn(list);
            cell.text = text;
            cell.tooltip = $"{group} {column}: {text}. Click to edit.";
            cell.EnableInClassList(NoneCellClass, text == "None");
        }

        /// <summary>The tags a list of queries names, as a cell shows them: "Status.Wet, State.Stunned", or None.</summary>
        internal static string TagsIn(SerializedProperty list)
        {
            if (list == null || !list.isArray || list.arraySize == 0) return "None";
            var names = new List<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var tagName = TagDisplaySettings.PathOf(list.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(TagQuery.Tag)));
                names.Add(string.IsNullOrEmpty(tagName) ? "No Tag" : tagName);
            }
            return string.Join(", ", names);
        }

        // Opens a cell's list under the table, or closes it when it's open already
        private void Toggle(Row row, bool requires)
        {
            bool same = openRow == row && openRequires == requires;
            foreach (var each in rows)
            {
                each.Requires.RemoveFromClassList(OpenCellClass);
                each.Avoids.RemoveFromClassList(OpenCellClass);
            }
            editor.Clear();
            if (same)
            {
                openRow = null;
                return;
            }

            openRow = row;
            openRequires = requires;
            (requires ? row.Requires : row.Avoids).AddToClassList(OpenCellClass);

            var group = GroupOf(row);
            var list = List(row, requires);
            if (group == null || list == null) return;
            var column = requires ? "Requires" : "Must Not Have";
            editor.Add(new PropertyField(list, $"{row.Group.Label}: {column}") { name = "Tags" });
            // The group's name, which tells it apart where it's imported; the drawer showed it, and the table didn't (ED-66)
            var groupName = new TextField("Name")
            {
                name = "RequirementName",
                bindingPath = group.FindPropertyRelative(nameof(AvoidRequireTagGroup.Name))?.propertyPath,
                tooltip = "The group's name, which tells it apart where it's imported; optional",
            };
            groupName.AddToClassList(TextField.alignedFieldUssClassName);
            editor.Add(groupName);
            editor.Add(new PropertyField(group.FindPropertyRelative(nameof(AvoidRequireTagGroup.SourceTemplate)), "Template")
            {
                tooltip = "A shared requirement template this group can take its tags from",
            });
            editor.Add(new PropertyField(group.FindPropertyRelative(nameof(AvoidRequireTagGroup.SyncWithTemplate)), "Sync With Template")
            {
                tooltip = "Keeps the group's tags the same as its template's",
            });
            // Built after the panel bound the table, so the editor binds itself
            editor.Bind(requirements.serializedObject);
        }
    }
}
