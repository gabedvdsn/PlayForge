using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The picker a kind field opens (ED-56, EP-1): PlayForge's dropdown of the kinds a field can hold, by their names, None
    /// first where the field may be empty, and the kind it holds checked. Kinds that name a category, by
    /// <see cref="ForgeCategoryAttribute"/>, are listed in a submenu for each, in name order, the kinds that name none after
    /// them; its search finds any kind whatever its category (FN-24). It was Unity's searchable dropdown, and before that a
    /// list drawn under the field in tinted boxes, from styles set in C#.
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
                Dropdown.Add(CategoryOf(kind) ?? "", name, () => picked?.Invoke(kind), isChecked: kind != null && kind == current);
            }
        }

        /// <summary>Shows it under a rect of the field's panel, as the field's box.</summary>
        public void Show(Rect worldBound) => Dropdown.Show(worldBound);

        /// <summary>The category a kind names, its class's or the one it's made from, or null for none (FN-24).</summary>
        internal static string CategoryOf(Type type) =>
            type == null ? null : (System.Attribute.GetCustomAttribute(type, typeof(ForgeCategoryAttribute), true) as ForgeCategoryAttribute)?.Name;
    }
}
