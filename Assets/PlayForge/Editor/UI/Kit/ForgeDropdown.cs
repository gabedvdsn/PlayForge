using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>One choice of a <see cref="ForgeDropdown"/>: what it shows, where, and what picking it does.</summary>
    internal sealed class ForgeDropdownItem
    {
        public ForgeDropdownItem(string path, string name, Action picked)
        {
            Path = ForgeDropdown.CleanPath(path);
            Name = string.IsNullOrEmpty(name) ? "—" : name;
            Picked = picked;
        }

        /// <summary>The choice as its row shows it.</summary>
        public string Name { get; }

        /// <summary>The submenus it sits under, a slash between each, as "Effects/Damage"; "" for the top.</summary>
        public string Path { get; }

        /// <summary>What picking it does.</summary>
        public Action Picked { get; }

        /// <summary>What the dropdown's Context section says while it's highlighted, or null for nothing.</summary>
        public string Context { get; set; }

        /// <summary>More text its search matches, as a tag's whole path; null for its name and path alone.</summary>
        public string SearchText { get; set; }

        /// <summary>Whether it's checked, as the field's value is.</summary>
        public bool Checked { get; set; }

        /// <summary>Whether it can be picked. One that can't still highlights, so its Context can say why.</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>A node of a dropdown's tree: a submenu, a choice, or a line between choices.</summary>
    internal sealed class ForgeDropdownNode
    {
        public ForgeDropdownNode(string name, ForgeDropdownItem item, bool separator, ForgeDropdownNode parent)
        {
            Name = name ?? "";
            Item = item;
            IsSeparator = separator;
            Parent = parent;
        }

        /// <summary>The submenu's or the choice's name; empty for a line.</summary>
        public string Name { get; }

        /// <summary>The choice, or null for a submenu or a line.</summary>
        public ForgeDropdownItem Item { get; }

        public bool IsSeparator { get; }

        /// <summary>Whether it's a submenu, which opens on its own list.</summary>
        public bool IsMenu => Item == null && !IsSeparator;

        /// <summary>The submenu it's in, or null at the top.</summary>
        public ForgeDropdownNode Parent { get; internal set; }

        /// <summary>A submenu's submenus, choices and lines, in its order.</summary>
        public List<ForgeDropdownNode> Children { get; } = new();

        /// <summary>The choices under it, at any depth, in order.</summary>
        public IEnumerable<ForgeDropdownNode> Choices()
        {
            foreach (var child in Children)
            {
                if (child.Item != null) yield return child;
                else if (child.IsMenu) foreach (var deeper in child.Choices()) yield return deeper;
            }
        }

        /// <summary>The submenu by its name, among this one's, or null.</summary>
        public ForgeDropdownNode Menu(string name) => Children.FirstOrDefault(child => child.IsMenu && child.Name == name);

        /// <summary>The names of the menus down to it from the top, a slash between each, as "Effects/Damage".</summary>
        public string MenuPath
        {
            get
            {
                var names = new List<string>();
                for (var each = IsMenu ? this : Parent; each?.Parent != null; each = each.Parent) names.Insert(0, each.Name);
                return string.Join("/", names);
            }
        }
    }

    /// <summary>
    /// PlayForge's dropdown, every picker's: a title, a search field, its choices in the submenus their paths name,
    /// and, where any choice says more of itself, a Context section at its foot, which says it of the choice the pointer or
    /// the keys are on, as a tag's contexts or a kind's description. It's no wider than Preferences → PlayForge's Menu
    /// Width nor taller than its Menu Height, and a longer list scrolls. Each picker was Unity's searchable dropdown, which
    /// has no room beside a choice's name and grows as its longest list does; and a field's menu was Unity's own, the
    /// operating system's, which runs the screen's height.
    /// </summary>
    internal sealed class ForgeDropdown
    {
        private readonly List<ForgeDropdownItem> fixedItems = new();
        private readonly List<(ForgeDropdownItem Item, string Separator)> entries = new();

        public ForgeDropdown(string title)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Choose" : title.Trim();
        }

        /// <summary>The dropdown's heading, at its top while no submenu is open.</summary>
        public string Title { get; }

        /// <summary>What it says where it offers nothing.</summary>
        public string Empty { get; set; } = "Nothing to choose";

        /// <summary>
        /// Whether each menu lists its submenus first, by name, a line, then its choices in the order given, as a kind's
        /// picker does; else everything in the order given, each submenu where its first choice was, as a tag's does.
        /// </summary>
        public bool MenusFirst { get; set; } = true;

        /// <summary>The dropdown a field opened last, for a test to read what it offered.</summary>
        internal static ForgeDropdown LastShown { get; private set; }

        /// <summary>A choice at the very top, above a line, before the rest whatever their order: None, or New Tag….</summary>
        public ForgeDropdownItem AddFixed(string name, Action picked, string context = null)
        {
            var item = new ForgeDropdownItem("", name, picked) { Context = context };
            fixedItems.Add(item);
            return item;
        }

        /// <summary>A choice, under the submenus its path names, a slash between each; "" for the top.</summary>
        public ForgeDropdownItem Add(string path, string name, Action picked, string context = null, bool isChecked = false, bool enabled = true)
        {
            var item = new ForgeDropdownItem(path, name, picked) { Context = context, Checked = isChecked, Enabled = enabled };
            entries.Add((item, null));
            return item;
        }

        /// <summary>A line between choices, in the submenu its path names; "" for the top.</summary>
        public void AddSeparator(string path = "") => entries.Add((null, CleanPath(path)));

        /// <summary>Every choice, the fixed ones first, in the order given.</summary>
        public IEnumerable<ForgeDropdownItem> Items => fixedItems.Concat(entries.Where(entry => entry.Item != null).Select(entry => entry.Item));

        /// <summary>Whether any choice says more of itself, so the dropdown has a Context section.</summary>
        public bool HasContext => Items.Any(item => !string.IsNullOrWhiteSpace(item.Context));

        /// <summary>The dropdown's tree: its top, each submenu under the menu it's in, each choice and line under its menu.</summary>
        public ForgeDropdownNode Build()
        {
            var root = new ForgeDropdownNode(Title, null, false, null);
            foreach (var item in fixedItems) root.Children.Add(new ForgeDropdownNode(item.Name, item, false, root));

            var body = new ForgeDropdownNode(Title, null, false, null);
            foreach (var (item, separator) in entries)
            {
                var menu = MenuAt(body, item?.Path ?? separator);
                menu.Children.Add(item == null ? new ForgeDropdownNode("", null, true, menu) : new ForgeDropdownNode(item.Name, item, false, menu));
            }
            if (MenusFirst) MenusFirstIn(body);

            if (fixedItems.Count > 0 && body.Children.Any(child => !child.IsSeparator)) root.Children.Add(new ForgeDropdownNode("", null, true, root));
            foreach (var child in body.Children)
            {
                child.Parent = root;
                root.Children.Add(child);
            }
            Tidy(root);
            if (!root.Choices().Any())
                root.Children.Add(new ForgeDropdownNode(Empty, new ForgeDropdownItem("", Empty, null) { Enabled = false }, false, root));
            return root;
        }

        /// <summary>Shows the dropdown under an element, as a field's box. In a runtime panel it's UI Toolkit's dropdown.</summary>
        public void Show(VisualElement anchor)
        {
            if (anchor?.panel == null) return;
            LastShown = this;
            if (anchor.panel.contextType == ContextType.Editor) ForgeDropdownWindow.Open(this, GUIUtility.GUIToScreenRect(anchor.worldBound));
            else ShowInPanel(anchor);
        }

        /// <summary>Shows the dropdown under a rect of an editor panel, as Unity's own dropdown takes one.</summary>
        public void Show(Rect worldBound)
        {
            LastShown = this;
            ForgeDropdownWindow.Open(this, GUIUtility.GUIToScreenRect(worldBound));
        }

        // A runtime panel has no editor window to open, so UI Toolkit's dropdown shows the choices, each by its path
        private void ShowInPanel(VisualElement anchor)
        {
            var menu = new GenericDropdownMenu();
            foreach (var node in Build().Children) AddTo(menu, node, "");
            menu.DropDown(anchor.worldBound, anchor, true, true);
        }

        private static void AddTo(GenericDropdownMenu menu, ForgeDropdownNode node, string path)
        {
            if (node.IsSeparator)
            {
                menu.AddSeparator(path);
                return;
            }
            var named = string.IsNullOrEmpty(path) ? node.Name : $"{path}/{node.Name}";
            if (node.IsMenu)
            {
                foreach (var child in node.Children) AddTo(menu, child, named);
                return;
            }
            var item = node.Item;
            if (item.Enabled && item.Picked != null) menu.AddItem(named, item.Checked, item.Picked);
            else menu.AddDisabledItem(named, item.Checked);
        }

        // The submenu a path names, made as it's first named, at the place of the choice that names it
        private static ForgeDropdownNode MenuAt(ForgeDropdownNode top, string path)
        {
            var menu = top;
            if (string.IsNullOrEmpty(path)) return menu;
            foreach (var name in path.Split('/'))
            {
                var found = menu.Menu(name);
                if (found == null)
                {
                    found = new ForgeDropdownNode(name, null, false, menu);
                    menu.Children.Add(found);
                }
                menu = found;
            }
            return menu;
        }

        // Each menu's submenus first, by name, then a line, then its choices in their order
        private static void MenusFirstIn(ForgeDropdownNode menu)
        {
            var natural = Comparer<string>.Create(EditorUtility.NaturalCompare);
            var menus = menu.Children.Where(child => child.IsMenu).OrderBy(child => child.Name, natural).ToList();
            var rest = menu.Children.Where(child => !child.IsMenu).ToList();
            menu.Children.Clear();
            menu.Children.AddRange(menus);
            if (menus.Count > 0 && rest.Any(child => !child.IsSeparator)) menu.Children.Add(new ForgeDropdownNode("", null, true, menu));
            menu.Children.AddRange(rest);
            foreach (var each in menus) MenusFirstIn(each);
        }

        // No line at a menu's ends, nor two together; an empty submenu goes
        private static void Tidy(ForgeDropdownNode menu)
        {
            foreach (var each in menu.Children.Where(child => child.IsMenu).ToList()) Tidy(each);
            menu.Children.RemoveAll(child => child.IsMenu && child.Children.Count == 0);
            for (int i = menu.Children.Count - 1; i >= 0; i--)
            {
                if (!menu.Children[i].IsSeparator) continue;
                bool atEnd = i == 0 || i == menu.Children.Count - 1;
                bool doubled = i > 0 && menu.Children[i - 1].IsSeparator;
                if (atEnd || doubled) menu.Children.RemoveAt(i);
            }
        }

        /// <summary>A path's parts, each trimmed, empty ones left out, as " Effects / Damage/" reads "Effects/Damage".</summary>
        internal static string CleanPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            return string.Join("/", path.Split('/').Select(part => part.Trim()).Where(part => part.Length > 0));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Its size
        // ═══════════════════════════════════════════════════════════════════════════

        internal const float RowHeight = 20f;
        internal const float SeparatorHeight = 7f;
        internal const float HeaderHeight = 24f;
        internal const float SearchHeight = 24f;
        internal const float ContextHeight = 64f;
        internal const float MinWidth = 220f;
        internal const float MinHeight = 120f;

        /// <summary>
        /// The window's size: as wide as its widest choice, or the field it opened from, and as tall as its longest list,
        /// each kept within Preferences → PlayForge's Menu Width and Menu Height.
        /// </summary>
        internal Vector2 SizeFor(float anchorWidth)
        {
            var root = Build();
            float widest = 0f;
            float tallest = 0f;
            void Measure(ForgeDropdownNode menu)
            {
                float height = 0f;
                foreach (var child in menu.Children)
                {
                    height += child.IsSeparator ? SeparatorHeight : RowHeight;
                    if (!child.IsSeparator) widest = Mathf.Max(widest, TextWidth(child.Name) + (child.IsMenu ? 16f : 0f));
                    if (child.IsMenu) Measure(child);
                }
                tallest = Mathf.Max(tallest, height);
            }
            Measure(root);
            widest = Mathf.Max(widest, TextWidth(Title) + 48f);

            // A check's column before each name, and its padding
            float width = Mathf.Max(Mathf.Max(anchorWidth, MinWidth), widest + 44f);
            width = Mathf.Min(width, Mathf.Max(MinWidth, PlayForgePreferences.MenuWidth.Value));
            float height = HeaderHeight + SearchHeight + tallest + 6f + (HasContext ? ContextHeight : 0f);
            height = Mathf.Clamp(height, MinHeight, Mathf.Max(MinHeight, PlayForgePreferences.MenuHeight.Value));
            return new Vector2(Mathf.Round(width), Mathf.Round(height));
        }

        // A text's width as a label draws it; a guess where no editor style is there to measure, as in batch mode
        private static float TextWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            try
            {
                var style = EditorStyles.label;
                if (style != null) return style.CalcSize(new GUIContent(text)).x;
            }
            catch (Exception)
            {
                // No skin to measure with
            }
            return text.Length * 7f;
        }
    }

    /// <summary>
    /// The dropdown's content, which its window holds and a test can build in any panel: a header with the open
    /// menu's name and Back, the search field, the list, and the Context section. The keys move the highlight, Enter or
    /// Right opens a submenu or picks a choice, Left goes back, and Escape closes; typing searches every menu at once.
    /// </summary>
    internal sealed class ForgeDropdownView : VisualElement
    {
        public const string RootClass = "pf-dropdown";

        private readonly ForgeDropdown dropdown;
        private readonly ForgeDropdownNode root;
        private ForgeDropdownNode menu;
        private readonly List<ForgeDropdownNode> rows = new();
        private readonly List<VisualElement> rowElements = new();
        private int highlighted = -1;
        private string search = "";

        private readonly Button back;
        private readonly Label title;
        private readonly ToolbarSearchField searchField;
        private readonly ScrollView list;
        private readonly VisualElement context;
        private readonly Label contextText;

        /// <summary>Raised as it's done, a choice picked or Escape pressed, before a pick runs: its window closes.</summary>
        public event Action Done;

        public ForgeDropdownView(ForgeDropdown dropdown)
        {
            this.dropdown = dropdown;
            root = dropdown.Build();
            menu = root;
            name = "ForgeDropdown";
            AddToClassList(RootClass);

            var header = new VisualElement { name = "Header" };
            header.AddToClassList("pf-dropdown__header");
            back = new Button(Back) { name = "Back", tooltip = "Back to the menu this one is in" };
            back.AddToClassList("pf-dropdown__back");
            back.Add(new ForgeGlyph(ForgeGlyphShape.Back));
            header.Add(back);
            title = new Label { name = "Title" };
            title.AddToClassList("pf-dropdown__title");
            header.Add(title);
            Add(header);

            searchField = new ToolbarSearchField { name = "Search" };
            searchField.AddToClassList("pf-dropdown__search");
            searchField.RegisterValueChangedCallback(change => Search(change.newValue));
            Add(searchField);

            list = new ScrollView(ScrollViewMode.Vertical) { name = "Choices" };
            list.AddToClassList("pf-dropdown__list");
            Add(list);

            context = new VisualElement { name = "Context" };
            context.AddToClassList("pf-dropdown__context");
            var heading = new Label("Context");
            heading.AddToClassList("pf-dropdown__context-title");
            context.Add(heading);
            contextText = new Label { name = "ContextText" };
            contextText.AddToClassList("pf-dropdown__context-text");
            context.Add(contextText);
            context.EnableInClassList("pf-hidden", !dropdown.HasContext);
            Add(context);

            RegisterCallback<KeyDownEvent>(Keys, TrickleDown.TrickleDown);
            RegisterCallback<NavigationMoveEvent>(move => move.StopImmediatePropagation(), TrickleDown.TrickleDown);
            Fill();
        }

        // ─── What a test reads, and what the keys and the pointer do ───

        /// <summary>The rows shown, a line as "—", in order.</summary>
        internal IReadOnlyList<string> Rows => rows.Select(row => row.IsSeparator ? "—" : row.Name).ToList();

        /// <summary>The open menu's heading: the dropdown's title at its top, a submenu's name in one, "Search" searching.</summary>
        internal string Heading => title.text;

        /// <summary>The highlighted row's name, or null.</summary>
        internal string Highlighted => highlighted >= 0 && highlighted < rows.Count ? rows[highlighted].Name : null;

        /// <summary>What the Context section says.</summary>
        internal string ContextText => contextText.text;

        /// <summary>Whether it has a Context section.</summary>
        internal bool ShowsContext => !context.ClassListContains("pf-hidden");

        /// <summary>Highlights the row of a name, the first, and says its context.</summary>
        internal bool Highlight(string rowName)
        {
            int at = rows.FindIndex(row => !row.IsSeparator && row.Name == rowName);
            if (at < 0) return false;
            HighlightAt(at, false);
            return true;
        }

        /// <summary>Opens the submenu of a name in the open menu.</summary>
        internal bool Open(string menuName)
        {
            var found = rows.FirstOrDefault(row => row.IsMenu && row.Name == menuName);
            if (found == null) return false;
            OpenMenu(found);
            return true;
        }

        /// <summary>Goes back to the menu the open one is in.</summary>
        internal void Back()
        {
            if (search.Length > 0 || menu.Parent == null) return;
            var from = menu;
            menu = menu.Parent;
            Fill();
            HighlightAt(rows.IndexOf(from), true);
        }

        /// <summary>Shows the choices whose name, path or search text holds each word typed, every menu's; none for the menu.</summary>
        internal void Search(string text)
        {
            search = (text ?? "").Trim();
            if (searchField.value != (text ?? "")) searchField.SetValueWithoutNotify(text ?? "");
            Fill();
        }

        /// <summary>Picks the highlighted choice, or opens the highlighted submenu.</summary>
        internal void Submit()
        {
            if (highlighted < 0 || highlighted >= rows.Count) return;
            Activate(rows[highlighted]);
        }

        /// <summary>Gives the search field the keys, as the window opens.</summary>
        internal void FocusSearch() => searchField.Q<TextField>()?.Focus();

        // ─── Filling ───

        private void Fill()
        {
            rows.Clear();
            rowElements.Clear();
            list.Clear();
            if (search.Length > 0)
            {
                rows.AddRange(Found(search));
                title.text = "Search";
            }
            else
            {
                rows.AddRange(menu.Children);
                title.text = menu == root ? dropdown.Title : menu.Name;
            }
            back.EnableInClassList("pf-hidden", search.Length > 0 || menu.Parent == null);

            for (int i = 0; i < rows.Count; i++) list.Add(RowOf(rows[i], i));
            if (rows.Count == 0)
            {
                var none = new Label(search.Length > 0 ? "Nothing matches" : dropdown.Empty) { name = "NoChoices" };
                none.AddToClassList("pf-dropdown__none");
                list.Add(none);
            }

            // The checked choice, or else the first that can be picked
            int start = rows.FindIndex(row => row.Item is { Checked: true });
            if (start < 0) start = rows.FindIndex(row => !row.IsSeparator && (row.IsMenu || row.Item.Enabled));
            HighlightAt(start, true);
        }

        // The choices every menu holds whose text holds each word of the search, those whose name starts with it first
        private IEnumerable<ForgeDropdownNode> Found(string text)
        {
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var matched = root.Choices().Where(node =>
            {
                var haystack = $"{node.Name} {node.Parent?.MenuPath} {node.Item.SearchText}";
                return words.All(word => haystack.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
            });
            return matched.OrderBy(node => node.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase) ? 0 : 1);
        }

        private VisualElement RowOf(ForgeDropdownNode node, int index)
        {
            if (node.IsSeparator)
            {
                var line = new VisualElement();
                line.AddToClassList("pf-dropdown__separator");
                rowElements.Add(line);
                return line;
            }

            var row = new VisualElement { name = node.Name };
            row.AddToClassList("pf-dropdown__row");
            row.EnableInClassList("pf-dropdown__row--menu", node.IsMenu);
            row.EnableInClassList("pf-dropdown__row--disabled", node.Item is { Enabled: false });

            var check = new ForgeGlyph(node.Item is { Checked: true } ? ForgeGlyphShape.Check : ForgeGlyphShape.None);
            check.AddToClassList("pf-dropdown__check");
            row.Add(check);
            var label = new Label(node.Name);
            label.AddToClassList("pf-dropdown__name");
            row.Add(label);
            // A search's result says the menu it's in
            if (search.Length > 0 && node.Parent?.MenuPath is { Length: > 0 } path)
            {
                var where = new Label(path);
                where.AddToClassList("pf-dropdown__path");
                row.Add(where);
            }
            if (node.IsMenu)
            {
                var arrow = new ForgeGlyph(ForgeGlyphShape.ArrowRight);
                arrow.AddToClassList("pf-dropdown__arrow");
                row.Add(arrow);
            }

            row.RegisterCallback<PointerEnterEvent>(_ => HighlightAt(index, false));
            row.RegisterCallback<PointerDownEvent>(down =>
            {
                if (down.button != 0) return;
                Activate(node);
                down.StopPropagation();
            });
            rowElements.Add(row);
            return row;
        }

        // ─── Highlighting and picking ───

        // Highlights a row and says its context; the keys scroll it into view, once the list is laid out
        private void HighlightAt(int index, bool scroll)
        {
            if (highlighted >= 0 && highlighted < rowElements.Count) rowElements[highlighted].RemoveFromClassList("pf-dropdown__row--highlighted");
            highlighted = index >= 0 && index < rows.Count && !rows[index].IsSeparator ? index : -1;
            if (highlighted >= 0)
            {
                var row = rowElements[highlighted];
                row.AddToClassList("pf-dropdown__row--highlighted");
                if (scroll) list.schedule.Execute(() =>
                {
                    if (list.panel != null && list.contentContainer.Contains(row) && !float.IsNaN(row.layout.height)) list.ScrollTo(row);
                });
            }
            contextText.text = highlighted < 0 ? "" : ContextOf(rows[highlighted]);
        }

        // A choice's context; a submenu's, how many choices it holds
        private static string ContextOf(ForgeDropdownNode node)
        {
            if (node.Item != null) return node.Item.Context ?? "";
            int count = node.Choices().Count();
            return count == 1 ? "1 choice" : $"{count} choices";
        }

        private void Move(int step)
        {
            if (rows.Count == 0) return;
            int at = highlighted;
            for (int tries = 0; tries < rows.Count; tries++)
            {
                at = at < 0 ? (step > 0 ? 0 : rows.Count - 1) : (at + step + rows.Count) % rows.Count;
                if (!rows[at].IsSeparator) break;
            }
            HighlightAt(at, true);
        }

        private void OpenMenu(ForgeDropdownNode submenu)
        {
            if (!submenu.IsMenu) return;
            menu = submenu;
            if (search.Length > 0) Search("");
            else Fill();
        }

        private void Activate(ForgeDropdownNode node)
        {
            if (node.IsMenu) OpenMenu(node);
            else if (node.Item is { Enabled: true } item)
            {
                Done?.Invoke();
                item.Picked?.Invoke();
            }
        }

        private void Keys(KeyDownEvent key)
        {
            switch (key.keyCode)
            {
                case KeyCode.DownArrow: Move(1); break;
                case KeyCode.UpArrow: Move(-1); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: Submit(); break;
                case KeyCode.RightArrow:
                    if (highlighted >= 0 && rows[highlighted].IsMenu) OpenMenu(rows[highlighted]);
                    else return;
                    break;
                case KeyCode.LeftArrow:
                    if (search.Length == 0 && menu.Parent != null) Back();
                    else return;
                    break;
                case KeyCode.Escape: Done?.Invoke(); break;
                default: return;
            }
            key.StopImmediatePropagation();
        }
    }

    /// <summary>The window a <see cref="ForgeDropdown"/> opens in, under the field it opened from, which closes as it loses focus.</summary>
    internal sealed class ForgeDropdownWindow : EditorWindow
    {
        private ForgeDropdown dropdown;
        private bool closing;

        public static ForgeDropdownWindow Open(ForgeDropdown dropdown, Rect screenRect)
        {
            var window = CreateInstance<ForgeDropdownWindow>();
            window.dropdown = dropdown;
            window.ShowAsDropDown(screenRect, dropdown.SizeFor(screenRect.width));
            window.Build();
            return window;
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dropdown-window");
            var view = new ForgeDropdownView(dropdown);
            view.Done += Shut;
            root.Add(view);
            root.schedule.Execute(view.FocusSearch);
        }

        private void Shut()
        {
            if (closing) return;
            closing = true;
            Close();
        }

        // A domain reload leaves it with nothing to show
        private void OnEnable() => EditorApplication.delayCall += () =>
        {
            if (this != null && dropdown == null) Shut();
        };

        private void OnLostFocus() => EditorApplication.delayCall += () =>
        {
            if (this != null) Shut();
        };
    }
}
