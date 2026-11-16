using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The tag picker a tag field opens (ED-55, EP-1): PlayForge's dropdown of the tags the field's context offers, by their
    /// display names, each under its root while Group Tags by Root is on and its root has more than one; None first, then
    /// New Tag… where the field may make one, and the field's tag checked. Its search finds a tag by any part of its path,
    /// and its Context says the contexts a tag is for: those it's used in, declared for, or exposed for in code (the user's
    /// note, EP-1). It was Unity's searchable dropdown, and before that a list drawn under the field in tinted boxes in
    /// code, with a button that showed only on hover.
    /// </summary>
    internal sealed class ForgeTagPicker
    {
        private const string NoneItem = "None";
        private const string NewItem = "New Tag…";

        /// <summary>The dropdown it shows, which a test reads.</summary>
        public ForgeDropdown Dropdown { get; }

        /// <param name="tags">The tags to offer.</param>
        /// <param name="allowNew">Whether New Tag… is offered.</param>
        /// <param name="group">Whether tags sit under their roots.</param>
        /// <param name="title">The dropdown's heading, as "Tags".</param>
        /// <param name="picked">What a pick does: a tag, or the default tag for None.</param>
        /// <param name="makeNew">What New Tag… does.</param>
        /// <param name="current">The field's tag, which it checks.</param>
        public ForgeTagPicker(IReadOnlyList<Tag> tags, bool allowNew, bool group, string title, Action<Tag> picked, Action makeNew, Tag current = default)
        {
            // In the rows' own order, a root's folder where its first tag is, as the picker always listed them
            Dropdown = new ForgeDropdown(string.IsNullOrEmpty(title) ? "Tags" : title) { Empty = "No tags for this field", MenusFirst = false };
            Dropdown.AddFixed(NoneItem, () => picked?.Invoke(default)).Checked = !current.IsValid;
            if (allowNew) Dropdown.AddFixed(NewItem, makeNew, "Make a tag, and use it here");

            // The contexts in the menus' order, read once for every tag's
            var order = ForgeTagContexts.All();
            var rows = RowsOf(tags, group, out var rowTags);
            for (int i = 0; i < rows.Count; i++)
            {
                var tag = rowTags[i];
                int slash = rows[i].IndexOf('/');
                var path = slash >= 0 ? rows[i].Substring(0, slash) : "";
                var name = slash >= 0 ? rows[i].Substring(slash + 1) : rows[i];
                var item = Dropdown.Add(path, name, () => picked?.Invoke(tag), ContextOf(tag, order), isChecked: current.IsValid && tag.Name == current.Name);
                item.SearchText = TagDisplaySettings.PathOf(tag);
            }
        }

        /// <summary>Shows it under a rect of the field's panel.</summary>
        public void Show(Rect worldBound) => Dropdown.Show(worldBound);

        /// <summary>What the picker says of a tag: the contexts it's for, used, declared or exposed in code (EP-1).</summary>
        internal static string ContextOf(Tag tag, IReadOnlyList<string> order = null)
        {
            var contexts = ForgeTagContexts.ContextsOf(tag, order);
            return contexts.Count == 0 ? "For no context yet: no field uses it, and it's declared for none" : $"For {string.Join(", ", contexts)}";
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
