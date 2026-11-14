using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The picker a sequence reference opens (FN-24): Unity's searchable dropdown, as a kind field's, of the project's
    /// sequences by their names, None first. Where more than one class holds sequences, each class's are a submenu under
    /// its name; its search finds any sequence whatever its class. It was Unity's popup, one long list.
    /// </summary>
    internal sealed class ForgeSequencePicker : AdvancedDropdown
    {
        private const int NoneId = -1;

        private readonly IReadOnlyList<TaskSequenceReferenceDrawer.Entry> entries;
        private readonly Action<TaskSequenceReferenceDrawer.Entry?> picked;

        /// <param name="entries">The sequences to offer, in the order to list them.</param>
        /// <param name="picked">What a pick does: a sequence, or null for None.</param>
        public ForgeSequencePicker(IReadOnlyList<TaskSequenceReferenceDrawer.Entry> entries, Action<TaskSequenceReferenceDrawer.Entry?> picked)
            : base(new AdvancedDropdownState())
        {
            this.entries = entries ?? Array.Empty<TaskSequenceReferenceDrawer.Entry>();
            this.picked = picked;
            minimumSize = new Vector2(240f, 280f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Sequence");
            root.AddChild(new AdvancedDropdownItem(TaskSequenceReferenceDrawer.None) { id = NoneId });
            root.AddSeparator();

            // A submenu for each class that holds sequences, where there's more than one, in name order
            var classes = new SortedDictionary<string, AdvancedDropdownItem>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                var name = ClassName(entries[i].Method.DeclaringType);
                if (!classes.ContainsKey(name)) classes[name] = null;
            }
            bool grouped = classes.Count > 1;
            for (int i = 0; i < entries.Count; i++)
            {
                var item = new AdvancedDropdownItem(entries[i].Name) { id = i };
                if (!grouped)
                {
                    root.AddChild(item);
                    continue;
                }
                var name = ClassName(entries[i].Method.DeclaringType);
                var group = classes[name] ??= new AdvancedDropdownItem(name);
                group.AddChild(item);
            }
            if (grouped) foreach (var group in classes.Values) if (group != null) root.AddChild(group);
            if (entries.Count == 0) root.AddChild(new AdvancedDropdownItem("No sequences in the project") { enabled = false });
            return root;
        }

        private static string ClassName(Type type) => type == null ? "Other" : ObjectNames.NicifyVariableName(type.Name);

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id == NoneId) picked?.Invoke(null);
            else if (item.id >= 0 && item.id < entries.Count) picked?.Invoke(entries[item.id]);
        }
    }
}
