using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A sheet's changes before they're made (TW-27, Decision 67): each number it changes, its asset, what it reads now and
    /// what it will, each chosen until it's unticked; what of the sheet can't be read; and Import, which makes the chosen
    /// changes as one Undo step.
    /// </summary>
    public sealed class ForgeSpreadsheetWindow : EditorWindow
    {
        private SpreadsheetImport import;
        private string source;
        private Button importButton;

        /// <summary>Lists a sheet's changes, read from <paramref name="source"/>, a file's name, where it's given.</summary>
        public static ForgeSpreadsheetWindow Show(SpreadsheetImport import, string source = null)
        {
            var window = CreateInstance<ForgeSpreadsheetWindow>();
            window.import = import ?? new SpreadsheetImport();
            window.source = source;
            window.titleContent = new GUIContent($"Import {window.import.Kind}");
            window.minSize = new Vector2(560, 280);
            window.ShowUtility();
            window.Build();
            return window;
        }

        private void OnEnable()
        {
            // A domain reload leaves the window with no sheet to import
            EditorApplication.delayCall += () =>
            {
                if (this != null && import == null) Close();
            };
        }

        /// <summary>Makes the chosen changes, as one Undo step, and closes.</summary>
        public void ImportChosen()
        {
            import?.Apply();
            Close();
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            var changes = import.Changes.ToList();
            var from = string.IsNullOrEmpty(source) ? "The sheet" : source;
            var summary = new Label(changes.Count == 0
                ? $"{from} changes nothing."
                : $"{from} changes {Count(changes.Count, "number", "numbers")}. Untick a change to leave it.") { name = "Summary" };
            summary.AddToClassList("pf-note");
            root.Add(summary);

            var list = new MultiColumnListView
            {
                name = "Changes",
                itemsSource = changes,
                fixedItemHeight = 20,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            list.style.flexGrow = 1;
            list.columns.Add(new Column
            {
                name = "Chosen", title = "", width = 24, resizable = false,
                makeCell = () =>
                {
                    var toggle = new Toggle { tooltip = "Make this change" };
                    toggle.RegisterValueChangedCallback(change =>
                    {
                        if (toggle.userData is SpreadsheetChange each) each.Chosen = change.newValue;
                        AllowImport();
                    });
                    return toggle;
                },
                bindCell = (cell, index) =>
                {
                    var toggle = (Toggle)cell;
                    toggle.userData = changes[index];
                    toggle.SetValueWithoutNotify(changes[index].Chosen);
                },
            });
            AddText(list, "Asset", "Asset", 160, change => ForgeSpreadsheets.NameOf(change.Asset), changes);
            AddText(list, "Field", "Field", 200, change => change.Field, changes, stretch: true);
            AddText(list, "Was", "Was", 120, change => change.Was, changes);
            AddText(list, "Becomes", "Becomes", 120, change => change.Becomes, changes);
            root.Add(list);

            if (import.Problems.Count > 0)
            {
                var problems = new Foldout
                {
                    name = "Problems",
                    text = $"{Count(import.Problems.Count, "row", "rows")} can't be read",
                    value = false,
                };
                foreach (var problem in import.Problems)
                {
                    var line = new Label(problem);
                    line.AddToClassList("pf-note");
                    problems.Add(line);
                }
                root.Add(problems);
            }

            var footer = new VisualElement { name = "Footer" };
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            importButton = new Button(ImportChosen) { name = "Import", text = "Import", tooltip = "Make the ticked changes, as one Undo step" };
            footer.Add(importButton);
            root.Add(footer);
            AllowImport();
        }

        // A column of text, which clips its own (Decision 20)
        private static void AddText(MultiColumnListView list, string name, string title, float width,
            System.Func<SpreadsheetChange, string> textOf, List<SpreadsheetChange> changes, bool stretch = false)
        {
            list.columns.Add(new Column
            {
                name = name, title = title, width = width, stretchable = stretch,
                makeCell = () =>
                {
                    var label = new Label();
                    label.style.overflow = Overflow.Hidden;
                    label.style.textOverflow = TextOverflow.Ellipsis;
                    return label;
                },
                bindCell = (cell, index) =>
                {
                    var text = textOf(changes[index]) ?? "";
                    ((Label)cell).text = text;
                    cell.tooltip = text;
                },
            });
        }

        // Import while there's a change to make; not ShowButton, which Unity takes for a window tab's own button
        private void AllowImport() => importButton?.SetEnabled(import.Changes.Any(change => change.Chosen));

        private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
    }
}
