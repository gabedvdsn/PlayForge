using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// The contexts a tag is offered in: one per kind of tag, what a tag is rather than where it's used (TAG-6, Decision
    /// 74). A tag field names the kinds it takes with [ForgeTagContext], and its picker offers the tags of those kinds: those
    /// assets use in such a field, those a Tag Library declares for them, and those code exposes with [ForgeTagExposed].
    ///
    /// Usage:
    /// [ForgeTagContext(ForgeContext.Granted)]
    /// public Tag Stunned;
    ///
    /// Each constant is its value without its spaces. A project's own contexts, made in a contexts field's New Context…,
    /// stand beside these.
    /// </summary>
    public static class ForgeContext
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // What an asset is
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>An asset's own tag, which names it</summary>
        public const string AssetTag = "Asset Tag";

        /// <summary>An asset's context tags, which group it with others of its sort, as Spell or Fire</summary>
        public const string ContextTag = "Context Tag";

        /// <summary>A Requirement Template's category (ED-79)</summary>
        public const string RequirementCategory = "Requirement Category";

        // ═══════════════════════════════════════════════════════════════════════════
        // What an entity carries
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A tag an entity carries while something grants it, as Stunned, which queries and requirements ask of it</summary>
        public const string Granted = "Granted";

        /// <summary>A side an entity is on</summary>
        public const string Affiliation = "Affiliation";

        /// <summary>Whether an effect or an item shows</summary>
        public const string Visibility = "Visibility";

        // ═══════════════════════════════════════════════════════════════════════════
        // What happens
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The kind of an impact, as Returned, which impact filters and workers take</summary>
        public const string ImpactType = "Impact Type";

        /// <summary>A retention group, under which an attribute keeps the impacts it took</summary>
        public const string RetentionGroup = "Retention Group";

        /// <summary>A gameplay event, as Impact Taken or Died, which triggers, thresholds and the combat log read</summary>
        public const string Event = "Event";

        /// <summary>A part an attribute plays in a project, as Health (TW-29)</summary>
        public const string AttributeRole = "Attribute Role";

        /// <summary>A texture item's slot</summary>
        public const string TextureSlot = "Texture Slot";

        // ═══════════════════════════════════════════════════════════════════════════
        // Keys
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A key an ability's data holds a value under, as the activation's target or what a Set Assets task loads</summary>
        public const string DataKey = "Data Key";

        /// <summary>A key an asset's Local Data holds a value under</summary>
        public const string LocalDataKey = "Local Data Key";

        /// <summary>A key a worker answers a number or an asset under</summary>
        public const string WorkerKey = "Worker Key";

        /// <summary>A key a value is set by its caller under: a set-by-caller magnitude's, or a process attribute's</summary>
        public const string SetByCallerKey = "Set By Caller Key";

        /// <summary>A flag a stage records in the activation's data, which a later stage's condition reads</summary>
        public const string StageFlag = "Stage Flag";

        // ═══════════════════════════════════════════════════════════════════════════
        // Special
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Tags written in code and exposed to the editor with [ForgeTagExposed] that name no context of their own, a
        /// project's as PlayForge's (TAG-5)
        /// </summary>
        public const string Code = "Code";

        /// <summary>Tags every picker offers: those a Tag Library declares for no context</summary>
        public const string Universal = "Universal";

        /// <summary>
        /// The contexts TAG-6 renamed, merged or dropped, by their old values, and what each reads as now: Data, which held
        /// four kinds of key, reads as each of them, and one dropped as none (Decision 74). No context of a project's own
        /// can take an old name, so an old name always reads so.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Renamed = new Dictionary<string, string[]>
        {
            ["Asset Identifier"] = new[] { AssetTag },
            ["Context Identifier"] = new[] { ContextTag },
            ["Category"] = new[] { ContextTag },
            ["Required"] = new[] { Granted },
            ["Condition"] = new[] { Granted },
            ["Blocked"] = new[] { Granted },
            ["System"] = new[] { Affiliation },
            ["Targeting"] = new[] { Visibility },
            ["Impact"] = new[] { ImpactType },
            ["Impact Retention"] = new[] { RetentionGroup },
            ["Trigger"] = new[] { Event },
            ["Role"] = new[] { AttributeRole },
            ["Texture"] = new[] { TextureSlot },
            ["Data"] = new[] { DataKey, LocalDataKey, WorkerKey, SetByCallerKey },
            ["Worker"] = new[] { WorkerKey },
            ["Stage"] = new[] { StageFlag },
            ["Ability"] = new string[0],
            ["Effect"] = new string[0],
            ["Entity"] = new string[0],
            ["Attribute"] = new string[0],
            ["AttributeSet"] = new string[0],
            ["Modifier"] = new string[0],
            ["Cost"] = new string[0],
            ["Cooldown"] = new string[0],
            ["Duration"] = new string[0],
            ["Debug"] = new string[0],
        };

        /// <summary>Contexts as they read now: an old one as its new, one dropped gone, any other as it is, each once (TAG-6)</summary>
        public static string[] Current(IEnumerable<string> contexts) =>
            (contexts ?? Enumerable.Empty<string>())
            .Where(context => !string.IsNullOrWhiteSpace(context))
            .SelectMany(context => Renamed.TryGetValue(context, out var now) ? now : new[] { context })
            .Distinct()
            .ToArray();
    }
}
