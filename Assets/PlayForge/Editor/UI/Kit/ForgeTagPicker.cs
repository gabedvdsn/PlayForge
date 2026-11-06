using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The tag picker a tag field opens (ED-55): Unity's searchable dropdown, as Add Component's, of the tags the field's
    /// context offers, by their display names, each under its root while Group Tags by Root is on and its root has more
    /// than one; None first, then New Tag… where the field may make one. Its search finds a tag by any part of its path.
    /// It was a list drawn under the field in tinted boxes in code, with a button that showed only on hover.
    /// </summary>
    internal sealed class ForgeTagPicker : AdvancedDropdown
    {
        private const string NoneItem = "None";
        private const string NewItem = "New Tag…";
        private const int NoneId = -1;
        private const int NewId = -2;

        private readonly IReadOnlyList<Tag> tags;
        private readonly bool allowNew;
        private readonly bool group;
        private readonly string title;
        private readonly Action<Tag> picked;
        private readonly Action makeNew;
        private readonly Dictionary<int, Tag> byId = new();

        /// <param name="tags">The tags to offer.</param>
        /// <param name="allowNew">Whether New Tag… is offered.</param>
        /// <param name="group">Whether tags sit under their roots.</param>
        /// <param name="title">The dropdown's heading, as "Tags".</param>
        /// <param name="picked">What a pick does: a tag, or the default tag for None.</param>
        /// <param name="makeNew">What New Tag… does.</param>
        public ForgeTagPicker(IReadOnlyList<Tag> tags, bool allowNew, bool group, string title, Action<Tag> picked, Action makeNew)
            : base(new AdvancedDropdownState())
        {
            this.tags = tags ?? Array.Empty<Tag>();
            this.allowNew = allowNew;
            this.group = group;
            this.title = string.IsNullOrEmpty(title) ? "Tags" : title;
            this.picked = picked;
            this.makeNew = makeNew;
            minimumSize = new Vector2(260f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            byId.Clear();
            var root = new AdvancedDropdownItem(title);
            root.AddChild(new AdvancedDropdownItem(NoneItem) { id = NoneId });
            if (allowNew) root.AddChild(new AdvancedDropdownItem(NewItem) { id = NewId });
            root.AddSeparator();

            var folders = new Dictionary<string, AdvancedDropdownItem>();
            int next = 0;
            foreach (var row in RowsOf(tags, group, out var rowTags))
            {
                var tag = rowTags[next];
                int slash = row.IndexOf('/');
                var item = new AdvancedDropdownItem(slash >= 0 ? row.Substring(slash + 1) : row) { id = next };
                byId[next] = tag;
                next++;
                if (slash < 0)
                {
                    root.AddChild(item);
                    continue;
                }
                var folderName = row.Substring(0, slash);
                if (!folders.TryGetValue(folderName, out var folder))
                {
                    folder = new AdvancedDropdownItem(folderName);
                    folders[folderName] = folder;
                    root.AddChild(folder);
                }
                folder.AddChild(item);
            }
            if (next == 0) root.AddChild(new AdvancedDropdownItem("No tags for this field") { enabled = false });
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id == NoneId) picked?.Invoke(default);
            else if (item.id == NewId) makeNew?.Invoke();
            else if (byId.TryGetValue(item.id, out var tag)) picked?.Invoke(tag);
        }

        /// <summary>
        /// The picker's rows, each a tag's display path, as "Status.Burn", or its path under its root's folder, as
        /// "Status/Status.Burn", while grouping and its root has more than one tag; in natural order, folders first by
        /// name among the single tags. A tag without a path isn't offered.
        /// </summary>
        internal static List<string> Rows(IEnumerable<Tag> tags, bool group) => RowsOf(tags, group, out _);

        private static List<string> RowsOf(IEnumerable<Tag> tags, bool group, out List<Tag> order)
        {
            var natural = Comparer<string>.Create(EditorUtility.NaturalCompare);
            var listed = (tags ?? Enumerable.Empty<Tag>())
                .Where(tag => tag.IsValid)
                .Select(tag => (Tag: tag, Path: TagDisplaySettings.PathOf(tag)))
                .Where(each => !string.IsNullOrEmpty(each.Path))
                .GroupBy(each => each.Tag.Name).Select(same => same.First())
                .OrderBy(each => each.Path, natural)
                .ToList();
            var counts = listed.GroupBy(each => RootOf(each.Path)).ToDictionary(root => root.Key, root => root.Count());
            var rows = listed.Select(each =>
            {
                var root = RootOf(each.Path);
                return (each.Tag, Row: group && counts[root] > 1 ? $"{root}/{each.Path}" : each.Path);
            }).OrderBy(each => each.Row, natural).ToList();
            order = rows.Select(each => each.Tag).ToList();
            return rows.Select(each => each.Row).ToList();
        }

        private static string RootOf(string path)
        {
            int dot = path.IndexOf('.');
            return dot < 0 ? path : path.Substring(0, dot);
        }
    }
}
