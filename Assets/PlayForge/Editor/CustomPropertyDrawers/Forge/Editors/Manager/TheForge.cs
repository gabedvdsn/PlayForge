using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The Forge, PlayForge's main window, in Docs/EDITOR_STYLE.md's terms (Q5): a list of the project's PlayForge assets
    /// beside a detail pane that holds the selected asset's inspector, under a summary of what it does and what uses it
    /// (Q6). The toolbar holds Create, the type filters, Tags, the search and the problem count; the status bar holds the
    /// counts. It follows the project, with no Refresh and no timer (Q30), and the validator's problems with it (Q8,
    /// ED-15). One click selects a row and shows its asset (ED-27). It had Create, View, Analysis and Settings tabs: the
    /// Analysis tab's power scores stay hidden until Items and Entities score too (Q7), and the Settings tab's settings
    /// are on the two settings pages (ED-21).
    /// </summary>
    public sealed class TheForge : EditorWindow, IHasCustomMenu
    {
        // The title is the menu item's name. It said "PlayForge Manager" (ED-19)
        private const string WindowTitle = "The Forge";
        internal const string PREFS_PREFIX = PlayForgePreferences.Prefix;
        private const string HiddenClass = "pf-forge__hidden";

        /// <summary>The asset types The Forge lists, in its order.</summary>
        internal static readonly Type[] Types = PlayForgeSettings.Kinds.Select(kind => kind.type).ToArray();

        // What the window shows, which a domain reload keeps
        [SerializeField] private string search = "";
        [SerializeField] private string typeFilter = "";
        [SerializeField] private bool tagsShown;
        [SerializeField] private Object selected;
        [SerializeField] private string selectedTag = "";
        [SerializeField] private string sortColumn = "Name";
        [SerializeField] private bool sortAscending = true;

        private MultiColumnListView assetList;
        private MultiColumnListView tagList;
        private VisualElement detail;
        private Label status;
        private ToolbarButton problemCount;
        private ToolbarSearchField searchField;
        private ToolbarToggle tagsToggle;
        private readonly Dictionary<string, ToolbarToggle> typeToggles = new();
        private List<ScriptableObject> allAssets = new();
        private List<ScriptableObject> shownAssets = new();
        private List<ForgeTagRegistry.TagUsageRecord> shownTags = new();
        private UnityEditor.Editor detailEditor;
        private Object shownInDetail;
        private bool reloadQueued;

        /// <summary>The asset the detail pane shows.</summary>
        internal Object Selected => selected;

        // ═══════════════════════════════════════════════════════════════════════════
        // Opening
        // ═══════════════════════════════════════════════════════════════════════════

        // Under Window, where the Asset Store expects a package's menu (rule 2.5.1.a, ED-19)
        [MenuItem("Window/PlayForge/" + WindowTitle, false, 0)]
        public static void ShowWindow() => Open();

        // A shortcut, rebindable in Edit → Shortcuts, opens The Forge with its search focused (Q29)
        [Shortcut("PlayForge/The Forge", KeyCode.F, ShortcutModifiers.Action | ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void OpenToSearch()
        {
            var forge = Open();
            forge.Focus();
            forge.searchField?.Q<TextField>()?.Focus();
        }

        internal static TheForge Open()
        {
            var window = GetWindow<TheForge>();
            window.titleContent = new GUIContent(WindowTitle, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(720, 400);
            window.Show();
            return window;
        }

        /// <summary>
        /// Opens The Forge on an asset, its type's filter on, with the asset selected, as an inspector's Open in The Forge
        /// does. It filtered by the asset's name, and showed the tab as it was (ED-27).
        /// </summary>
        public static void OpenToAsset(Object asset)
        {
            if (asset == null) return;
            var forge = Open();
            forge.Focus();
            forge.tagsShown = false;
            forge.search = "";
            forge.typeFilter = Types.FirstOrDefault(type => type.IsInstanceOfType(asset))?.Name ?? "";
            forge.selected = asset;
            forge.Refill();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Problems…"), false, () => ForgeProblemsWindow.ShowWindow());
            menu.AddItem(new GUIContent("Project Settings…"), false, PlayForgeSettingsPages.OpenProjectSettings);
            menu.AddItem(new GUIContent("Preferences…"), false, PlayForgeSettingsPages.OpenPreferences);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Following the project
        // ═══════════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            EditorApplication.projectChanged += QueueReload;
            ForgeValidator.Changed += OnProblemsChanged;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= QueueReload;
            ForgeValidator.Changed -= OnProblemsChanged;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            DestroyDetailEditor();
        }

        // A change to the project reads it again, once, after the change settles
        private void QueueReload()
        {
            if (reloadQueued) return;
            reloadQueued = true;
            EditorApplication.delayCall += () =>
            {
                reloadQueued = false;
                if (this != null) Reload();
            };
        }

        // An edit to a listed asset, as a rename in its inspector, redraws the rows
        private void OnObjectChanges(ref ObjectChangeEventStream stream)
        {
            for (int i = 0; i < stream.length; i++)
            {
                if (stream.GetEventType(i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
                stream.GetChangeAssetObjectPropertiesEvent(i, out var edited);
                if (EditorUtility.InstanceIDToObject(edited.instanceId) is ScriptableObject asset && allAssets.Contains(asset))
                {
                    assetList?.RefreshItems();
                    return;
                }
            }
        }

        private void OnProblemsChanged()
        {
            assetList?.RefreshItems();
            ShowCounts();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Building
        // ═══════════════════════════════════════════════════════════════════════════

        private void CreateGUI()
        {
            var root = rootVisualElement;
            // PlayForge's style sheet, in the editor's theme. It painted the window a hard-coded dark gray (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-forge");
            root.Add(BuildToolbar());

            var split = new TwoPaneSplitView(0, 360, TwoPaneSplitViewOrientation.Horizontal) { name = "Split" };
            split.AddToClassList("pf-forge__split");
            var lists = new VisualElement { name = "Lists" };
            lists.AddToClassList("pf-forge__lists");
            assetList = BuildAssetList();
            tagList = BuildTagList();
            lists.Add(assetList);
            lists.Add(tagList);
            split.Add(lists);
            var scroll = new ScrollView { name = "DetailScroll" };
            scroll.AddToClassList("pf-forge__detail-scroll");
            detail = new VisualElement { name = "Detail" };
            detail.AddToClassList("pf-forge__detail");
            scroll.Add(detail);
            split.Add(scroll);
            root.Add(split);

            status = new Label { name = "ForgeStatus" };
            status.AddToClassList("pf-forge__status");
            root.Add(status);
            Reload();
        }

        // Create, the type filters, each with its type icon, Tags, the search and the problem count
        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar { name = "ForgeToolbar" };
            var create = new ToolbarMenu { name = "Create", text = "Create", tooltip = "Make a new asset where Project Settings puts its type" };
            foreach (var type in Types)
                create.menu.AppendAction(ForgeAssetTypes.NameOf(type), _ => ForgeNewAssetWindow.Show(type, OpenToAsset));
            toolbar.Add(create);

            foreach (var type in Types)
            {
                var name = ForgeAssetTypes.NameOf(type);
                var toggle = new ToolbarToggle { name = type.Name, tooltip = $"Show only {name} assets" };
                toggle.AddToClassList("pf-forge__filter");
                var glyph = ForgeAssetTypes.GlyphOf(type);
                if (glyph != ForgeGlyphShape.None) toggle.Add(new ForgeTypeIcon(type));
                else toggle.text = name;
                toggle.RegisterValueChangedCallback(change =>
                {
                    typeFilter = change.newValue ? type.Name : typeFilter == type.Name ? "" : typeFilter;
                    if (change.newValue) tagsShown = false;
                    Refill();
                });
                typeToggles[type.Name] = toggle;
                toolbar.Add(toggle);
            }
            tagsToggle = new ToolbarToggle { name = "Tags", text = "Tags", tooltip = "Show the project's tags and what uses each" };
            tagsToggle.RegisterValueChangedCallback(change =>
            {
                tagsShown = change.newValue;
                if (tagsShown) typeFilter = "";
                Refill();
            });
            toolbar.Add(tagsToggle);

            toolbar.Add(new ToolbarSpacer { flex = true });
            searchField = new ToolbarSearchField { name = "Search", tooltip = "Show what has this text in its name, file name or tag" };
            searchField.AddToClassList("pf-forge__search");
            searchField.SetValueWithoutNotify(search);
            searchField.RegisterValueChangedCallback(change =>
            {
                search = change.newValue ?? "";
                Refill();
            });
            toolbar.Add(searchField);
            problemCount = new ToolbarButton(() => ForgeProblemsWindow.ShowWindow()) { name = "ProblemCount", tooltip = "Open the Problems view" };
            problemCount.AddToClassList("pf-forge__problems");
            toolbar.Add(problemCount);
            return toolbar;
        }

        // The assets: a sortable column for the name with its type icon, the type, the asset tag and the levels, then the
        // problem glyph. A click selects a row and a double click shows the asset in the Project window
        private MultiColumnListView BuildAssetList()
        {
            var list = new MultiColumnListView
            {
                name = "Assets",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                sortingMode = ColumnSortingMode.Custom,
            };
            list.AddToClassList("pf-forge__list");
            list.columns.Add(new Column
            {
                name = "Name", title = "Name", width = 160, stretchable = true, sortable = true,
                makeCell = () =>
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("pf-forge__name-cell");
                    cell.Add(new ForgeTypeIcon(typeof(Ability)) { name = "Icon" });
                    var name = new Label { name = "Name" };
                    name.AddToClassList("pf-forge__name");
                    cell.Add(name);
                    var file = new Label { name = "File" };
                    file.AddToClassList("pf-forge__file");
                    cell.Add(file);
                    return cell;
                },
                bindCell = (cell, index) =>
                {
                    var asset = shownAssets[index];
                    ShowTypeIcon(cell.Q<ForgeGlyph>("Icon"), asset.GetType());
                    cell.Q<Label>("Name").text = NameOf(asset);
                    var file = cell.Q<Label>("File");
                    bool showFile = PlayForgePreferences.ShowFileNames.Value && asset.name != NameOf(asset);
                    file.text = showFile ? asset.name : "";
                    file.EnableInClassList(HiddenClass, !showFile);
                    cell.tooltip = AssetDatabase.GetAssetPath(asset);
                },
            });
            list.columns.Add(TextColumn("Type", "Type", 100, asset => ForgeAssetTypes.NameOf(asset.GetType())));
            list.columns.Add(TextColumn("AssetTag", "Asset Tag", 130, AssetTagOf));
            list.columns.Add(TextColumn("Levels", "Levels", 56, LevelsOf));
            list.columns.Add(new Column
            {
                name = "Problems", title = "", width = 24, resizable = false, sortable = true,
                makeCell = () => new ForgeGlyph(ForgeGlyphShape.Warning),
                bindCell = (cell, index) =>
                {
                    var problems = ForgeValidator.ProblemsOf(shownAssets[index]);
                    cell.EnableInClassList(HiddenClass, problems.Count == 0);
                    cell.EnableInClassList("pf-glyph--warning", problems.Count > 0 && problems.All(problem => problem.Severity == ForgeProblemSeverity.Warning));
                    cell.EnableInClassList("pf-glyph--destructive", problems.Any(problem => problem.Severity == ForgeProblemSeverity.Error));
                    cell.tooltip = string.Join("\n", problems.Select(problem => problem.Message));
                },
            });
            list.columnSortingChanged += () =>
            {
                var sort = list.sortedColumns.FirstOrDefault();
                sortColumn = sort?.columnName ?? "Name";
                sortAscending = sort == null || sort.direction == SortDirection.Ascending;
                Refill();
            };
            list.selectionChanged += chosen =>
            {
                var asset = chosen.OfType<ScriptableObject>().FirstOrDefault();
                if (asset == null) return;
                selected = asset;
                ShowDetail();
            };
            list.itemsChosen += chosen => ShowInProject(chosen.OfType<Object>().FirstOrDefault());
            list.AddManipulator(new ContextualMenuManipulator(AssetMenu));
            return list;
        }

        private Column TextColumn(string name, string title, float width, Func<ScriptableObject, string> text) => new()
        {
            name = name, title = title, width = width, sortable = true,
            makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("pf-forge__cell");
                return label;
            },
            bindCell = (cell, index) => ((Label)cell).text = text(shownAssets[index]),
        };

        // The tags: each one, by its full path or its leaf as the tag preferences say, what it's for, and how many assets
        // use it
        private MultiColumnListView BuildTagList()
        {
            var list = new MultiColumnListView
            {
                name = "TagList",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            list.AddToClassList("pf-forge__list");
            list.columns.Add(TagColumn("Tag", "Tag", 180, record => TagText(record.Tag), true));
            list.columns.Add(TagColumn("Contexts", "For", 120, record => string.Join(", ", record.UsageByContext.Keys.OrderBy(key => key)), false));
            list.columns.Add(TagColumn("Uses", "Uses", 50, record => record.TotalAssetCount.ToString(), false));
            list.selectionChanged += chosen =>
            {
                var record = chosen.OfType<ForgeTagRegistry.TagUsageRecord>().FirstOrDefault();
                if (record == null) return;
                selectedTag = record.Tag.Name;
                ShowDetail();
            };
            return list;
        }

        private Column TagColumn(string name, string title, float width, Func<ForgeTagRegistry.TagUsageRecord, string> text, bool stretch) => new()
        {
            name = name, title = title, width = width, stretchable = stretch,
            makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("pf-forge__cell");
                return label;
            },
            bindCell = (cell, index) => ((Label)cell).text = text(shownTags[index]),
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // Showing
        // ═══════════════════════════════════════════════════════════════════════════

        // Reads the project's assets again, then shows what the filters let through
        private void Reload()
        {
            allAssets = ForgeValidator.ProjectAssets().ToList();
            if (selected == null) selected = null;
            Refill();
        }

        private void Refill()
        {
            if (assetList == null) return;
            foreach (var (name, toggle) in typeToggles) toggle.SetValueWithoutNotify(!tagsShown && typeFilter == name);
            tagsToggle.SetValueWithoutNotify(tagsShown);
            searchField.SetValueWithoutNotify(search);
            assetList.EnableInClassList(HiddenClass, tagsShown);
            tagList.EnableInClassList(HiddenClass, !tagsShown);

            if (tagsShown)
            {
                shownTags = ForgeTagRegistry.GetAllTagRecords()
                    .Where(record => Matches(TagText(record.Tag)) || Matches(record.Tag.Name))
                    .OrderBy(record => TagText(record.Tag), Comparer<string>.Create(EditorUtility.NaturalCompare))
                    .ToList();
                tagList.itemsSource = shownTags;
                tagList.RefreshItems();
                int at = shownTags.FindIndex(record => record.Tag.Name == selectedTag);
                if (at >= 0) tagList.SetSelectionWithoutNotify(new[] { at });
                else tagList.ClearSelection();
            }
            else
            {
                shownAssets = Sorted(allAssets.Where(Shows), sortColumn, sortAscending);
                assetList.itemsSource = shownAssets;
                assetList.RefreshItems();
                int at = selected != null ? shownAssets.IndexOf(selected as ScriptableObject) : -1;
                if (at >= 0)
                {
                    assetList.SetSelectionWithoutNotify(new[] { at });
                    assetList.ScrollToItem(at);
                }
                else assetList.ClearSelection();
            }
            ShowDetail();
            ShowCounts();
        }

        // Whether the filters and the search let an asset through
        private bool Shows(ScriptableObject asset)
        {
            if (asset == null) return false;
            if (!string.IsNullOrEmpty(typeFilter) && Types.FirstOrDefault(type => type.Name == typeFilter)?.IsInstanceOfType(asset) != true)
                return false;
            return Matches(NameOf(asset)) || Matches(asset.name) || Matches(AssetTagOf(asset));
        }

        private bool Matches(string text) =>
            string.IsNullOrWhiteSpace(search) || (text ?? "").IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

        private void ShowCounts()
        {
            if (status == null) return;
            var problems = ForgeValidator.All;
            int errors = problems.Count(problem => problem.Severity == ForgeProblemSeverity.Error);
            problemCount.text = problems.Count == 0 ? "No problems" : CountOf(problems.Count, "problem", "problems");
            problemCount.EnableInClassList("pf-forge__problems--some", problems.Count > 0);
            problemCount.EnableInClassList("pf-forge__problems--error", errors > 0);
            var listed = tagsShown
                ? (shownTags.Count == ForgeTagRegistry.GetAllTagRecords().Count() ? CountOf(shownTags.Count, "tag", "tags") : $"{shownTags.Count} of {CountOf(ForgeTagRegistry.GetAllTagRecords().Count(), "tag", "tags")}")
                : (shownAssets.Count == allAssets.Count ? CountOf(allAssets.Count, "asset", "assets") : $"{shownAssets.Count} of {CountOf(allAssets.Count, "asset", "assets")}");
            status.text = $"{listed} · {(problems.Count == 0 ? "no problems" : CountOf(problems.Count, "problem", "problems"))}";
        }

        // The detail pane: the selected asset's summary and inspector, or the selected tag and what uses it
        private void ShowDetail()
        {
            if (detail == null) return;
            if (tagsShown)
            {
                shownInDetail = null;
                DestroyDetailEditor();
                detail.Clear();
                ShowTagDetail();
                return;
            }
            if (selected != null && selected == shownInDetail && detailEditor != null) return;
            DestroyDetailEditor();
            detail.Clear();
            shownInDetail = selected;
            if (selected == null)
            {
                detail.Add(Note("Select an asset to see it here."));
                return;
            }

            var summary = new VisualElement { name = "Summary" };
            summary.AddToClassList("pf-forge__summary");
            detail.Add(summary);
            detailEditor = UnityEditor.Editor.CreateEditor(selected);
            var inspector = new InspectorElement(detailEditor) { name = "Inspector" };
            detail.Add(inspector);
            var asset = selected;
            FillSummary(summary, asset);
            // Once the inspector is built, and again as the asset changes, the summary reads its sections' lines
            summary.schedule.Execute(() => FillSummary(summary, asset));
            summary.TrackSerializedObjectValue(detailEditor.serializedObject, _ => summary.schedule.Execute(() => FillSummary(summary, asset)));
        }

        // What the asset does, by its description and its sections' one-line summaries, and what's wrong with it
        private void FillSummary(VisualElement summary, Object asset)
        {
            if (asset == null || detailEditor == null) return;
            summary.Clear();
            var title = new Label("Summary");
            title.AddToClassList("pf-forge__summary-title");
            summary.Add(title);
            var description = asset is BaseForgeAsset forge ? forge.GetDescription() : null;
            if (!string.IsNullOrWhiteSpace(description))
            {
                var text = new Label(description);
                text.AddToClassList("pf-forge__summary-description");
                summary.Add(text);
            }
            if (detailEditor is BasePlayForgeEditor editor)
            {
                foreach (var section in editor.Sections)
                {
                    var line = section.Summary?.text;
                    if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(section.Title)) continue;
                    summary.Add(Row(section.Title, line));
                }
            }
            var problems = ForgeValidator.ProblemsOf(asset);
            if (problems.Count > 0) summary.Add(Row("Problems", string.Join(" ", problems.Select(problem => problem.Message))));
        }

        private static VisualElement Row(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("pf-forge__summary-row");
            var name = new Label(label);
            name.AddToClassList("pf-forge__summary-label");
            row.Add(name);
            var text = new Label(value);
            text.AddToClassList("pf-forge__summary-value");
            text.tooltip = value;
            row.Add(text);
            return row;
        }

        // A tag: its path, what it's for, and each asset that uses it, as a row that shows the asset
        private void ShowTagDetail()
        {
            var record = shownTags.FirstOrDefault(each => each.Tag.Name == selectedTag);
            if (record == null)
            {
                detail.Add(Note("Select a tag to see what uses it."));
                return;
            }
            var summary = new VisualElement { name = "Summary" };
            summary.AddToClassList("pf-forge__summary");
            var title = new Label(record.Tag.Name);
            title.AddToClassList("pf-forge__summary-title");
            summary.Add(title);
            summary.Add(Row("For", string.Join(", ", record.UsageByContext.Keys.OrderBy(key => key))));
            summary.Add(Row("Uses", CountOf(record.TotalAssetCount, "asset", "assets")));
            detail.Add(summary);

            var users = new VisualElement { name = "TagUsers" };
            users.AddToClassList("pf-forge__users");
            var usages = record.UsageByContext.Values.SelectMany(usage => usage.Usages)
                .Where(usage => usage.Asset != null)
                .GroupBy(usage => usage.Asset)
                .OrderBy(group => NameOf(group.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var group in usages)
            {
                var asset = group.Key;
                var row = new Button(() => OpenToAsset(asset)) { tooltip = "Show this asset in The Forge" };
                row.AddToClassList("pf-forge__user");
                var icon = new ForgeTypeIcon(asset.GetType());
                row.Add(icon);
                var name = new Label(NameOf(asset));
                name.AddToClassList("pf-forge__user-name");
                row.Add(name);
                var fields = new Label(string.Join(", ", group.Select(usage => usage.FriendlyFieldName).Where(field => field.Length > 0).Distinct()));
                fields.AddToClassList("pf-forge__user-field");
                row.Add(fields);
                users.Add(row);
            }
            if (users.childCount == 0) users.Add(Note("No asset uses it: it's declared, or a default."));
            detail.Add(users);
        }

        private void DestroyDetailEditor()
        {
            if (detailEditor != null) DestroyImmediate(detailEditor);
            detailEditor = null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Acting on an asset
        // ═══════════════════════════════════════════════════════════════════════════

        // The selected asset's actions, in its right-click menu
        private void AssetMenu(ContextualMenuPopulateEvent menu)
        {
            var asset = selected as ScriptableObject;
            var status = asset != null && !tagsShown ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            menu.menu.AppendAction("Show in Project", _ => ShowInProject(asset), status);
            menu.menu.AppendAction("Open in a Window", _ => EditorUtility.OpenPropertyEditor(asset), status);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Duplicate", _ =>
            {
                var copy = ForgeAssetCreation.Duplicate(asset);
                if (copy != null) OpenToAsset(copy);
            }, status);
            menu.menu.AppendAction("Move to Trash", _ => ForgeAssetDeletion.MoveToTrash(asset), status);
        }

        private static void ShowInProject(Object asset)
        {
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What a row shows
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>An asset's name: its own, or its file's when it has none.</summary>
        internal static string NameOf(Object asset) => asset switch
        {
            null => "",
            BaseForgeAsset forge when !string.IsNullOrWhiteSpace(forge.GetName()) => forge.GetName(),
            _ => asset.name,
        };

        private static string AssetTagOf(ScriptableObject asset)
        {
            if (asset is not BaseForgeLevelProvider provider) return "";
            var tag = provider.GetAssetTag();
            return tag.IsValid ? tag.Name : "";
        }

        // The levels an asset uses, as "1–5", or nothing for an asset without levels of its own
        private static string LevelsOf(ScriptableObject asset)
        {
            if (asset is not BaseForgeLevelProvider provider || asset is GameplayEffect) return "";
            int max = provider.GetMaxLevel();
            return max <= 0 ? "" : $"{provider.GetStartingLevel()}–{max}";
        }

        /// <summary>A tag, by its full path or by its leaf, as the tag preferences say (ED-25).</summary>
        internal static string TagText(Tag tag)
        {
            var name = tag.Name ?? "";
            if (TagDisplaySettings.ShowFullPath) return name;
            int dot = name.LastIndexOf('.');
            return dot >= 0 ? name.Substring(dot + 1) : name;
        }

        /// <summary>
        /// The assets in a column's order: each column's text, compared in natural order, so 9 comes before 10, then by
        /// name (ED-7).
        /// </summary>
        internal static List<ScriptableObject> Sorted(IEnumerable<ScriptableObject> assets, string column, bool ascending)
        {
            Func<ScriptableObject, string> key = column switch
            {
                "Type" => asset => ForgeAssetTypes.NameOf(asset.GetType()),
                "AssetTag" => AssetTagOf,
                "Levels" => LevelsOf,
                "Problems" => asset => ForgeValidator.ProblemsOf(asset).Count.ToString(),
                _ => NameOf,
            };
            var natural = Comparer<string>.Create(EditorUtility.NaturalCompare);
            var ordered = ascending ? assets.OrderBy(key, natural) : assets.OrderByDescending(key, natural);
            return ordered.ThenBy(NameOf, natural).ToList();
        }

        private static readonly string[] Modifiers = { "ability", "effect", "entity", "item", "attribute", "attribute-set" };

        // A reused cell's type icon, set to another type
        private static void ShowTypeIcon(ForgeGlyph icon, Type type)
        {
            if (icon == null) return;
            icon.Shape = ForgeAssetTypes.GlyphOf(type);
            var modifier = ForgeAssetTypes.ModifierOf(type);
            foreach (var each in Modifiers) icon.EnableInClassList($"{ForgeTypeIcon.TypeIconClass}--{each}", each == modifier);
            icon.EnableInClassList(HiddenClass, icon.Shape == ForgeGlyphShape.None);
        }

        private static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("pf-note");
            return note;
        }

        private static string CountOf(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
    }
}
