using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What can be done to assets, one menu for The Forge's list and an asset's header (ED-71): Show in Project, Open in a
    /// Window, Duplicate, Move to Project and Copy to Project (FEAT-34), and Move to Trash. A right-click on a row of The
    /// Forge's list offered these, and the header's three dots its own actions alone.
    /// </summary>
    internal static class ForgeAssetActions
    {
        /// <summary>An action: its place in the menu, whether it can be chosen, and what it does; or a line between.</summary>
        internal readonly struct Item
        {
            public readonly string Path;
            public readonly bool On;
            public readonly Action Act;
            public bool IsSeparator => Act == null && !On && Path == null;

            public Item(string path, bool on, Action act)
            {
                Path = path;
                On = on;
                Act = act;
            }

            public static readonly Item Separator = new(null, false, null);
        }

        /// <summary>
        /// The actions on assets: on the first, where one is meant, or each, as moving them into another project. A
        /// copy Duplicate makes is shown as <paramref name="showCopy"/> says.
        /// </summary>
        public static List<Item> For(IReadOnlyList<ScriptableObject> assets, Action<ScriptableObject> showCopy)
        {
            var all = (assets ?? Array.Empty<ScriptableObject>()).Where(asset => asset != null).ToList();
            var first = all.FirstOrDefault();
            bool one = first != null;
            var items = new List<Item>
            {
                new("Show in Project", one, () =>
                {
                    Selection.activeObject = first;
                    EditorGUIUtility.PingObject(first);
                }),
                new("Open in a Window", one, () => EditorUtility.OpenPropertyEditor(first)),
                Item.Separator,
                new("Duplicate", one, () =>
                {
                    var copy = ForgeAssetCreation.Duplicate(first);
                    if (copy != null) showCopy?.Invoke(copy);
                }),
            };

            // Each, into another Forge Project's folder for its type (FEAT-34)
            var others = ForgeProjects.All().Where(project => project != ForgeProjects.Active).ToList();
            foreach (var project in others)
            {
                var each = project;
                items.Add(new Item($"Move to Project/{each.name}", one, () =>
                {
                    foreach (var asset in all) ForgeProjects.MoveInto(asset, each);
                }));
            }
            foreach (var project in others)
            {
                var each = project;
                items.Add(new Item($"Copy to Project/{each.name}", one, () =>
                {
                    foreach (var asset in all) ForgeProjects.CopyInto(asset, each);
                }));
            }
            if (others.Count == 0)
            {
                items.Add(new Item("Move to Project", false, null));
                items.Add(new Item("Copy to Project", false, null));
            }
            items.Add(new Item("Move to Trash", one, () => ForgeAssetDeletion.MoveToTrash(first)));
            return items;
        }

        /// <summary>The actions in a UI Toolkit menu, as a right-click on The Forge's list opens.</summary>
        public static void AddTo(DropdownMenu menu, IEnumerable<Item> items)
        {
            foreach (var item in items)
            {
                if (item.IsSeparator)
                {
                    menu.AppendSeparator();
                    continue;
                }
                var act = item.Act;
                menu.AppendAction(item.Path, _ => act?.Invoke(), item.On ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        /// <summary>The actions in Unity's own menu, as an asset header's three dots open.</summary>
        public static void AddTo(GenericMenu menu, IEnumerable<Item> items)
        {
            foreach (var item in items)
            {
                if (item.IsSeparator)
                {
                    menu.AddSeparator("");
                    continue;
                }
                var act = item.Act;
                if (item.On && act != null) menu.AddItem(new GUIContent(item.Path), false, () => act());
                else menu.AddDisabledItem(new GUIContent(item.Path));
            }
        }

        /// <summary>
        /// Shows a copy as the menu that made it was opened: in The Forge where The Forge has focus, else selected in the
        /// Project window.
        /// </summary>
        public static void ShowCopy(ScriptableObject copy)
        {
            if (copy == null) return;
            if (EditorWindow.focusedWindow is TheForge)
            {
                TheForge.OpenToAsset(copy);
                return;
            }
            Selection.activeObject = copy;
            EditorGUIUtility.PingObject(copy);
        }
    }
}
