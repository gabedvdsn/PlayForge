using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What The Forge's summary says of an asset whose inspector has no sections to summarize it: a Scaler asset and a
    /// Requirement Template. Each's summary was empty, and its description could only be read in the box that edits it
    /// (the user's notes, ED-74, ED-78). The summary says the description, as text, and a line for each of its parts.
    /// </summary>
    internal static class ForgeSummaries
    {
        /// <summary>An asset's description, as its summary says it first; null where it has none.</summary>
        public static string DescriptionOf(Object asset) => asset switch
        {
            BaseForgeAsset forge => forge.GetDescription(),
            ScalerAsset scaler => scaler.Description,
            RequirementTemplate template => template.Description,
            _ => null,
        };

        /// <summary>The summary's lines for an asset whose inspector has no sections, each a label and its values.</summary>
        public static IEnumerable<(string Label, string Line)> LinesOf(Object asset)
        {
            switch (asset)
            {
                case ScalerAsset scaler:
                    var kind = scaler.Scaler;
                    yield return ("Kind", kind == null ? "None" : NumberText.KindName(kind));
                    if (kind != null) yield return ("Reads", NumberText.Describe(kind));
                    int uses = ScalerFields.UsesOf(scaler).Count;
                    yield return ("Used By", uses == 0 ? "None" : uses == 1 ? "1 field" : $"{uses} fields");
                    break;

                case RequirementTemplate template:
                    var group = template.Requirements;
                    yield return ("Requires", Tags(group?.RequireTags));
                    yield return ("Must Not Have", Tags(group?.AvoidTags));
                    break;
            }
        }

        // Tags by their display names, as The Forge shows a tag, or None
        private static string Tags(IEnumerable<TagQuery> queries)
        {
            var names = (queries ?? Enumerable.Empty<TagQuery>()).Where(query => query != null && query.Tag.IsValid).Select(query => TheForge.TagText(query.Tag)).ToList();
            return names.Count == 0 ? "None" : string.Join(", ", names);
        }
    }
}
