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
        [SerializeField] private string tagSortColumn = "Tag";
        [SerializeField] private bool tagSortAscending = true;

        private MultiColumnListView assetList;
        private MultiColumnListView tagList;
        private VisualElement detail;
        private Label status;
        private ToolbarButton problemCount;
        private ToolbarSearchField searchField;
        private ToolbarToggle tagsToggle;
        private readonly Dictionary<string, ToolbarToggle> typeToggles = new();
        private ToolbarMenu moreMenu;

        // The types the toolbar shows as toggles: the PlayForge assets; the rest are under More (FN-12, Decision 72)
        private static bool IsCommon(Type type) => typeof(BaseForgeAsset).IsAssignableFrom(type);
        private List<ScriptableObject> allAssets = new();
        private List<ScriptableObject> shownAssets = new();
        private List<ForgeTagRegistry.TagUsageRecord> shownTags = new();
        private UnityEditor.Editor detailEditor;
        private Object shownInDetail;

        // Back and forward over the assets the detail pane has shown, as a browser's, which a domain reload keeps (ED-76)
        [SerializeField] private List<Object> backHistory = new();
        [SerializeField] private List<Object> forwardHistory = new();
        private ToolbarButton backButton;
        private ToolbarButton forwardButton;
        private const int MostHistory = 50;

        // The active Forge Project, whose assets the list shows, and the others to switch to (FEAT-34)
        private ToolbarMenu projectMenu;

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
            forge.Visit(asset);
            forge.selected = asset;
            forge.Refill();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Problems…"), false, () => ForgeProblemsWindow.ShowWindow());
            menu.AddItem(new GUIContent("Project Settings…"), false, PlayForgeSettingsPages.OpenProjectSettings);
            menu.AddItem(new GUIContent("Preferences…"), false, PlayForgeSettingsPages.OpenPreferences);

            // Numbers by level and item stats, to a spreadsheet and back: the project's, or the selected assets' (TW-27)
            menu.AddSeparator("");
            var chosen = SelectedAssets();
            var items = chosen.OfType<Item>().ToList();
            menu.AddItem(new GUIContent("Spreadsheets/Export Numbers…"), false, () => ForgeSpreadsheets.ExportNumbers(allAssets));
            if (chosen.Count > 0) menu.AddItem(new GUIContent("Spreadsheets/Export Selected Numbers…"), false, () => ForgeSpreadsheets.ExportNumbers(chosen));
            else menu.AddDisabledItem(new GUIContent("Spreadsheets/Export Selected Numbers…"));
            menu.AddItem(new GUIContent("Spreadsheets/Export Item Stats…"), false, () => ForgeSpreadsheets.ExportStats(allAssets.OfType<Item>()));
            if (items.Count > 0) menu.AddItem(new GUIContent("Spreadsheets/Export Selected Item Stats…"), false, () => ForgeSpreadsheets.ExportStats(items));
            else menu.AddDisabledItem(new GUIContent("Spreadsheets/Export Selected Item Stats…"));
            menu.AddItem(new GUIContent("Spreadsheets/Import…"), false, ForgeSpreadsheets.Import);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Following the project
        // ═══════════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            open.Add(this);
            EditorApplication.projectChanged += QueueReload;
            ForgeProjects.Changed += QueueReload;
            ForgeValidator.Changed += OnProblemsChanged;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
        }

        private void OnDisable()
        {
            open.Remove(this);
            EditorApplication.projectChanged -= QueueReload;
            ForgeProjects.Changed -= QueueReload;
            EditorApplication.update -= ReloadQueued;
            ForgeValidator.Changed -= OnProblemsChanged;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            DestroyDetailEditor();
        }

        // Each open Forge, which an import tells
        private static readonly HashSet<TheForge> open = new();

        // An import, of an asset made, deleted, moved or changed on disk, reads the project again in each open Forge
        internal static void ReloadOpenWindows()
        {
            foreach (var forge in open) forge.QueueReload();
        }

        // A change to the project reads it again, once, on the editor's next update. A delayCall, which waits for the
        // inspectors to update, ran late or not at all in batch mode
        private void QueueReload()
        {
            EditorApplication.update -= ReloadQueued;
            EditorApplication.update += ReloadQueued;
        }

        private void ReloadQueued()
        {
            EditorApplication.update -= ReloadQueued;
            if (this != null) Reload();
        }

        // An edit to a listed asset, as a rename in its inspector, redraws the rows; an asset made or destroyed reads the
        // project again
        private void OnObjectChanges(ref ObjectChangeEventStream stream)
        {
            bool redraw = false;
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.CreateAssetObject:
                    case ObjectChangeKind.DestroyAssetObject:
                        QueueReload();
                        return;
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var edited);
                        redraw |= EditorUtility.InstanceIDToObject(edited.instanceId) is ScriptableObject asset && allAssets.Contains(asset);
                        break;
                }
            }
            if (redraw) assetList?.RefreshItems();
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

            // The list as wide as the user left it; its columns' widths are kept as well (ED-48, ED-72)
            var split = new TwoPaneSplitView(0, ListWidth, TwoPaneSplitViewOrientation.Horizontal) { name = "Split" };
            split.AddToClassList("pf-forge__split");
            // The asset list and the tag list take turns in one place, each laid out while the other shows, so each keeps
            // its columns' widths (ED-48)
            var lists = ForgeStack.Make(new VisualElement { name = "Lists" });
            lists.AddToClassList("pf-forge__lists");
            lists.RegisterCallback<GeometryChangedEvent>(change =>
            {
                if (change.newRect.width > 0f && Mathf.Abs(change.newRect.width - change.oldRect.width) > 0.5f) ListWidth = change.newRect.width;
            });
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

            // Alt+Left and Alt+Right, and the mouse's back and forward buttons, go back and forward, but in a text field,
            // where the keys move the cursor (ED-76)
            root.RegisterCallback<KeyDownEvent>(key =>
            {
                if (!key.altKey || key.keyCode is not (KeyCode.LeftArrow or KeyCode.RightArrow)) return;
                if ((key.target as VisualElement)?.GetFirstAncestorOfType<TextField>() != null || key.target is TextField) return;
                if (key.keyCode == KeyCode.LeftArrow) GoBack();
                else GoForward();
                key.StopPropagation();
            });
            root.RegisterCallback<PointerDownEvent>(down =>
            {
                if (down.button == 3) GoBack();
                else if (down.button == 4) GoForward();
            }, TrickleDown.TrickleDown);
            Reload();
            ShowHistory();
        }

        // Create, the type filters, each with its type icon, Tags, the search and the problem count
        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar { name = "ForgeToolbar" };
            // Back and forward over the assets shown, as a browser's (ED-76)
            backButton = new ToolbarButton(GoBack) { name = "Back", tooltip = "Back to the asset shown before (Alt+Left)" };
            backButton.Add(new ForgeGlyph(ForgeGlyphShape.Back));
            toolbar.Add(backButton);
            forwardButton = new ToolbarButton(GoForward) { name = "Forward", tooltip = "Forward to the asset gone back from (Alt+Right)" };
            forwardButton.Add(new ForgeGlyph(ForgeGlyphShape.Forward));
            toolbar.Add(forwardButton);
            // The active Forge Project, whose assets the list shows (FEAT-34)
            projectMenu = new ToolbarMenu { name = "Project", tooltip = "The Forge Project whose assets The Forge shows" };
            toolbar.Add(projectMenu);
            var create = new ToolbarMenu { name = "Create", text = "Create", tooltip = "Make a new asset where Project Settings puts its type" };
            FillCreate(create);
            // Again as it opens, so the project's templates are as they are now (TW-24)
            create.RegisterCallback<PointerDownEvent>(_ => FillCreate(create), TrickleDown.TrickleDown);
            toolbar.Add(create);

            // The common types as toggles, the rest under More, which says the one chosen (FN-12, Decision 72)
            foreach (var type in Types.Where(IsCommon))
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
            moreMenu = new ToolbarMenu { name = "More", text = "More", tooltip = "Show only assets of another type" };
            foreach (var type in Types.Where(type => !IsCommon(type)))
            {
                var each = type;
                moreMenu.menu.AppendAction(ForgeAssetTypes.NameOf(each), _ =>
                {
                    typeFilter = typeFilter == each.Name ? "" : each.Name;
                    if (typeFilter != "") tagsShown = false;
                    Refill();
                }, _ => typeFilter == each.Name ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            toolbar.Add(moreMenu);
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
                // A selection moves or copies into another project at once; the first shows in the detail (FEAT-34)
                selectionType = SelectionType.Multiple,
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
                    return cell;
                },
                // The asset's name alone: its file name is in the selected asset's header, under Show in Project (ED-58)
                bindCell = (cell, index) =>
                {
                    var asset = shownAssets[index];
                    ShowTypeIcon(cell.Q<ForgeGlyph>("Icon"), asset.GetType());
                    cell.Q<Label>("Name").text = NameOf(asset);
                    cell.tooltip = AssetDatabase.GetAssetPath(asset);
                },
            });
            list.columns.Add(TextColumn("Type", "Type", 100, asset => ForgeAssetTypes.NameOf(asset.GetType())));
            list.columns.Add(TextColumn("AssetTag", "Asset Tag", 130, AssetTagOf));
            list.columns.Add(TextColumn("Levels", "Levels", 56, LevelsOf));
            // Each asset's one line, as its header shows it, clipped, the whole of it in the cell's tooltip (TW-21)
            list.columns.Add(new Column
            {
                name = "Summary", title = "Summary", width = 220, sortable = true,
                makeCell = () =>
                {
                    var label = new Label();
                    label.AddToClassList("pf-forge__cell");
                    return label;
                },
                bindCell = (cell, index) =>
                {
                    string line = ForgeAssetLines.Kept(shownAssets[index]);
                    ((Label)cell).text = line;
                    cell.tooltip = line;
                },
            });
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
            ForgeColumnWidths.Remember(list.columns, "Forge.Assets");
            // The sort the window keeps through a domain reload shows in the header again (ED-59)
            list.sortColumnDescriptions.Add(new SortColumnDescription(sortColumn, sortAscending ? SortDirection.Ascending : SortDirection.Descending));
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
                Visit(asset);
                selected = asset;
                ShowDetail();
            };
            list.itemsChosen += chosen => ShowInProject(chosen.OfType<Object>().FirstOrDefault());
            list.AddManipulator(new ContextualMenuManipulator(AssetMenu));
            // F2 renames the selected asset, as in the Project window (FN-8)
            list.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode != KeyCode.F2) return;
                var asset = SelectedAssets().FirstOrDefault();
                if (asset == null) return;
                key.StopPropagation();
                ForgeRenameWindow.Show(asset);
            });
            return list;
        }

        // Create's items: each type, and under one with presets, Blank first, then PlayForge's presets, then the project's
        // templates after a line (TW-24)
        private void FillCreate(ToolbarMenu create)
        {
            create.menu.MenuItems().Clear();
            foreach (var type in Types)
            {
                string typeName = ForgeAssetTypes.NameOf(type);
                var presets = ForgePresets.For(type);
                if (presets.Count == 0)
                {
                    create.menu.AppendAction(typeName, _ => ForgeNewAssetWindow.Show(type, OpenToAsset));
                    continue;
                }
                create.menu.AppendAction($"{typeName}/Blank", _ => ForgeNewAssetWindow.Show(type, OpenToAsset));
                bool templates = false;
                foreach (var preset in presets)
                {
                    if (preset.Template != null && !templates)
                    {
                        create.menu.AppendSeparator($"{typeName}/");
                        templates = true;
                    }
                    var each = preset;
                    create.menu.AppendAction($"{typeName}/{each.Name}", _ => ForgeNewAssetWindow.Show(type, OpenToAsset, each));
                }
            }
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
        // use it, each a column that sorts (ED-59)
        private MultiColumnListView BuildTagList()
        {
            var list = new MultiColumnListView
            {
                name = "TagList",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                sortingMode = ColumnSortingMode.Custom,
            };
            list.AddToClassList("pf-forge__list");
            list.columns.Add(TagColumn("Tag", "Tag", 180, record => TagText(record.Tag), true));
            list.columns.Add(TagColumn("Contexts", "For", 120, ContextsOf, false));
            list.columns.Add(TagColumn("Uses", "Uses", 50, record => record.TotalAssetCount.ToString(), false));
            ForgeColumnWidths.Remember(list.columns, "Forge.Tags");
            list.sortColumnDescriptions.Add(new SortColumnDescription(tagSortColumn, tagSortAscending ? SortDirection.Ascending : SortDirection.Descending));
            list.columnSortingChanged += () =>
            {
                var sort = list.sortedColumns.FirstOrDefault();
                tagSortColumn = sort?.columnName ?? "Tag";
                tagSortAscending = sort == null || sort.direction == SortDirection.Ascending;
                Refill();
            };
            list.selectionChanged += chosen =>
            {
                var record = chosen.OfType<ForgeTagRegistry.TagUsageRecord>().FirstOrDefault();
                if (record == null) return;
                selectedTag = record.Tag.Name;
                ShowDetail();
            };
            // Rename… on a tag's row, as F2 on the list, renames it and the tags under it, everywhere they're stored (FN-9)
            list.AddManipulator(new ContextualMenuManipulator(menu =>
            {
                var tag = SelectedTag();
                menu.menu.AppendAction("Rename…", _ => RenameTag(tag), tag.IsValid ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }));
            list.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode != KeyCode.F2) return;
                var tag = SelectedTag();
                if (!tag.IsValid) return;
                key.StopPropagation();
                RenameTag(tag);
            });
            return list;
        }

        // The tag the Tags view has selected, or none
        private Tag SelectedTag() => shownTags.FirstOrDefault(each => each.Tag.Name == selectedTag)?.Tag ?? default;

        /// <summary>Asks for a tag's new path, then renames it and the tags under it, everywhere they're stored (FN-9).</summary>
        internal void RenameTag(Tag tag)
        {
            if (!tag.IsValid) return;
            ForgeNameWindow.Show($"Rename the Tag {tag}", "Path", tag.Path, "Rename", path => ForgeTagRenames.WhyNotRename(tag, path), path =>
            {
                var became = ForgeTagRenames.RenameTag(tag, path);
                selectedTag = became.Name;
                QueueReload();
            });
        }

        private Column TagColumn(string name, string title, float width, Func<ForgeTagRegistry.TagUsageRecord, string> text, bool stretch) => new()
        {
            name = name, title = title, width = width, stretchable = stretch, sortable = true,
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
            // The active Forge Project's assets alone; every asset, while the project has none (FEAT-34)
            var inActive = ForgeProjects.InActive();
            allAssets = ForgeValidator.ProjectAssets().Where(asset => inActive(asset)).ToList();
            ShowProject();
            if (selected == null) selected = null;
            Refill();
        }

        private void Refill()
        {
            if (assetList == null) return;
            foreach (var (name, toggle) in typeToggles) toggle.SetValueWithoutNotify(!tagsShown && typeFilter == name);
            // More says the type it shows, where it's one of its own
            var other = tagsShown ? null : Types.FirstOrDefault(type => !IsCommon(type) && type.Name == typeFilter);
            if (moreMenu != null) moreMenu.text = other != null ? ForgeAssetTypes.NameOf(other) : "More";
            tagsToggle.SetValueWithoutNotify(tagsShown);
            searchField.SetValueWithoutNotify(search);
            ForgeStack.Show(assetList, !tagsShown);
            ForgeStack.Show(tagList, tagsShown);

            if (tagsShown)
            {
                shownTags = SortedTags(ForgeTagRegistry.GetAllTagRecords()
                    .Where(record => Matches(TagText(record.Tag)) || Matches(TagDisplaySettings.PathOf(record.Tag))), tagSortColumn, tagSortAscending);
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

        // The project menu: each project, the active one checked, then New Project and the active one's file (FEAT-34)
        private void ShowProject()
        {
            if (projectMenu == null) return;
            var active = ForgeProjects.Active;
            var projects = ForgeProjects.All();
            projectMenu.text = active != null ? active.name : projects.Count > 0 ? "No Project" : "All Assets";
            projectMenu.menu.ClearItems();
            foreach (var project in projects)
            {
                var each = project;
                projectMenu.menu.AppendAction(each.name, _ => ForgeProjects.Active = each,
                    _ => each == active ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            // The assets in no project, where no project at the root holds them
            if (projects.Count > 0 && projects.All(project => project.Folder != "Assets"))
                projectMenu.menu.AppendAction("No Project", _ => ForgeProjects.Active = null,
                    _ => active == null ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            projectMenu.menu.AppendSeparator();
            projectMenu.menu.AppendAction("New Project…", _ => ForgeNewProjectWindow.Open());
            projectMenu.menu.AppendAction("Show in Project", _ => ShowInProject(active),
                active != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
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
            // The Forge Project the assets are of (FEAT-34)
            var project = tagsShown ? null : ForgeProjects.Active;
            var where = project != null ? $" in {project.name}" : "";
            status.text = $"{listed}{where} · {(problems.Count == 0 ? "no problems" : CountOf(problems.Count, "problem", "problems"))}";
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
            // A Scaler asset's and a Requirement Template's description too, as text to read (ED-78)
            var description = ForgeSummaries.DescriptionOf(asset);
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
            // An inspector with no sections, as a Scaler asset's, said nothing here (ED-74)
            else
            {
                foreach (var (label, line) in ForgeSummaries.LinesOf(asset)) summary.Add(Row(label, line));
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
            // Its own name, its leaf, and under it its whole path where the path says more, as a tag's parents or an asset
            // tag's type (FN-9); the name a unique tag pads, which only tells tags apart, on hover (ED-60)
            var path = record.Tag.Path;
            var primary = record.Tag.GetLeafName();
            var title = new Label(primary) { name = "TagName", tooltip = record.Tag.Name != path ? record.Tag.Name : "" };
            title.AddToClassList("pf-forge__summary-title");
            summary.Add(title);
            if (path != primary)
            {
                var whole = new Label(path) { name = "TagPath", tooltip = "The tag's whole path" };
                whole.AddToClassList("pf-forge__summary-path");
                summary.Add(whole);
            }
            summary.Add(Row("For", ContextsOf(record)));
            summary.Add(Row("Uses", CountOf(record.TotalAssetCount, "asset", "assets")));
            detail.Add(summary);

            // The contexts it's declared for, which put it in those contexts' pickers as well as its uses' (ED-83)
            var tag = record.Tag;
            var declared = new ForgeTagContextsField("Declared For", ForgeTagContexts.DeclaredOf(tag))
            {
                name = "TagContexts",
                tooltip = "The contexts the tag is declared for, in the Tag Library: a field of one offers it, whatever uses it",
            };
            declared.Changed += contexts =>
            {
                ForgeTagContexts.Declare(tag, contexts);
                QueueReload();
            };
            detail.Add(declared);

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

        // How wide the list is, which a user's drag of the split sets, kept for each user as the columns' widths are. It
        // opened 360 wide each time (ED-72)
        private static string ListWidthKey => PlayForgePreferences.Prefix + "Forge.Split";

        internal static float ListWidth
        {
            get => float.TryParse(EditorPrefs.GetString(ListWidthKey, ""), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var width) ? Mathf.Clamp(width, 200f, 1600f) : 360f;
            set => EditorPrefs.SetString(ListWidthKey, Mathf.Clamp(value, 200f, 1600f).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Back and forward (ED-76)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Whether there's an asset to go back to (ED-76).</summary>
        internal bool CanGoBack => backHistory.Any(asset => asset != null);

        /// <summary>Whether there's an asset to go forward to (ED-76).</summary>
        internal bool CanGoForward => forwardHistory.Any(asset => asset != null);

        // A new asset shown: the one before goes on the way back, and the way forward is gone, as a browser's
        private void Visit(Object asset)
        {
            if (asset == null || asset == selected) return;
            if (selected != null)
            {
                backHistory.Add(selected);
                if (backHistory.Count > MostHistory) backHistory.RemoveAt(0);
            }
            forwardHistory.Clear();
            ShowHistory();
        }

        /// <summary>Shows the asset shown before, as a browser's Back (ED-76).</summary>
        internal void GoBack() => Step(backHistory, forwardHistory);

        /// <summary>Shows the asset gone back from, as a browser's Forward (ED-76).</summary>
        internal void GoForward() => Step(forwardHistory, backHistory);

        // The last asset one way, passing over any deleted since, shown, and the one it replaces put on the other way. Its
        // type's filter goes on only where the list hides it, so the list stays as it is where it can
        private void Step(List<Object> from, List<Object> to)
        {
            while (from.Count > 0 && from[from.Count - 1] == null) from.RemoveAt(from.Count - 1);
            if (from.Count == 0) return;
            var asset = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            if (selected != null) to.Add(selected);
            tagsShown = false;
            if (!Shows(asset as ScriptableObject))
            {
                search = "";
                typeFilter = Types.FirstOrDefault(type => type.IsInstanceOfType(asset))?.Name ?? "";
            }
            selected = asset;
            Refill();
            ShowHistory();
        }

        private void ShowHistory()
        {
            backButton?.SetEnabled(CanGoBack);
            forwardButton?.SetEnabled(CanGoForward);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Acting on an asset
        // ═══════════════════════════════════════════════════════════════════════════

        // The selected asset's actions, in its right-click menu
        private void AssetMenu(ContextualMenuPopulateEvent menu) => FillAssetMenu(menu.menu);

        // The selected assets' actions, one menu with an asset header's three dots (ED-71): the first's alone, and the
        // selection's into another Forge Project (FEAT-34)
        internal void FillAssetMenu(DropdownMenu menu) =>
            ForgeAssetActions.AddTo(menu, ForgeAssetActions.For(SelectedAssets(), copy => OpenToAsset(copy)));

        // The assets the list has selected, or the one the detail shows
        private List<ScriptableObject> SelectedAssets()
        {
            if (tagsShown) return new List<ScriptableObject>();
            var chosen = assetList?.selectedItems.OfType<ScriptableObject>().ToList() ?? new List<ScriptableObject>();
            if (chosen.Count == 0 && selected is ScriptableObject one) chosen.Add(one);
            return chosen;
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
            // The name its inspector gives it, as menus list it (ED-74)
            ScalerAsset scaler => scaler.DisplayName,
            RequirementTemplate template => template.DisplayName,
            _ => asset.name,
        };

        private static string AssetTagOf(ScriptableObject asset)
        {
            if (asset is not BaseForgeLevelProvider provider) return "";
            return TagDisplaySettings.PathOf(provider.GetAssetTag());
        }

        // The levels an asset uses, as "1–5", or nothing for an asset without levels of its own
        private static string LevelsOf(ScriptableObject asset)
        {
            if (asset is not BaseForgeLevelProvider provider || asset is GameplayEffect) return "";
            int max = provider.GetMaxLevel();
            return max <= 0 ? "" : $"{provider.GetStartingLevel()}–{max}";
        }

        /// <summary>
        /// A tag, by its full path or by its leaf, as the tag preferences say (ED-25), from its display name: a unique
        /// tag's name is padded with a generated suffix (ED-60).
        /// </summary>
        internal static string TagText(Tag tag)
        {
            var name = TagDisplaySettings.PathOf(tag);
            if (TagDisplaySettings.ShowFullPath) return name;
            int dot = name.LastIndexOf('.');
            return dot >= 0 ? name.Substring(dot + 1) : name;
        }

        // What a tag is for: the contexts its uses are in, by name
        private static string ContextsOf(ForgeTagRegistry.TagUsageRecord record) => string.Join(", ", record.UsageByContext.Keys.OrderBy(key => key));

        /// <summary>
        /// The tags in a column's order: by the tag as the list shows it, by what it's for, or by its uses as a number,
        /// each compared in natural order, then by the tag (ED-59).
        /// </summary>
        internal static List<ForgeTagRegistry.TagUsageRecord> SortedTags(IEnumerable<ForgeTagRegistry.TagUsageRecord> records, string column, bool ascending)
        {
            Func<ForgeTagRegistry.TagUsageRecord, string> key = column switch
            {
                "Contexts" => ContextsOf,
                "Uses" => record => record.TotalAssetCount.ToString(),
                _ => record => TagText(record.Tag),
            };
            var natural = Comparer<string>.Create(EditorUtility.NaturalCompare);
            var ordered = ascending ? records.OrderBy(key, natural) : records.OrderByDescending(key, natural);
            return ordered.ThenBy(record => TagText(record.Tag), natural).ToList();
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
                // By each asset's one line (TW-21)
                "Summary" => ForgeAssetLines.Kept,
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

    // Tells each open Forge what an import brought, as it happens, so it doesn't wait on the project's change event
    internal sealed class TheForgeImports : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Length + deleted.Length + moved.Length > 0) TheForge.ReloadOpenWindows();
        }
    }
}
