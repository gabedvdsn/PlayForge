using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The Tags view, as the design canvas's Tags board draws it: every tag the entities hold, as a tree by its path,
    // beside who holds the one selected and what grants it them. The Tag Debugger listed one entity's tags in a window of
    // its own
    public sealed partial class RuntimeDebugger
    {
        private MultiColumnTreeView tagTree;
        private VisualElement tagDetail;
        private string shownTagPath;

        // What a sample changes in the tag's detail, in place
        private readonly List<Action> tagUpdates = new();

        // Each path's id in the tree, kept across rebuilds so a row keeps its fold and its selection, and the ids the tree
        // has opened once, so a fold the user closed stays closed
        private readonly Dictionary<string, int> tagIds = new();
        private readonly HashSet<int> tagsOpened = new();
        private readonly Dictionary<string, TagNode> tagNodes = new();

        // A row of the tree: a tag's path, or the part of one that tags share. A parent's counts take in its children's
        private sealed class TagNode
        {
            public string Path;
            public string Name;
            public int Depth;
            public int Entities;
            public int Weight;
            public readonly List<TagNode> Children = new();
        }

        // What grants an entity a tag: its kind, which sets its icon, what it is by name, and where that came from
        private readonly struct TagGrant
        {
            public readonly Type Kind;
            public readonly string By;
            public readonly string Source;

            public TagGrant(Type kind, string by, string source)
            {
                Kind = kind;
                By = by;
                Source = source;
            }
        }

        private VisualElement BuildTagsView()
        {
            var view = new VisualElement { name = "TagsView" };
            view.AddToClassList("pf-debugger__view");
            var split = new TwoPaneSplitView(0, 440, TwoPaneSplitViewOrientation.Horizontal) { name = "TagSplit" };
            split.AddToClassList("pf-debugger__split");

            tagTree = new MultiColumnTreeView
            {
                name = "Tags",
                fixedItemHeight = 20,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            tagTree.AddToClassList("pf-debugger__list");
            tagTree.columns.Add(new Column
            {
                name = "Tag", title = "Tag", width = 240, stretchable = true,
                makeCell = () =>
                {
                    var label = new Label();
                    label.AddToClassList("pf-debugger__cell");
                    label.AddToClassList("pf-debugger__cell--strong");
                    return label;
                },
                bindCell = (cell, index) => ((Label)cell).text = tagTree.GetItemDataForIndex<TagNode>(index).Name,
            });
            tagTree.columns.Add(TagColumn("Entities", "Entities", 72, node => node.Entities.ToString()));
            tagTree.columns.Add(TagColumn("Weight", "Weight", 60, node => node.Weight.ToString()));
            tagTree.selectionChanged += chosen => ShowTag(chosen.OfType<TagNode>().FirstOrDefault()?.Path);
            split.Add(tagTree);

            var scroll = new ScrollView { name = "TagScroll" };
            scroll.AddToClassList("pf-debugger__detail-scroll");
            tagDetail = new VisualElement { name = "TagDetail" };
            tagDetail.AddToClassList("pf-debugger__detail");
            scroll.Add(tagDetail);
            split.Add(scroll);
            view.Add(split);
            ShowTag(null);
            return view;
        }

        private Column TagColumn(string name, string title, float width, Func<TagNode, string> text) => new()
        {
            name = name, title = title, width = width,
            makeCell = () =>
            {
                var label = new Label();
                label.AddToClassList("pf-debugger__cell");
                label.AddToClassList("pf-debugger__cell--number");
                return label;
            },
            bindCell = (cell, index) => ((Label)cell).text = text(tagTree.GetItemDataForIndex<TagNode>(index)),
        };

        // The tree of the tags the hubs the filter lets through hold, and the search matches, keeping the selection
        private void RefillTags()
        {
            if (tagTree == null) return;
            var roots = TagRoots(out var nodes);
            tagNodes.Clear();
            foreach (var node in nodes) tagNodes[node.Path] = node;
            tagTree.SetRootItems(roots.Select(TreeItem).ToList());
            tagTree.Rebuild();
            // A parent opens the first time it's shown
            foreach (var node in nodes)
            {
                int id = tagIds[node.Path];
                if (node.Children.Count > 0 && tagsOpened.Add(id)) tagTree.ExpandItem(id, false, false);
            }
            tagTree.RefreshItems();
            if (shownTagPath != null && tagNodes.ContainsKey(shownTagPath))
                tagTree.SetSelectionByIdWithoutNotify(new[] { tagIds[shownTagPath] });
            else
            {
                tagTree.SetSelectionByIdWithoutNotify(Array.Empty<int>());
                if (shownTagPath != null) ShowTag(null);
            }
        }

        private TreeViewItemData<TagNode> TreeItem(TagNode node)
        {
            if (!tagIds.TryGetValue(node.Path, out int id)) tagIds[node.Path] = id = tagIds.Count + 1;
            return new TreeViewItemData<TagNode>(id, node, node.Children.Select(TreeItem).ToList());
        }

        // The counts change in place; a tag that's held or let go, or a hub that joins or leaves, builds the tree again
        private void SampleTagTree()
        {
            if (tagTree == null) return;
            TagRoots(out var nodes);
            if (nodes.Count != tagNodes.Count || nodes.Any(node => !tagNodes.ContainsKey(node.Path)))
            {
                RefillTags();
                return;
            }
            foreach (var node in nodes)
            {
                var shown = tagNodes[node.Path];
                shown.Entities = node.Entities;
                shown.Weight = node.Weight;
            }
            tagTree.RefreshItems();
        }

        // The tags the shown hubs hold, as a tree by their paths, each path's parts in natural order
        private List<TagNode> TagRoots(out List<TagNode> all)
        {
            var nodes = new Dictionary<string, TagNode>();
            var holders = new Dictionary<string, HashSet<IGameplayAbilitySystem>>();
            var roots = new List<TagNode>();
            foreach (var gas in hubs)
            {
                if (!Alive(gas) || !ShowsHub(gas)) continue;
                foreach (var tag in (gas.GetAppliedTags() ?? new List<Tag>()).Distinct())
                {
                    var path = tag.Name;
                    if (string.IsNullOrEmpty(path) || !Matches(path)) continue;
                    int weight = Math.Max(0, gas.GetTagWeight(tag));
                    var parts = path.Split('.');
                    string at = null;
                    TagNode parent = null;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        at = at == null ? parts[i] : $"{at}.{parts[i]}";
                        if (!nodes.TryGetValue(at, out var node))
                        {
                            nodes[at] = node = new TagNode { Path = at, Name = parts[i], Depth = i };
                            holders[at] = new HashSet<IGameplayAbilitySystem>();
                            (parent?.Children ?? roots).Add(node);
                        }
                        node.Weight += weight;
                        holders[at].Add(gas);
                        parent = node;
                    }
                }
            }
            foreach (var node in nodes.Values) node.Entities = holders[node.Path].Count;
            Order(roots);
            all = nodes.Values.ToList();
            return roots;

            static void Order(List<TagNode> level)
            {
                level.Sort((a, b) => NaturalOrder.Compare(a.Name, b.Name));
                foreach (var node in level) Order(node.Children);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The detail
        // ═══════════════════════════════════════════════════════════════════════════

        // The tag the detail shows: its path, how many entities hold it, then each of them, with what grants it them
        private void ShowTag(string path)
        {
            if (tagDetail == null) return;
            if (path != null && path == shownTagPath && tagUpdates.Count > 0) return;
            tagDetail.Clear();
            tagUpdates.Clear();
            shownTagPath = path;
            if (path == null)
            {
                tagDetail.Add(Note("Select a tag to see who holds it."));
                return;
            }

            var header = Header("TagHeader", new ForgeGlyph(ForgeGlyphShape.Tag), out var title, out var detail);
            var nameRow = new VisualElement { name = "HeaderNameRow" };
            nameRow.AddToClassList("pf-debugger__header-name-row");
            title.parent.Insert(0, nameRow);
            int dot = path.LastIndexOf('.');
            var parents = new Label(dot >= 0 ? path.Substring(0, dot + 1) : "") { name = "HeaderParents" };
            parents.AddToClassList("pf-debugger__header-name");
            parents.AddToClassList("pf-debugger__header-parents");
            nameRow.Add(parents);
            nameRow.Add(title);
            title.text = dot >= 0 ? path.Substring(dot + 1) : path;
            tagDetail.Add(header);

            var section = Section("HeldBy", "Held By");
            section.Content.Add(TableHead(("Entity", "pf-debugger__col-grow"), ("Weight", "pf-debugger__col-60"),
                ("Granted by", "pf-debugger__col-grow"), ("Source", "pf-debugger__col-grow")));
            var rows = new VisualElement { name = "HolderRows" };
            section.Content.Add(rows);
            tagDetail.Add(section);

            var shown = new List<IGameplayAbilitySystem>();
            var updates = new List<Action>();
            void Build(List<IGameplayAbilitySystem> holding)
            {
                rows.Clear();
                updates.Clear();
                shown.Clear();
                shown.AddRange(holding);
                for (int i = 0; i < shown.Count; i++) rows.Add(HolderRow(shown[i], path, i, updates));
                if (shown.Count == 0) rows.Add(Note("None"));
                section.Summary.text = shown.Count.ToString();
            }
            tagUpdates.Add(() =>
            {
                var holding = HoldersOf(path);
                if (holding.Count != shown.Count || holding.Where((gas, i) => !ReferenceEquals(gas, shown[i])).Any()) Build(holding);
                foreach (var update in updates) update();
                int weight = shown.Sum(gas => HeldUnder(gas, path).Sum(held => held.Weight));
                detail.text = $"Tag · {CountOf(shown.Count, "entity", "entities")} · weight {weight}";
            });
            SampleTag();
        }

        private VisualElement HolderRow(IGameplayAbilitySystem gas, string path, int index, List<Action> updates)
        {
            var row = TableRow($"Holder {NameOf(gas)}", index, ("Weight", "pf-debugger__col-60"));
            Lead(row, NameCell(typeof(EntityIdentity), NameOf(gas)));
            var weight = row.Q<Label>("Weight");
            weight.AddToClassList("pf-debugger__cell--number");

            var granted = new VisualElement { name = "GrantedBy" };
            granted.AddToClassList("pf-debugger__cell");
            granted.AddToClassList("pf-debugger__col-grow");
            granted.AddToClassList("pf-debugger__name-cell");
            row.Add(granted);
            var source = new Label { name = "Source" };
            source.AddToClassList("pf-debugger__cell");
            source.AddToClassList("pf-debugger__col-grow");
            source.AddToClassList("pf-debugger__cell--secondary");
            row.Add(source);

            Type shownKind = null;
            Label by = null;
            updates.Add(() =>
            {
                var held = HeldUnder(gas, path).ToList();
                weight.text = held.Sum(each => each.Weight).ToString();
                var grants = held.SelectMany(each => SourcesOf(gas, each.Tag)).ToList();
                var kind = grants.Count > 0 ? grants[0].Kind : null;
                if (by == null || kind != shownKind)
                {
                    granted.Clear();
                    granted.Add(IconOf(kind));
                    by = new Label { name = "Name" };
                    by.AddToClassList("pf-debugger__name");
                    granted.Add(by);
                    shownKind = kind;
                }
                by.text = grants.Count > 0 ? string.Join(", ", grants.Select(grant => grant.By).Distinct()) : "—";
                source.text = grants.Count > 0 ? string.Join(", ", grants.Select(grant => grant.Source).Distinct()) : "—";
            });
            return row;
        }

        private static VisualElement IconOf(Type kind) => kind != null ? new ForgeTypeIcon(kind) : new ForgeGlyph(ForgeGlyphShape.Tag);

        private void SampleTag()
        {
            if (shownTagPath == null) return;
            foreach (var update in tagUpdates) update();
        }

        // The hubs that hold a path's tag, or one under it, in the list's order
        private List<IGameplayAbilitySystem> HoldersOf(string path) =>
            hubs.Where(gas => Alive(gas) && ShowsHub(gas) && HeldUnder(gas, path).Any()).ToList();

        // The tags an entity holds at a path or under it, each with its weight
        private static IEnumerable<(Tag Tag, int Weight)> HeldUnder(IGameplayAbilitySystem gas, string path)
        {
            foreach (var tag in (gas.GetAppliedTags() ?? new List<Tag>()).Distinct())
            {
                var name = tag.Name ?? "";
                if (name != path && !name.StartsWith(path + ".", StringComparison.Ordinal)) continue;
                int weight = gas.GetTagWeight(tag);
                if (weight > 0) yield return (tag, weight);
            }
        }

        // What grants an entity a tag, as its compile walks them: its attribute set, its effects, its abilities, its items,
        // its identity, then the tags granted it directly (GrantedTagCompiler, FEAT-16)
        private static List<TagGrant> SourcesOf(IGameplayAbilitySystem gas, Tag tag)
        {
            var grants = new List<TagGrant>();
            AttributeSet set;
            Tag assetTag;
            List<Tag> identityTags;
            if (gas.ToGASObject() is { } body && body.EntityData != null)
            {
                set = body.EntityData.AttributeSet;
                assetTag = body.EntityData.AssetTag;
                identityTags = body.EntityData.GrantedTags;
            }
            else if (gas is RuntimeGameplayAbilitySystem runtime && runtime.EntityData != null)
            {
                set = runtime.EntityData.AttributeSet;
                assetTag = runtime.EntityData.AssetTag;
                identityTags = runtime.EntityData.GrantedTags;
            }
            else
            {
                set = null;
                assetTag = default;
                identityTags = null;
            }

            if (set != null && set.GetGrantedTags().Contains(tag)) grants.Add(new TagGrant(typeof(AttributeSet), set.GetName(), "Attribute set"));

            foreach (var container in EffectsOn(gas))
            {
                var effect = container.Spec?.Base;
                if (effect == null) continue;
                if (effect.Tags.AssetTag == tag || effect.GetGrantedTags().Contains(tag))
                    grants.Add(new TagGrant(typeof(GameplayEffect), effect.GetName(), OriginOf(gas, container.Spec)));
            }

            foreach (var container in gas.GetAbilitySystem()?.GetAbilityContainers() ?? new List<AbilitySpecContainer>())
            {
                var ability = container.Spec?.Base;
                if (ability == null) continue;
                if (ability.Tags.AssetTag == tag) grants.Add(new TagGrant(typeof(Ability), ability.GetName(), "Asset tag"));
                else if (ability.Tags.PassiveGrantedTags?.Contains(tag) == true) grants.Add(new TagGrant(typeof(Ability), ability.GetName(), "Passive"));
                else if (container.IsActive && ability.Tags.ActiveGrantedTags?.Contains(tag) == true)
                    grants.Add(new TagGrant(typeof(Ability), ability.GetName(), "Active"));
            }

            var items = gas.GetItemSystem()?.GetItemContainers();
            if (items != null)
                foreach (var container in items)
                {
                    var item = container.Spec?.Base;
                    if (item == null) continue;
                    if (item.Tags.AssetTag == tag || container.GetGrantedTags().Contains(tag))
                        grants.Add(new TagGrant(typeof(Item), item.GetName(), "Item"));
                }

            if (assetTag == tag) grants.Add(new TagGrant(typeof(EntityIdentity), "Asset tag", "Self"));
            if (gas.GetAffiliation()?.Contains(tag) == true) grants.Add(new TagGrant(typeof(EntityIdentity), "Affiliation", "Self"));
            if (identityTags?.Contains(tag) == true) grants.Add(new TagGrant(typeof(EntityIdentity), "Granted tags", "Self"));
            if (gas.GetLooseTags()?.Contains(tag) == true) grants.Add(new TagGrant(null, "Loose tag", "Granted directly"));
            return grants;
        }
    }
}
