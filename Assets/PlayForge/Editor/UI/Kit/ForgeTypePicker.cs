using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The picker a kind field opens (ED-56): Unity's searchable dropdown, as Add Component's, of the kinds a field can
    /// hold, by their names, with None first where the field may be empty. It was a list drawn under the field in tinted
    /// boxes, from styles set in C#.
    /// </summary>
    internal sealed class ForgeTypePicker : AdvancedDropdown
    {
        private const int NoneId = -1;

        private readonly IReadOnlyList<(Type Type, string Name)> kinds;
        private readonly bool allowNone;
        private readonly string title;
        private readonly Action<Type> picked;

        /// <param name="kinds">The kinds to offer, in the order to list them.</param>
        /// <param name="allowNone">Whether None is offered.</param>
        /// <param name="title">The dropdown's heading, as "Task".</param>
        /// <param name="picked">What a pick does: a kind, or null for None.</param>
        public ForgeTypePicker(IReadOnlyList<(Type Type, string Name)> kinds, bool allowNone, string title, Action<Type> picked)
            : base(new AdvancedDropdownState())
        {
            this.kinds = kinds ?? Array.Empty<(Type, string)>();
            this.allowNone = allowNone;
            this.title = string.IsNullOrEmpty(title) ? "Kind" : title;
            this.picked = picked;
            minimumSize = new Vector2(240f, 280f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(title);
            if (allowNone)
            {
                root.AddChild(new AdvancedDropdownItem("None") { id = NoneId });
                root.AddSeparator();
            }
            for (int i = 0; i < kinds.Count; i++) root.AddChild(new AdvancedDropdownItem(kinds[i].Name) { id = i });
            if (kinds.Count == 0) root.AddChild(new AdvancedDropdownItem("No kinds for this field") { enabled = false });
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id == NoneId) picked?.Invoke(null);
            else if (item.id >= 0 && item.id < kinds.Count) picked?.Invoke(kinds[item.id].Type);
        }
    }
}
