using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A cue library's inspector: its routes, each a cue tag and the handlers it plays; then each cue tag the project's
    /// assets raise through it, by a Cue Tag cue, with the route that plays it, or that none does, and the assets that
    /// raise it, each a row that opens its asset in The Forge. It follows the project as it changes, and its routes as
    /// they're edited.
    /// </summary>
    [CustomEditor(typeof(CueLibrary))]
    public class CueLibraryEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var library = (CueLibrary)target;
            var root = new VisualElement { name = "CueLibraryRoot" };
            ForgeTheme.Apply(root);
            // The shared header, as every PlayForge asset's inspector opens with
            root.Add(ForgeAssetHeaders.For(this));

            var routes = ForgeDrawerStyles.CreateCollapsibleSection(new ForgeDrawerStyles.SectionConfig
            {
                Name = "RoutesSection",
                Title = "Routes",
                HelpTopic = "cues/routes",
                IncludeButtons = new[] { false, false, true },
            });
            routes.Content.Add(new PropertyField(serializedObject.FindProperty(nameof(CueLibrary.Routes)), "Routes") { name = "Routes" });
            root.Add(routes.Section);

            var raised = ForgeDrawerStyles.CreateCollapsibleSection(new ForgeDrawerStyles.SectionConfig
            {
                Name = "RaisedSection",
                Title = "Raised",
                HelpTopic = "cues/raised",
                IncludeButtons = new[] { false, false, true },
            });
            var list = new VisualElement { name = "Raised" };
            raised.Content.Add(list);
            root.Add(raised.Section);

            void Show()
            {
                if (target == null) return;
                raised.Summary.text = Fill(list, library);
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.projectChanged += Show);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= Show);
            // A route added, removed or given another tag plays the raised tags afresh
            root.TrackSerializedObjectValue(serializedObject, _ => Show());
            Show();
            return root;
        }

        // A row for each cue tag the project's assets raise through the library, in tag order: the tag and the route
        // that plays it, then each asset that raises it. It returns the section's summary: how many tags, and how many
        // play nowhere.
        private static string Fill(VisualElement list, CueLibrary library)
        {
            list.Clear();
            var byTag = new SortedDictionary<string, (Tag Tag, List<ScriptableObject> Assets)>(StringComparer.Ordinal);
            foreach (var asset in ForgeValidator.ProjectAssets())
            {
                // An asset whose project routes its tags through another library raises nothing here
                if (CueTags.LibraryFor(asset) != library) continue;
                foreach (var (cue, _) in CueTags.In(asset))
                {
                    if (!cue.Tag.IsValid) continue;
                    if (!byTag.TryGetValue(cue.Tag.Path, out var entry))
                        byTag[cue.Tag.Path] = entry = (cue.Tag, new List<ScriptableObject>());
                    if (!entry.Assets.Contains(asset)) entry.Assets.Add(asset);
                }
            }

            if (byTag.Count == 0)
            {
                var none = new Label("No asset raises a cue tag through this library.") { name = "NoneRaised" };
                none.AddToClassList("pf-note");
                list.Add(none);
                return "None";
            }

            int nowhere = 0;
            foreach (var (path, (tag, assets)) in byTag)
            {
                var route = ForgeCueRoutes.RouteFor(library, tag);
                if (route == null) nowhere++;
                var row = new VisualElement { name = path };
                row.AddToClassList("pf-cue-raised__row");

                var line = new VisualElement();
                line.AddToClassList("pf-cue-raised__line");
                var name = new Label(tag.ToString()) { tooltip = path };
                name.AddToClassList("pf-cue-raised__tag");
                line.Add(name);
                var plays = new Label(route == null ? "plays nowhere" : route.Tag.Path == path ? "plays its own route" : $"plays {route.Tag}'s route")
                {
                    tooltip = route == null
                        ? "The library routes neither the tag nor a parent of it, so it plays only where an entity's Cue Receiver plays it"
                        : "The route that plays it: its own, or its nearest parent's",
                };
                plays.AddToClassList("pf-cue-raised__route");
                line.Add(plays);
                row.Add(line);

                foreach (var asset in assets.OrderBy(TheForge.NameOf, StringComparer.OrdinalIgnoreCase))
                {
                    var each = asset;
                    var assetName = TheForge.NameOf(each);
                    // A row opens its asset in The Forge, which it selected in the Project window
                    var open = new Button(() => TheForge.OpenToAsset(each)) { name = assetName, tooltip = $"Open {assetName} in The Forge" };
                    open.AddToClassList("pf-used-by__row");
                    open.AddToClassList("pf-cue-raised__asset");
                    open.Add(new ForgeGlyph(ForgeAssetTypes.GlyphOf(each.GetType())));
                    var label = new Label(assetName);
                    label.AddToClassList("pf-used-by__name");
                    open.Add(label);
                    row.Add(open);
                }
                list.Add(row);
            }

            var tags = byTag.Count == 1 ? "1 tag" : $"{byTag.Count} tags";
            return nowhere == 0 ? tags : $"{tags} · {nowhere} {(nowhere == 1 ? "plays" : "play")} nowhere";
        }
    }
}
