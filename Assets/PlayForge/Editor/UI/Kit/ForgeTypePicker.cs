using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The picker a kind field opens (ED-56, EP-1): PlayForge's dropdown of the kinds a field can hold, by their names, None
    /// first where the field may be empty, and the kind it holds checked. Each kind sits under the submenus its
    /// <see cref="ForgeMenuAttribute"/> names, or a class it's made from names, submenus first in name order, then the
    /// kinds; its search finds any kind whatever its submenu, and its Context says a kind's description (EP-2). It was
    /// Unity's searchable dropdown, a group of one level for ability tasks alone (FN-24), and before that a list drawn under
    /// the field in tinted boxes, from styles set in C#.
    /// </summary>
    internal sealed class ForgeTypePicker
    {
        /// <summary>A kind no field holds, for a selection whose kinds differ: no kind is checked, nor None.</summary>
        internal static readonly Type Mixed = typeof(ForgeTypePicker);

        /// <summary>The dropdown it shows, which a test reads.</summary>
        public ForgeDropdown Dropdown { get; }

        /// <param name="kinds">The kinds to offer, in the order to list them.</param>
        /// <param name="allowNone">Whether None is offered.</param>
        /// <param name="title">The dropdown's heading, as "Task".</param>
        /// <param name="picked">What a pick does: a kind, or null for None.</param>
        /// <param name="current">The kind the field holds, which it checks; null for none.</param>
        public ForgeTypePicker(IReadOnlyList<(Type Type, string Name)> kinds, bool allowNone, string title, Action<Type> picked, Type current = null)
        {
            Dropdown = new ForgeDropdown(string.IsNullOrEmpty(title) ? "Kind" : title) { Empty = "No kinds for this field" };
            if (allowNone) Dropdown.AddFixed("None", () => picked?.Invoke(null)).Checked = current == null;
            foreach (var (type, name) in kinds ?? Array.Empty<(Type, string)>())
            {
                var kind = type;
                Dropdown.Add(ForgeMenuAttribute.PathOf(kind), name, () => picked?.Invoke(kind), ForgeMenuAttribute.DescriptionOf(kind),
                    isChecked: kind != null && kind == current);
            }
        }

        /// <summary>Shows it under a rect of the field's panel, as the field's box.</summary>
        public void Show(Rect worldBound) => Dropdown.Show(worldBound);
    }
}
