using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// What The Forge's summary says of an asset whose inspector has no sections to summarize it: a Scaler asset, a
    /// Requirement Template, and the rules assets, a Value Pipeline, an Effect Modifier and Crowd Control Rules. Each's
    /// summary was empty, and its description could only be read in the box that edits it. The summary says the
    /// description, as text, and a line for each of its parts.
    /// </summary>
    internal static class ForgeSummaries
    {
        /// <summary>An asset's description, as its summary says it first; null where it has none.</summary>
        public static string DescriptionOf(Object asset) => asset switch
        {
            BaseForgeAsset forge => forge.GetDescription(),
            ScalerAsset scaler => scaler.Description,
            RequirementTemplate template => template.Description,
            ValuePipeline pipeline => pipeline.Description,
            EffectModifier modifier => modifier.Description,
            CrowdControlRules rules => rules.Description,
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

                case ValuePipeline pipeline:
                    // In order, each by its label or what it does, as "Armor → Barrier → × 2 on a 10% chance"
                    yield return ("Stages", Listed((pipeline.Stages ?? Enumerable.Empty<AbstractValueStage>())
                        .Where(stage => stage != null)
                        .Select(stage => stage.Enabled ? stage.Name() : $"{stage.Name()} (off)"), " → "));
                    break;

                case EffectModifier modifier:
                    var rule = modifier.Rule;
                    if (rule == null) break;
                    yield return ("Direction", rule.Direction.ToString());
                    // No tags picks every effect
                    yield return ("Tags", rule.EffectTags is { Count: > 0 } ? Tags(rule.EffectTags) : "Every effect");
                    // No condition always holds
                    yield return ("When", rule.When == null ? "Always" : NumberText.Of(rule.When));
                    yield return ("Changes", Listed((rule.Changes ?? Enumerable.Empty<AbstractEffectChange>())
                        .Where(change => change != null)
                        .Select(change => change.Describe()), " · "));
                    break;

                case CrowdControlRules rules:
                    // Each rule's tag and what it stops, as "Stunned: every ability · Silenced: Spell"
                    yield return ("Rules", Listed((rules.Rules ?? Enumerable.Empty<CrowdControlRule>())
                        .Where(each => each != null && each.Tag.IsValid)
                        .Select(each => $"{TheForge.TagText(each.Tag)}: {(each.Stops == ECrowdControlStops.EveryAbility ? "every ability" : Tags(each.AbilityTags))}"), " · "));
                    break;
            }
        }

        /// <summary>
        /// A Components section's line, which The Forge's summary shows: each component's name and what it does, as
        /// "Cast Range: Range 600 · Charges: 3 charges, each recharging on its own"; or None. It said how many there
        /// were.
        /// </summary>
        public static string ComponentsLine(IEnumerable<AbstractForgeComponent> components)
        {
            var parts = (components ?? Enumerable.Empty<AbstractForgeComponent>())
                .Where(component => component != null)
                .Select(component => component.Describe() is { Length: > 0 } line
                    ? $"{AbstractForgeComponent.NameOf(component)}: {line}"
                    : AbstractForgeComponent.NameOf(component))
                .ToList();
            return parts.Count == 0 ? "None" : string.Join(" · ", parts);
        }

        // Tags by their display names, as The Forge shows a tag, or None
        private static string Tags(IEnumerable<TagQuery> queries) =>
            Tags((queries ?? Enumerable.Empty<TagQuery>()).Where(query => query != null).Select(query => query.Tag));

        private static string Tags(IEnumerable<Tag> tags) =>
            Listed((tags ?? Enumerable.Empty<Tag>()).Where(tag => tag.IsValid).Select(TheForge.TagText), ", ");

        // The parts joined, or None where there are none
        private static string Listed(IEnumerable<string> parts, string separator)
        {
            var list = parts.ToList();
            return list.Count == 0 ? "None" : string.Join(separator, list);
        }
    }
}
