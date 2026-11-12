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
    /// <para>
    /// By Rule, its other view, groups the problems under the rule each breaks, and where a simple step fixes a problem,
    /// gives it a Quick Fix, and its rule a Fix All, each one Undo step (FEAT-25, the user's request).
    /// </para>
    /// <para>
    /// It lists the linter's notes too, which may be meant, as a trigger on an event that code sends, under their own
    /// filter, and its counts leave them out (TW-25).
    /// </para>
    /// </summary>
    public sealed class ForgeProblemsWindow : EditorWindow
    {
        private const string Title = "Problems";

        [SerializeField] private string search = "";
        [SerializeField] private bool showErrors = true;
        [SerializeField] private bool showWarnings = true;
        [SerializeField] private bool showNotes = true;
        [SerializeField] private bool byRule;

        private MultiColumnListView list;
        private ScrollView rules;
        private ToolbarToggle listToggle;
        private ToolbarToggle ruleToggle;
        // The rules folded in the By Rule view, by name, for the editor session
        private static readonly HashSet<string> folded = new();
        private ToolbarToggle errorsToggle;
        private ToolbarToggle warningsToggle;
        private ToolbarToggle notesToggle;
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
            // The list, or By Rule (FEAT-25)
            listToggle = Filter(toolbar, "List", !byRule, "Each problem, the errors first, by asset", on => ShowView(!on));
            listToggle.text = "List";
            ruleToggle = Filter(toolbar, "ByRule", byRule, "The problems grouped under the rule each breaks, with their quick fixes", on => ShowView(on));
            ruleToggle.text = "By Rule";
            toolbar.Add(new ToolbarSpacer());
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
            notesToggle = Filter(toolbar, "Notes", showNotes, "Show the notes, which may be meant, as a trigger on an event that code sends", on =>
            {
                showNotes = on;
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
                bindCell = (cell, index) => ShowSeverity((ForgeGlyph)cell, shown[index]),
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

            rules = new ScrollView(ScrollViewMode.Vertical) { name = "ProblemRules" };
            rules.AddToClassList("pf-problems__rules");
            root.Add(rules);

            status = new Label { name = "ProblemsStatus" };
            status.AddToClassList("pf-problems__status");
            root.Add(status);
            ShowView(byRule);
        }

        // Shows the list or By Rule, the toolbar's two toggles taking turns
        private void ShowView(bool rulesShown)
        {
            byRule = rulesShown;
            listToggle?.SetValueWithoutNotify(!rulesShown);
            ruleToggle?.SetValueWithoutNotify(rulesShown);
            list?.EnableInClassList("pf-hidden", rulesShown);
            rules?.EnableInClassList("pf-hidden", !rulesShown);
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

        // Shows the problems and notes the filters let through, the errors first and the notes last, then by asset
        private void Refill()
        {
            if (list == null) return;
            var problems = ForgeValidator.All;
            var notes = ForgeValidator.Notes;
            var all = problems.Concat(notes).ToList();
            shown = all
                .Where(Shows)
                .Where(Matches)
                .OrderByDescending(problem => problem.Severity)
                .ThenBy(problem => NameOf(problem.Asset), StringComparer.OrdinalIgnoreCase)
                .ToList();
            list.itemsSource = shown;
            list.RefreshItems();
            if (byRule) FillRules();

            int errors = problems.Count(problem => problem.Severity == ForgeProblemSeverity.Error);
            int warnings = problems.Count - errors;
            errorsToggle.text = CountOf(errors, "Error", "Errors");
            warningsToggle.text = CountOf(warnings, "Warning", "Warnings");
            notesToggle.text = CountOf(notes.Count, "Note", "Notes");
            int assets = problems.Select(problem => problem.Asset).Distinct().Count();
            int fixable = shown.Count(problem => problem.Fix != null);
            // The problems, and the notes beside them, which they don't count (TW-25)
            status.text = (problems.Count == 0 ? "No problems" : $"{CountOf(problems.Count, "problem", "problems")} in {CountOf(assets, "asset", "assets")}") +
                          (notes.Count > 0 ? $" · {CountOf(notes.Count, "note", "notes")}" : "") +
                          (shown.Count < all.Count ? $" · {shown.Count} shown" : "") +
                          (byRule && fixable > 0 ? $" · {fixable} with a quick fix" : "");
        }

        // Whether the toolbar's filters let a problem's severity through
        private bool Shows(ForgeProblem problem) => problem.Severity switch
        {
            ForgeProblemSeverity.Error => showErrors,
            ForgeProblemSeverity.Note => showNotes,
            _ => showWarnings,
        };

        // A problem's severity as its glyph: a warning's triangle, an error's in the destructive color, and a note's info
        // mark in the neutral one (TW-25)
        private static void ShowSeverity(ForgeGlyph glyph, ForgeProblem problem)
        {
            glyph.Shape = problem.Severity == ForgeProblemSeverity.Note ? ForgeGlyphShape.Info : ForgeGlyphShape.Warning;
            glyph.EnableInClassList("pf-glyph--warning", problem.Severity == ForgeProblemSeverity.Warning);
            glyph.EnableInClassList("pf-glyph--destructive", problem.Severity == ForgeProblemSeverity.Error);
            glyph.tooltip = problem.Severity.ToString();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // By Rule (FEAT-25)
        // ═══════════════════════════════════════════════════════════════════════════

        // The shown problems under their rules: a rule with errors first, then by name; each rule's problems as the list
        // orders them
        private void FillRules()
        {
            rules.Clear();
            if (shown.Count == 0)
            {
                var none = new Label("No problems") { name = "NoProblems" };
                none.AddToClassList("pf-note");
                rules.Add(none);
                return;
            }
            var groups = shown.GroupBy(problem => problem.Rule?.Name ?? "Other")
                .OrderByDescending(group => group.Max(problem => problem.Severity))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var group in groups) rules.Add(RuleGroup(group.Key, group.ToList()));
        }

        // A rule's header, with its name, how many problems break it, and Fix All, then a row for each of its problems
        private VisualElement RuleGroup(string rule, List<ForgeProblem> problems)
        {
            var box = new VisualElement { name = "RuleGroup" };
            box.AddToClassList("pf-problems__group");

            var header = new VisualElement { name = "RuleHeader" };
            header.AddToClassList("pf-problems__group-header");
            var arrow = new ForgeIconButton(ForgeGlyphShape.ArrowDown, "Collapse", null) { name = "RuleFold" };
            arrow.AddToClassList("pf-element__arrow");
            header.Add(arrow);
            var name = new Label(rule) { name = "RuleName" };
            name.AddToClassList("pf-problems__group-name");
            header.Add(name);
            var count = new Label(problems.Count.ToString()) { name = "RuleCount", tooltip = CountOf(problems.Count, "problem", "problems") };
            count.AddToClassList("pf-problems__group-count");
            header.Add(count);
            var space = new VisualElement();
            space.AddToClassList("pf-problems__group-space");
            header.Add(space);
            var fixable = problems.Where(problem => problem.Fix != null).ToList();
            if (fixable.Count > 0)
            {
                var fixAll = new Button(() => Apply(fixable, $"Fix All: {rule}"))
                {
                    name = "FixAll",
                    text = fixable.Count == problems.Count ? "Fix All" : $"Fix {fixable.Count}",
                    tooltip = $"{fixable[0].Fix.Name}, for each of the {CountOf(fixable.Count, "problem", "problems")} a step fixes, as one Undo step",
                };
                fixAll.AddToClassList("pf-problems__fix");
                header.Add(fixAll);
            }
            box.Add(header);

            var rows = new VisualElement { name = "RuleProblems" };
            rows.AddToClassList("pf-problems__group-rows");
            for (int i = 0; i < problems.Count; i++) rows.Add(ProblemRow(problems[i], i % 2 == 1));
            box.Add(rows);

            void Fold(bool fold)
            {
                rows.EnableInClassList("pf-hidden", fold);
                arrow.Glyph.Shape = fold ? ForgeGlyphShape.ArrowRight : ForgeGlyphShape.ArrowDown;
                arrow.tooltip = fold ? "Expand" : "Collapse";
            }
            arrow.clicked += () =>
            {
                bool fold = !folded.Contains(rule);
                if (fold) folded.Add(rule);
                else folded.Remove(rule);
                Fold(fold);
            };
            Fold(folded.Contains(rule));
            return box;
        }

        // A problem's row: its severity, its asset, which a click selects, what's wrong, and its quick fix
        private static VisualElement ProblemRow(ForgeProblem problem, bool alternate)
        {
            var row = new VisualElement { name = "RuleProblem" };
            row.AddToClassList("pf-problems__row");
            row.EnableInClassList("pf-problems__row--alternate", alternate);

            var severity = new ForgeGlyph(ForgeGlyphShape.Warning);
            ShowSeverity(severity, problem);
            row.Add(severity);

            var asset = new VisualElement();
            asset.AddToClassList("pf-problems__asset");
            var type = problem.Asset != null ? problem.Asset.GetType() : null;
            var icon = new ForgeTypeIcon(type ?? typeof(Ability)) { name = "Icon" };
            asset.Add(icon);
            var assetName = new Label(NameOf(problem.Asset)) { name = "Name", tooltip = problem.Asset != null ? AssetDatabase.GetAssetPath(problem.Asset) : "" };
            assetName.AddToClassList("pf-problems__name");
            asset.Add(assetName);
            row.Add(asset);

            var message = new Label(problem.Message) { name = "Message", tooltip = problem.Message };
            message.AddToClassList("pf-problems__message");
            row.Add(message);

            if (problem.Fix != null)
            {
                var fix = new Button(() => Apply(new List<ForgeProblem> { problem }, problem.Fix.Name))
                {
                    name = "Fix",
                    text = problem.Fix.Name,
                    tooltip = $"{problem.Fix.Name}, as one Undo step",
                };
                fix.AddToClassList("pf-problems__fix");
                row.Add(fix);
            }
            row.AddManipulator(new Clickable(() => Reveal(problem)));
            return row;
        }

        /// <summary>
        /// Applies problems' quick fixes as one Undo step, named for what it does. They run from the last to the first, so
        /// a fix that removes a list's element leaves the places of those before it as they were found; one that throws
        /// doesn't keep the others from running. The validator, which follows each edit, checks each asset again.
        /// </summary>
        internal static void Apply(IReadOnlyList<ForgeProblem> problems, string name)
        {
            var fixes = problems.Where(problem => problem?.Fix != null).ToList();
            if (fixes.Count == 0) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            for (int i = fixes.Count - 1; i >= 0; i--)
            {
                try
                {
                    fixes[i].Fix.Apply();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            Undo.SetCurrentGroupName(name);
            Undo.CollapseUndoOperations(group);
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
