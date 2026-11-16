using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// The picker a sequence reference opens: PlayForge's dropdown of the project's sequences by their names,
    /// None first and the one referenced checked. Where more than one class holds sequences, each class's are a submenu
    /// under its name; its search finds any sequence whatever its class, and its Context says the method a sequence is. It
    /// was Unity's searchable dropdown, and before that Unity's popup, one long list.
    /// </summary>
    internal sealed class ForgeSequencePicker
    {
        /// <summary>The dropdown it shows, which a test reads.</summary>
        public ForgeDropdown Dropdown { get; }

        /// <param name="entries">The sequences to offer, in the order to list them.</param>
        /// <param name="picked">What a pick does: a sequence, or null for None.</param>
        /// <param name="current">The name of the sequence referenced, which it checks; null for none.</param>
        public ForgeSequencePicker(IReadOnlyList<TaskSequenceReferenceDrawer.Entry> entries, Action<TaskSequenceReferenceDrawer.Entry?> picked, string current = null)
        {
            entries ??= Array.Empty<TaskSequenceReferenceDrawer.Entry>();
            Dropdown = new ForgeDropdown("Sequence") { Empty = "No sequences in the project" };
            Dropdown.AddFixed(TaskSequenceReferenceDrawer.None, () => picked?.Invoke(null)).Checked = string.IsNullOrEmpty(current);

            // A submenu for each class that holds sequences, where there's more than one, in name order
            bool grouped = entries.Select(entry => ClassName(entry.Method.DeclaringType)).Distinct().Count() > 1;
            foreach (var entry in entries)
            {
                var each = entry;
                var owner = entry.Method.DeclaringType;
                Dropdown.Add(grouped ? ClassName(owner) : "", entry.Name, () => picked?.Invoke(each),
                    $"{owner?.Name ?? "?"}.{entry.Method.Name}, in code", isChecked: entry.Name == current);
            }
        }

        /// <summary>Shows it under a rect of the field's panel.</summary>
        public void Show(Rect worldBound) => Dropdown.Show(worldBound);

        private static string ClassName(Type type) => type == null ? "Other" : ObjectNames.NicifyVariableName(type.Name);
    }
}
