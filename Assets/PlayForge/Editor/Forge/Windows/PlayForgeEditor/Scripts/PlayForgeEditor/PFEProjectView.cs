using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UIElements;
using GenericMenu = UnityEditor.GenericMenu;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        #region Project View
        public VisualTreeAsset projectViewRow;

        private VisualElement projectViewRoot;

        // Search
        private TextField pv_searchField;
        private bool hasQuery;

        // List view
        private ListView pv_listView;
        private List<PVRow> pv_viewRows = new();
        private VisualElement pv_selectedRow;

        // Main filter buttons
        public Button pv_dataButton;
        public Button pv_dataOptButton;
        public Button pv_categoryButton;
        public Button pv_categoryOptButton;

        // Sub filter buttons
        public Button pv_templateButton;
        public Button pv_templateOptButton;
        public Button pv_alertButton;
        public Button pv_alertOptButton;
        public Button pv_otherButton;
        public Button pv_otherOptButton;

        // Dropdowns
        public VisualElement pv_dataOptDd;
        public VisualElement pv_categoryOptDd;
        public VisualElement pv_templateOptDd;
        public VisualElement pv_alertOptDd;
        public VisualElement pv_otherOptDd;

        // Variables
        private bool pv_byKind;
        private Dictionary<EDataType, bool> pv_openTypes;  // Data types with headers open
        private Dictionary<EDataType, bool> pv_shownTypes;  // Data types to show

        private bool pv_onlyPopulatedTypes = false;

        private bool pv_byCategory;
        private Dictionary<Tag, bool> pv_openCategories = new();

        private bool pv_filterForTemplates;
        private bool pv_filterForAlerts;
        private bool pv_filterInOther;

        void SetupProjectView()
        {
            pv_openTypes = new Dictionary<EDataType, bool>();
            pv_shownTypes = new Dictionary<EDataType, bool>();
            pv_openCategories = new Dictionary<Tag, bool>();
            
            foreach (var dt in Enum.GetValues(typeof(EDataType)).Cast<EDataType>())
            {
                if (dt == EDataType.None) continue;
                pv_openTypes[dt] = true;
                pv_shownTypes[dt] = true;
            }

            foreach (string tString in Setting(ForgeTags.EDITOR_CATEGORIES, new List<string>()))
            {
                pv_openCategories[Tag.Generate(tString)] = true;
            }
        }
        
        private void BindProjectView()
        {
            projectViewRoot = rootVisualElement.Q("ContentView").Q("ProjectView");

            // List view
            pv_listView = projectViewRoot.Q("View").Q<ListView>("List");
            pv_searchField = projectViewRoot.Q<TextField>("Search");

            // Main filters
            pv_dataButton = projectViewRoot.Q("SortModes").Q("Data").Q<Button>("Btn");
            pv_dataOptButton = projectViewRoot.Q("SortModes").Q("Data").Q<Button>("Options");

            pv_categoryButton = projectViewRoot.Q("SortModes").Q("Category").Q<Button>("Btn");
            pv_categoryOptButton = projectViewRoot.Q("SortModes").Q("Category").Q<Button>("Options");

            // Sub filters
            pv_templateButton = projectViewRoot.Q("Filters").Q("Template").Q<Button>("Btn");
            pv_templateOptButton = projectViewRoot.Q("Filters").Q("Template").Q<Button>("Options");

            pv_alertButton = projectViewRoot.Q("Filters").Q("Alert").Q<Button>("Btn");
            pv_alertOptButton = projectViewRoot.Q("Filters").Q("Alert").Q<Button>("Options");

            pv_otherButton = projectViewRoot.Q("Filters").Q("Other").Q<Button>("Btn");
            pv_otherOptButton = projectViewRoot.Q("Filters").Q("Other").Q<Button>("Options");
            
            BindProgress();
        }

        private void BuildProjectView()
        {
            pv_searchField.RegisterValueChangedCallback(evt =>
            {
                hasQuery = evt.newValue.Length > 0;
                RefreshProjectViewSearchResults();
            });
            
            pv_searchField.RegisterCallback<FocusInEvent>(evt =>
            {
                pv_searchField.SetValueWithoutNotify("");
                RefreshProjectViewSearchResults();
            });
            
            pv_searchField.RegisterCallback<FocusOutEvent>(evt =>
            {
                hasQuery = pv_searchField.value.Length > 0;
                if (!hasQuery)
                {
                    pv_searchField.SetValueWithoutNotify("Search Project...");
                    RefreshProjectViewSearchResults();
                }
            });
            
            BuildListView();
            BuildFilterButtons();
            
            BuildProgress();

            return;

            void BuildFilterButtons()
            {
                // Main filters
                pv_dataButton.clicked += OnClickDataButton;
                pv_dataOptButton.clicked += OnClickDataOptButton;

                pv_byKind = true;
                pv_dataButton.style.backgroundColor = ColorSelected;

                pv_categoryButton.clicked += OnClickCategoryButton;
                pv_categoryOptButton.clicked += OnClickCategoryOptButton;

                // Sub filters
                pv_templateButton.clicked += OnClickTemplateButton;
                pv_templateOptButton.clicked += OnClickTemplateOptButton;

                pv_alertButton.clicked += OnClickAlertButton;
                pv_alertOptButton.clicked += OnClickAlertOptButton;

                pv_otherButton.clicked += OnClickOtherButton;
                pv_otherOptButton.clicked += OnClickOtherOptButton;

                void OnClickDataButton()
                {
                    pv_byKind = !pv_byKind;
                    pv_dataButton.style.backgroundColor = pv_byKind ? ColorSelected : ColorLight;

                    RefreshProjectView();
                }

                void OnClickDataOptButton()
                {
                    var menu = new GenericMenu();

                    foreach (var dt in Enum.GetValues(typeof(EDataType)).Cast<EDataType>())
                    {
                        if (dt == EDataType.None) continue;
                        menu.AddItem(new GUIContent(DataTypeText(dt)), pv_shownTypes[dt], data =>
                        {
                            pv_shownTypes[dt] = !pv_shownTypes[dt];
                            RefreshProjectView();
                        }, dt);
                    }
                    
                    menu.ShowAsContext();
                }

                void OnClickCategoryButton()
                {
                    pv_byCategory = !pv_byCategory;
                    pv_categoryButton.style.backgroundColor = pv_byCategory ? ColorSelected : ColorLight;

                    RefreshProjectView();
                }

                void OnClickCategoryOptButton()
                {

                }

                void OnClickTemplateButton()
                {
                    pv_filterForTemplates = !pv_filterForTemplates;
                    pv_templateButton.style.backgroundColor = pv_filterForTemplates ? ColorSelected : ColorLight;
                    RefreshProjectView();
                }

                void OnClickTemplateOptButton()
                {

                }

                void OnClickAlertButton()
                {
                    pv_filterForAlerts = !pv_filterForAlerts;
                    pv_alertButton.style.backgroundColor = pv_filterForAlerts ? ColorSelected : ColorLight;
                    RefreshProjectView();
                }

                void OnClickAlertOptButton()
                {

                }

                void OnClickOtherButton()
                {
                    pv_filterInOther = !pv_filterInOther;
                    pv_otherButton.style.backgroundColor = pv_filterInOther ? ColorSelected : ColorLight;
                    RefreshProjectView();
                }

                void OnClickOtherOptButton()
                {

                }
            }

            void BuildListView()
            {
                pv_listView.makeItem = () =>
                {
                    VisualElement row = projectViewRow.CloneTree();
                    return row;
                };

                pv_listView.bindItem = (row, idx) =>
                {
                    if (idx < 0 || idx >= pv_viewRows.Count) return;
                    var data = pv_viewRows[idx];
                    row.userData = data;

                    var chevron = row.Q<Button>("Chevron");
                    var _name = row.Q<Label>("Name");
                    var alert = row.Q("AlertBody");
                    var rowBtns = row.Q("Data");
                    var headerAddBtn = row.Q<Button>("HeaderAdd");

                    if (data.Type == EPVRowType.KindHeader)
                    {
                        _name.style.unityFontStyleAndWeight = FontStyle.Bold;
                        alert.style.display = DisplayStyle.None;
                        rowBtns.style.display = DisplayStyle.None;
                        _name.style.fontSize = 13;

                        headerAddBtn.clicked += () =>
                        {
                            LoadIntoCreator(data.Kind);
                        };

                        chevron.clicked += () => OnClickCollapseTypeHeader(data.Kind, chevron);
                        chevron.text = pv_openTypes[data.Kind] ? ChevronDown : ChevronRight;
                    }
                    else if (data.Type == EPVRowType.CategoryHeader)
                    {
                        _name.style.unityFontStyleAndWeight = FontStyle.Bold;
                        alert.style.display = DisplayStyle.None;
                        rowBtns.style.display = DisplayStyle.None;
                        headerAddBtn.style.display = DisplayStyle.None;
                        _name.style.fontSize = 11;

                        bool isOpen = !pv_openCategories.TryGetValue(data.CatTag, out var open) || open;
                        chevron.text = isOpen ? ChevronDown : ChevronRight;

                        chevron.clicked += () => OnClickCollapseCatHeader(data, chevron);
                    }
                    else if (data.Type == EPVRowType.Item)
                    {
                        alert.style.display = DisplayStyle.None;
                        rowBtns.style.display = DisplayStyle.None;
                        headerAddBtn.style.display = DisplayStyle.None;
                        chevron.style.display = DisplayStyle.None;
                        alert.style.display = DisplayStyle.None;

                        row.RegisterCallback<MouseDownEvent>(_ =>
                        {
                            if (Focus.IsFocused && Focus.Node.Id == data.Node.Id)
                            {
                                SetFocus(Focus, NoneFocus, EForgeContext.Home);
                                row.style.backgroundColor = default;
                            }
                            else
                            {
                                SetFocus(Focus, data, EForgeContext.Home);
                                if (pv_selectedRow is not null) pv_selectedRow.style.backgroundColor = default;
                                pv_selectedRow = row;
                                pv_selectedRow.style.backgroundColor = ColorSelected;
                            }
                            
                            RefreshHeader();
                            
                        });

                        row.Q<Button>("Edit").clicked += () =>
                        {
                            if (pv_selectedRow is not null) pv_selectedRow.style.backgroundColor = default;
                            pv_selectedRow = row;
                            pv_selectedRow.style.backgroundColor = ColorSelected;
                            
                            LoadIntoCreator(data.Node, data.Kind);
                        };

                        row.RegisterCallback<PointerEnterEvent>(evt =>
                        {
                            if (!data.Node.TagStatus(ForgeTags.VALID_FOR_GAMEPLAY)) alert.style.display = DisplayStyle.Flex;
                            rowBtns.style.display = DisplayStyle.Flex;
                            if (pv_selectedRow != row) row.style.backgroundColor = Color.black * .5f;
                        });

                        row.RegisterCallback<PointerLeaveEvent>(evt =>
                        {
                            alert.style.display = DisplayStyle.None;
                            rowBtns.style.display = DisplayStyle.None;
                            if (pv_selectedRow != row) row.style.backgroundColor = Color.clear;
                        });
                    }
                    else
                    {
                        headerAddBtn.style.display = DisplayStyle.None;
                        chevron.style.display = DisplayStyle.None;
                        alert.style.display = DisplayStyle.None;
                        rowBtns.style.display = DisplayStyle.None;
                    }

                    row.style.paddingLeft = data.Inset * InsetFactor;
                    _name.text = data.Node?.Name ?? data.Key;
                };

                pv_listView.selectionChanged += objs =>
                {
                    var row = objs?.FirstOrDefault() as PVRow;
                    pv_listView.ClearSelection();
                };

                return;

                void OnClickCollapseTypeHeader(EDataType kind, Button b)
                {
                    Debug.Log($"collapse clicked {kind}");
                    
                    pv_openTypes[kind] = !pv_openTypes[kind];
                    
                    RefreshProjectView();
                }

                void OnClickCollapseCatHeader(PVRow row, Button b)
                {
                    bool isOpen = !pv_openCategories.TryGetValue(row.CatTag, out var open) || open;
                    pv_openCategories[row.CatTag] = !isOpen;

                    // Update icon immediately
                    b.text = pv_openCategories[row.CatTag] ? ChevronDown : ChevronRight;

                    // Rebuild so items hide/show under this header
                    RefreshProjectView();
                }
            }
        }

        private void RefreshProjectView(bool loadProjectData = false)
        {
            if (loadProjectData) LoadProjectData();
            RefreshProjectViewSearchResults();
        }

        void RefreshProjectViewSearchResults()
        {
            pv_viewRows = GetProjectViewItemsList();
            pv_listView.itemsSource = pv_viewRows;
            pv_listView.Rebuild();
        }

        #region Helpers

        private string ChevronRight => "\u25b6";
        private string ChevronDown => "\u25bc";
        
        private const string UncategorizedLabel = "Uncategorized";
        private const int InsetFactor = 27;

        private enum EPVRowType
        {
            Item,
            KindHeader,
            CategoryHeader,
            Placeholder
        }

        private class PVRow : DataContainer
        {
            public readonly int Inset;
            public readonly string Key;
            public readonly EPVRowType Type;
            public Tag CatTag;

            public PVRow(ForgeDataNode node, EDataType kind, EPVRowType type, Tag catTag, int inset, string key = "") : base(node, kind)
            {
                Type = type;
                Inset = inset;
                Key = key;
            }

            public override string ToString()
            {
                return $"{Node?.Name ?? "NULL"} {Kind.ToString()} {Type}";
            }
        }

        #region Project Data Sorting
        private enum EGroupNodeType
        {
            KindHeader,
            CategoryHeader,
            All
        }

        private sealed class GroupNode
        {
            public readonly string Key;
            public readonly EGroupNodeType Type;
            public EDataType Kind;
            public Tag CatTag;

            public GroupNode(string key, EGroupNodeType type)
            {
                Key = key;
                Type = type;
            }

            public List<GroupNode> Children { get; } = new();
            public List<DataContainer> Items { get; set; } = new();
        }

        private List<PVRow> GetProjectViewItemsList()
        {
            if (Project.DataCount == 0)
            {
                return new List<PVRow>
                {
                    new(null, EDataType.None, EPVRowType.Placeholder, Tags.NULL, 0, "This Framework does not contain any data yet...")
                };
            }

            var Ci = StringComparer.OrdinalIgnoreCase;

            Func<DataContainer, string> nameSelector = it => it.Node.Name;
            Func<Tag, string> tagSelector = t => t.Name ?? string.Empty;

            var groups = GroupAndSort();
            var rows = new List<PVRow>();
            
            // foreach (var g in groups) RecFilterGroups(g);
            foreach (var g in groups)
            {
                RecCompileRows(g, 0);
            }

            if (rows.Count == 0) rows.Add(new PVRow(null, EDataType.None, EPVRowType.Placeholder, Tags.NULL, 5, "No matching items..."));

            return rows;
            
            void RecCompileRows(GroupNode g, int depth)
            {
                switch (g.Type)
                {
                    // passthrough for "All"
                    case EGroupNodeType.All:
                        rows.AddRange(g.Items.Select(dc => new PVRow(dc.Node, dc.Kind, EPVRowType.Item, Tags.NULL, 0)));
                        return;
                    case EGroupNodeType.KindHeader:
                        rows.Add(new PVRow(null, g.Kind, EPVRowType.KindHeader, g.CatTag, depth, g.Key));
                        break;
                    case EGroupNodeType.CategoryHeader:
                    {
                        var catRow = new PVRow(null, EDataType.None, EPVRowType.CategoryHeader, g.CatTag, depth, g.Key);
                        catRow.CatTag = g.CatTag; // stable identity for collapse
                        rows.Add(catRow);
                        break;
                    }
                }

                // ---- NEW: collapse gating ----
                bool allowKind = true;
                if (g.Type == EGroupNodeType.KindHeader)
                {
                    allowKind = !pv_openTypes.TryGetValue(g.Kind, out var isOpen) || isOpen;
                }
                // ------------------------------

                // recurse children
                if (allowKind)
                {
                    foreach (var _g in g.Children) RecCompileRows(_g, depth + 1);
                }

                // append items; skip if this is a collapsed category or collapsed kind
                bool allowItems = allowKind;
                if (g.Type == EGroupNodeType.CategoryHeader)
                {
                    allowItems = !pv_openCategories.TryGetValue(g.CatTag, out var isOpen) || isOpen;
                }

                if (allowItems)
                {
                    foreach (var n in g.Items)
                        rows.Add(new PVRow(n.Node, n.Kind, EPVRowType.Item, g.CatTag, depth + 1));
                }
            }

            List<GroupNode> GroupAndSort(IComparer<EDataType> kindComparer = null)
            {
                if (projectData is null) return new List<GroupNode>();
                kindComparer ??= Comparer<EDataType>.Default;

                var filtered = FlattenFiltered().ToList();
                var queried = FlattenQueried(filtered).ToList();

                if (!pv_byKind && !pv_byCategory)
                {
                    var all = new GroupNode("All", EGroupNodeType.All);
                    all.Items.AddRange(queried.OrderBy(dc => nameSelector(dc), Ci));
                    return new List<GroupNode> { all };
                }

                if (pv_byKind && pv_byCategory)
                {
                    return GroupByKindThenCategory(queried);
                }

                if (pv_byKind) return GroupByKind(queried);

                // Cat only
                return GroupByCategory(queried);

                IEnumerable<DataContainer> FlattenFiltered()
                {
                    foreach (var (k, list) in projectData)
                    {
                        if (list is null) continue;
                        foreach (var node in list)
                        {
                            if (!pv_filterForTemplates && node.TagStatus(ForgeTags.IS_TEMPLATE)) continue;
                            if (pv_filterForTemplates && !node.TagStatus(ForgeTags.IS_TEMPLATE)) continue;
                            if (pv_filterForAlerts && node.TagStatus(ForgeTags.VALID_FOR_GAMEPLAY)) continue;

                            var container = new DataContainer(node, k);
                            yield return container;
                        }
                    }
                }

                IEnumerable<DataContainer> FlattenQueried(IEnumerable<DataContainer> src)
                {
                    string q = pv_searchField.value.Trim().ToLowerInvariant();
                    
                    foreach (var dc in src)
                    {
                        if (dc.Node is null || dc.Kind == EDataType.None) continue;
                        if (!hasQuery) yield return dc;
                        else
                        {
                            if (
                                !string.IsNullOrEmpty(dc.Node.Name) && dc.Node.Name.ToLowerInvariant().Contains(q) 
                                || dc.Node.Id > 0 && dc.Node.Id.ToString().Contains(q))
                            {
                                yield return dc;
                            }
                        }
                    }
                }

                List<GroupNode> GroupByKind(
                    IEnumerable<DataContainer> items
                )
                {
                    var byKind = items
                        .GroupBy(dc => dc.Kind)
                        .ToDictionary(g => g.Key, g => g.OrderBy(dc => nameSelector(dc), Ci).ToList());

                    var kindsOrdered = Enum.GetValues(typeof(EDataType)).Cast<EDataType>()
                        .Where(k => k != EDataType.None)
                        .OrderBy(k => k, kindComparer);

                    var result = new List<GroupNode>();

                    foreach (var kind in kindsOrdered)
                    {
                        if (!pv_shownTypes[kind]) continue;
                        var list = byKind.TryGetValue(kind, out var exists) ? exists : null;
                        
                        var _title = list is null ? $" (0)" : $" ({list.Count})";
                        var node = new GroupNode($"{DataTypeTextPlural(kind)}{_title}", EGroupNodeType.KindHeader)
                        {
                            Kind = kind
                        };
                        if (pv_openTypes[kind] && list is not null) node.Items.AddRange(list);
                        result.Add(node);
                    }

                    return result;
                }

                List<GroupNode> GroupByCategory(IEnumerable<DataContainer> items)
                {
                    var result = new List<GroupNode>();

                    // Group items by category tag
                    var grouped = items
                        .Select(dc => (dc, cats: dc.Node.TagStatus(ForgeTags.EDITOR_CATEGORIES, new List<Tag>())))
                        .SelectMany(x => (x.cats.Count > 0 ? x.cats : new List<Tag> { Tag.Generate(UncategorizedLabel) })
                            .Select(tag => (tag, x.dc)))
                        .GroupBy(x => x.tag, EqualityComparer<Tag>.Default)
                        .OrderBy(g => tagSelector(g.Key) ?? string.Empty, Ci)
                        .ToList();

                    foreach (var g in grouped)
                    {
                        var itemsList = g.Select(x => x.dc)
                            .Distinct()
                            .OrderBy(dc => nameSelector(dc), Ci)
                            .ToList();

                        var catName = tagSelector(g.Key) ?? UncategorizedLabel;
                        var count = itemsList.Count;
                        var _title = $"{catName} ({count})";

                        var node = new GroupNode(_title, EGroupNodeType.CategoryHeader)
                        {
                            Kind = EDataType.None,
                            CatTag = g.Key
                        };

                        node.Items.AddRange(itemsList);
                        result.Add(node);
                    }

                    return result;
                }

                List<GroupNode> GroupByKindThenCategory(IEnumerable<DataContainer> items)
                {
                    var byKind = items
                        .GroupBy(dc => dc.Kind)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    var kindsOrdered = Enum.GetValues(typeof(EDataType))
                        .Cast<EDataType>()
                        .Where(k => k != EDataType.None)
                        .OrderBy(k => k, kindComparer);

                    var result = new List<GroupNode>();

                    foreach (var kind in kindsOrdered)
                    {
                        if (!pv_shownTypes.TryGetValue(kind, out var show) || !show)
                            continue;

                        var list = byKind.TryGetValue(kind, out var exists) ? exists : null;

                        if (pv_onlyPopulatedTypes && (list == null || list.Count == 0))
                            continue;

                        var kindCount = list?.Count ?? 0;
                        var kindTitle = $"{DataTypeTextPlural(kind)} ({kindCount})";

                        var kindNode = new GroupNode(kindTitle, EGroupNodeType.KindHeader)
                        {
                            Kind = kind
                        };

                        if (list != null && list.Count > 0)
                        {
                            // Items WITH categories
                            var withCats = list
                                .Select(dc => (dc, cats: dc.Node.TagStatus(ForgeTags.EDITOR_CATEGORIES, new List<Tag>())))
                                .Where(x => x.cats.Count > 0)
                                .SelectMany(x => x.cats.Select(tag => (tag, x.dc)))
                                .GroupBy(x => x.tag, EqualityComparer<Tag>.Default)
                                .OrderBy(g => tagSelector(g.Key) ?? string.Empty, Ci)
                                .Select(g =>
                                {
                                    var catItems = g.Select(x => x.dc)
                                        .Distinct()
                                        .OrderBy(dc => nameSelector(dc), Ci)
                                        .ToList();

                                    var catName = tagSelector(g.Key) ?? string.Empty;
                                    var catCount = catItems.Count;
                                    var catTitle = $"{catName} ({catCount})";

                                    var node = new GroupNode(catTitle, EGroupNodeType.CategoryHeader)
                                    {
                                        Kind = kind,
                                        CatTag = g.Key
                                    };

                                    node.Items.AddRange(catItems);
                                    return node;
                                })
                                .ToList();

                            // Items with NO categories
                            var noCats = list
                                .Where(dc => dc.Node.TagStatus(ForgeTags.EDITOR_CATEGORIES, new List<Tag>()).Count == 0)
                                .Distinct()
                                .OrderBy(dc => nameSelector(dc), Ci)
                                .ToList();

                            if (noCats.Count > 0)
                            {
                                var uncatTitle = $"{UncategorizedLabel} ({noCats.Count})";
                                var uncat = new GroupNode(uncatTitle, EGroupNodeType.CategoryHeader)
                                {
                                    Kind = kind,
                                    CatTag = Tag.Generate(UncategorizedLabel)
                                };

                                uncat.Items.AddRange(noCats);
                                withCats.Add(uncat);
                            }

                            kindNode.Children.AddRange(withCats);
                        }

                        result.Add(kindNode);
                    }

                    return result;
                }
            }
        }
        #endregion
        #endregion
        #endregion
    }
}
