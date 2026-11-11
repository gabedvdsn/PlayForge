using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A field's menu, as a number field's three-dot button opens it, whose items file under submenus by their paths, as
    /// "Scaler Assets/Fire Damage" (ED-77). In an editor window it's Unity's own menu, which shows a path's submenus, as a
    /// kind field's popup does there. In a runtime panel, where tests draw fields, it's UI Toolkit's dropdown, which has no
    /// submenus in this Unity, so each item there shows its path. The field menu was that dropdown everywhere, so every
    /// Scaler asset sat at its top (the user's report). Either opens at the button, as wide as its items (ED-52).
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

            public Item(string path, bool isChecked, bool on, Action action, bool separator)
            {
                Path = path ?? "";
                Checked = isChecked;
                On = on;
                Action = action;
                Separator = separator;
            }
        }

        private readonly List<Item> items = new();

        /// <summary>Adds an item that does something. Slashes in its path file it under submenus.</summary>
        public void AddItem(string path, bool isChecked, Action action) => items.Add(new Item(path, isChecked, action != null, action, false));

        /// <summary>Adds an item that shows, and can't be chosen.</summary>
        public void AddDisabledItem(string path, bool isChecked) => items.Add(new Item(path, isChecked, false, null, false));

        /// <summary>Adds a line between items, in the submenu the path names, or the menu's own for "".</summary>
        public void AddSeparator(string path) => items.Add(new Item(path, false, false, null, true));

        /// <summary>The items' paths, in order, with whether each can be chosen; separators left out.</summary>
        public IEnumerable<(string Path, bool On)> Items => items.Where(item => !item.Separator).Select(item => (item.Path, item.On));

        /// <summary>Opens the menu at an element, as its button: Unity's own menu in an editor window, else the dropdown.</summary>
        public void DropDown(VisualElement anchor)
        {
            if (anchor?.panel == null) return;
            if (anchor.panel.contextType == ContextType.Editor)
            {
                var menu = new GenericMenu();
                foreach (var item in items)
                {
                    if (item.Separator) menu.AddSeparator(item.Path);
                    else if (item.On)
                    {
                        var action = item.Action;
                        menu.AddItem(new GUIContent(item.Path), item.Checked, () => action());
                    }
                    else menu.AddDisabledItem(new GUIContent(item.Path), item.Checked);
                }
                menu.DropDown(anchor.worldBound);
                return;
            }

            var dropdown = new GenericDropdownMenu();
            foreach (var item in items)
            {
                if (item.Separator) dropdown.AddSeparator(item.Path);
                else if (item.On) dropdown.AddItem(item.Path, item.Checked, item.Action);
                else dropdown.AddDisabledItem(item.Path, item.Checked);
            }
            // At the button, as wide as its items: anchored alone, it took the button's 20 px and no item could be read (ED-52)
            dropdown.DropDown(anchor.worldBound, anchor, true, true);
        }
    }
}
