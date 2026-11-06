using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The Problems view (Q8, ED-15): every problem the validator finds in the project's PlayForge assets, each with its
    /// severity and its asset, and a click selects the asset. It follows the validator, so it has no Refresh (Q30). It
    /// replaces Validate All, which showed 15 problems in a dialog nothing in could be selected, and the Audit, which
    /// wrote to the Console.
    /// </summary>
    public sealed class ForgeProblemsWindow : EditorWindow
    {
        private const string Title = "Problems";

        [SerializeField] private string search = "";
        [SerializeField] private bool showErrors = true;
        [SerializeField] private bool showWarnings = true;

        private MultiColumnListView list;
        private ToolbarToggle errorsToggle;
        private ToolbarToggle warningsToggle;
        private ToolbarSearchField searchField;
        private Label status;
        private List<ForgeProblem> shown = new();

        // The menu's name is the window's title (ED-19)
        [MenuItem("Window/PlayForge/" + Title, false, 10)]
        public static ForgeProblemsWindow ShowWindow()
        {
            var window = GetWindow<ForgeProblemsWindow>();
            window.titleContent = new GUIContent(Title);
            window.minSize = new Vector2(420, 200);
            return window;
        }

        /// <summary>Opens the view on one asset's problems, as an inspector header's count does.</summary>
        public static void ShowFor(Object asset)
        {
            var window = ShowWindow();
            window.search = asset != null ? asset.name : "";
            window.searchField?.SetValueWithoutNotify(window.search);
            window.Refill();
        }

        private void OnEnable() => ForgeValidator.Changed += Refill;

        private void OnDisable() => ForgeValidator.Changed -= Refill;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-problems");

            var toolbar = new Toolbar { name = "ProblemsToolbar" };
            errorsToggle = Filter(toolbar, "Errors", showErrors, "Show the problems that stop something working", on =>
            {
                showErrors = on;
                Refill();
            });
            warningsToggle = Filter(toolbar, "Warnings", showWarnings, "Show the problems that are likely a mistake", on =>
            {
                showWarnings = on;
                Refill();
            });
            toolbar.Add(new ToolbarSpacer { flex = true });
            searchField = new ToolbarSearchField { name = "Search", tooltip = "Show the problems whose asset or message has this text" };
            searchField.SetValueWithoutNotify(search);
            searchField.RegisterValueChangedCallback(change =>
            {
                search = change.newValue ?? "";
                Refill();
            });
            searchField.AddToClassList("pf-problems__search");
            toolbar.Add(searchField);
            root.Add(toolbar);

            list = new MultiColumnListView
            {
                name = "Problems",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                sortingMode = ColumnSortingMode.Default,
            };
            list.AddToClassList("pf-problems__list");
            list.columns.Add(new Column
            {
                name = "Severity", title = "", width = 26, resizable = false, sortable = true,
                makeCell = () => new ForgeGlyph(ForgeGlyphShape.Warning),
                bindCell = (cell, index) =>
                {
                    var problem = shown[index];
                    cell.EnableInClassList("pf-glyph--warning", problem.Severity == ForgeProblemSeverity.Warning);
                    cell.EnableInClassList("pf-glyph--destructive", problem.Severity == ForgeProblemSeverity.Error);
                    cell.tooltip = problem.Severity.ToString();
                },
                comparison = (a, b) => shown[b].Severity.CompareTo(shown[a].Severity),
            });
            list.columns.Add(new Column
            {
                name = "Asset", title = "Asset", width = 200, stretchable = false, sortable = true,
                makeCell = () =>
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("pf-problems__asset");
                    var icon = new ForgeTypeIcon(typeof(Ability)) { name = "Icon" };
                    cell.Add(icon);
                    var label = new Label { name = "Name" };
                    label.AddToClassList("pf-problems__name");
                    cell.Add(label);
                    return cell;
                },
                bindCell = (cell, index) =>
                {
                    var asset = shown[index].Asset;
                    var type = asset != null ? asset.GetType() : null;
                    var icon = cell.Q<ForgeGlyph>("Icon");
                    icon.Shape = ForgeAssetTypes.GlyphOf(type);
                    foreach (var modifier in Modifiers) icon.EnableInClassList($"{ForgeTypeIcon.TypeIconClass}--{modifier}", ForgeAssetTypes.ModifierOf(type) == modifier);
                    var label = cell.Q<Label>("Name");
                    label.text = NameOf(asset);
                    label.tooltip = asset != null ? AssetDatabase.GetAssetPath(asset) : "";
                },
                comparison = (a, b) => string.Compare(NameOf(shown[a].Asset), NameOf(shown[b].Asset), StringComparison.OrdinalIgnoreCase),
            });
            list.columns.Add(new Column
            {
                name = "Problem", title = "Problem", stretchable = true, sortable = true,
                makeCell = () =>
                {
                    var label = new Label();
                    label.AddToClassList("pf-problems__message");
                    return label;
                },
                bindCell = (cell, index) =>
                {
                    var label = (Label)cell;
                    label.text = shown[index].Message;
                    label.tooltip = shown[index].Message;
                },
                comparison = (a, b) => string.Compare(shown[a].Message, shown[b].Message, StringComparison.OrdinalIgnoreCase),
            });
            ForgeColumnWidths.Remember(list.columns, "Problems");
            // One click selects the asset and shows it in the Project window; a double click opens it in a window of its own
            list.selectionChanged += selection => Reveal(selection.OfType<ForgeProblem>().FirstOrDefault());
            list.itemsChosen += chosen =>
            {
                var problem = chosen.OfType<ForgeProblem>().FirstOrDefault();
                if (problem?.Asset != null) EditorUtility.OpenPropertyEditor(problem.Asset);
            };
            root.Add(list);

            status = new Label { name = "ProblemsStatus" };
            status.AddToClassList("pf-problems__status");
            root.Add(status);
            Refill();
        }

        private static ToolbarToggle Filter(Toolbar toolbar, string name, bool on, string tooltip, Action<bool> changed)
        {
            var toggle = new ToolbarToggle { name = name, tooltip = tooltip };
            toggle.SetValueWithoutNotify(on);
            toggle.RegisterValueChangedCallback(change => changed(change.newValue));
            toolbar.Add(toggle);
            return toggle;
        }

        private static readonly string[] Modifiers = { "ability", "effect", "entity", "item", "attribute", "attribute-set" };

        // Shows the problems the filters let through, the errors first, then by asset
        private void Refill()
        {
            if (list == null) return;
            var all = ForgeValidator.All;
            shown = all
                .Where(problem => problem.Severity == ForgeProblemSeverity.Error ? showErrors : showWarnings)
                .Where(Matches)
                .OrderByDescending(problem => problem.Severity)
                .ThenBy(problem => NameOf(problem.Asset), StringComparer.OrdinalIgnoreCase)
                .ToList();
            list.itemsSource = shown;
            list.RefreshItems();

            int errors = all.Count(problem => problem.Severity == ForgeProblemSeverity.Error);
            int warnings = all.Count - errors;
            errorsToggle.text = CountOf(errors, "Error", "Errors");
            warningsToggle.text = CountOf(warnings, "Warning", "Warnings");
            int assets = all.Select(problem => problem.Asset).Distinct().Count();
            status.text = all.Count == 0
                ? "No problems"
                : $"{CountOf(all.Count, "problem", "problems")} in {CountOf(assets, "asset", "assets")}" +
                  (shown.Count < all.Count ? $" · {shown.Count} shown" : "");
        }

        private bool Matches(ForgeProblem problem)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var text = search.Trim();
            return NameOf(problem.Asset).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (problem.Asset != null && problem.Asset.name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   problem.Message.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Selects the problem's asset and shows it in the Project window
        private static void Reveal(ForgeProblem problem)
        {
            if (problem?.Asset == null) return;
            Selection.activeObject = problem.Asset;
            EditorGUIUtility.PingObject(problem.Asset);
        }

        private static string NameOf(Object asset) => asset switch
        {
            null => "",
            BaseForgeAsset forge when !string.IsNullOrWhiteSpace(forge.GetName()) => forge.GetName(),
            _ => asset.name,
        };

        private static string CountOf(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
    }
}
