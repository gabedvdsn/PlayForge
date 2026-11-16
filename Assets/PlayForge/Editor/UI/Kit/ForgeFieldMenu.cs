using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A field's menu, as a number field's three-dot button opens it, whose items file under submenus by their paths, as
    /// "Scaler Assets/Fire Damage". In an editor window it's PlayForge's dropdown, searchable, no taller or
    /// wider than Preferences sets, its Context saying more of an item where the menu gives it; it was Unity's own menu, the
    /// operating system's, which ran the screen's height. In a runtime panel, where tests draw fields, it's UI Toolkit's
    /// dropdown, which has no submenus in this Unity, so each item there shows its path. Either opens at the button, as wide
    /// as its items.
    /// </summary>
    internal sealed class ForgeFieldMenu
    {
        private readonly struct Item
        {
            public readonly string Path;
            public readonly bool Checked;
            public readonly bool On;
            public readonly Action Action;
            public readonly bool Separator;
            public readonly string Context;

            public Item(string path, bool isChecked, bool on, Action action, bool separator, string context)
            {
                Path = path ?? "";
                Checked = isChecked;
                On = on;
                Action = action;
                Separator = separator;
                Context = context;
            }
        }

        private readonly List<Item> items = new();

        /// <param name="title">The dropdown's heading, as "Contexts".</param>
        public ForgeFieldMenu(string title = "Menu")
        {
            Title = title;
        }

        /// <summary>The menu's heading in the editor.</summary>
        public string Title { get; }

        /// <summary>Adds an item that does something. Slashes in its path file it under submenus.</summary>
        public void AddItem(string path, bool isChecked, Action action, string context = null) =>
            items.Add(new Item(path, isChecked, action != null, action, false, context));

        /// <summary>Adds an item that shows, and can't be chosen; its context can say why.</summary>
        public void AddDisabledItem(string path, bool isChecked, string context = null) =>
            items.Add(new Item(path, isChecked, false, null, false, context));

        /// <summary>Adds a line between items, in the submenu the path names, or the menu's own for "".</summary>
        public void AddSeparator(string path) => items.Add(new Item(path, false, false, null, true, null));

        /// <summary>The items' paths, in order, with whether each can be chosen; separators left out.</summary>
        public IEnumerable<(string Path, bool On)> Items => items.Where(item => !item.Separator).Select(item => (item.Path, item.On));

        /// <summary>The menu as PlayForge's dropdown: each item under its path's submenus, in the order given.</summary>
        public ForgeDropdown ToDropdown()
        {
            var dropdown = new ForgeDropdown(Title) { MenusFirst = false };
            foreach (var item in items)
            {
                if (item.Separator)
                {
                    dropdown.AddSeparator(item.Path);
                    continue;
                }
                int slash = item.Path.LastIndexOf('/');
                var path = slash < 0 ? "" : item.Path.Substring(0, slash);
                var name = slash < 0 ? item.Path : item.Path.Substring(slash + 1);
                dropdown.Add(path, name, item.Action, item.Context, item.Checked, item.On);
            }
            return dropdown;
        }

        /// <summary>Opens the menu at an element, as its button: PlayForge's dropdown in an editor window, else UI Toolkit's.</summary>
        public void DropDown(VisualElement anchor)
        {
            if (anchor?.panel == null) return;
            if (anchor.panel.contextType == ContextType.Editor)
            {
                ToDropdown().Show(anchor);
                return;
            }

            var dropdown = new GenericDropdownMenu();
            foreach (var item in items)
            {
                if (item.Separator) dropdown.AddSeparator(item.Path);
                else if (item.On) dropdown.AddItem(item.Path, item.Checked, item.Action);
                else dropdown.AddDisabledItem(item.Path, item.Checked);
            }
            // At the button, as wide as its items: anchored alone, it took the button's 20 px and no item could be read
            dropdown.DropDown(anchor.worldBound, anchor, true, true);
        }
    }
}
