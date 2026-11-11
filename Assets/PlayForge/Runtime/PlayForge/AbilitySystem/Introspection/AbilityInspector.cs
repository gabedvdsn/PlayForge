using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // ABILITY INTROSPECTION
    //
    // Static (no live GAS) analysis of what an Ability actually DOES: which effects it
    // applies, in what role (stage task / cost / cooldown / contained), to whom
    // (caster vs acquired target, affiliation policy), and with what numbers ACROSS
    // LEVELS — magnitudes, durations and tick counts evaluated per level through the
    // real scaler pipeline via synthetic LevelerEffectOrigin carriers.
    //
    // Intended consumers: tooltips ("Deals 20/30/40/50 damage over 3/4/5/6 s"),
    // the Forge analysis tab (power methodology), and debugging.
    //
    // Scalers that depend on runtime state (captured attributes, conditions) are
    // evaluated best-effort and flagged IsDynamic so UI can annotate ("+10% of STR").
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>How an effect participates in an ability.</summary>
    public enum EAbilityEffectRole
    {
        /// <summary>Applied by a stage task (ApplyEffectsAbilityTask or equivalent).</summary>
        StageApplication,
        /// <summary>Applied on stage entry and removed on stage exit (ApplyRemoveEffectsAbilityTask).</summary>
        StageApplicationWithRemoval,
        /// <summary>The ability's usage cost.</summary>
        Cost,
        /// <summary>The ability's cooldown.</summary>
        Cooldown,
        /// <summary>Contained effect fired when its parent applies.</summary>
        ContainedOnApply,
        /// <summary>Contained effect fired per parent tick.</summary>
        ContainedOnTick,
        /// <summary>Contained effect fired when the parent is removed.</summary>
        ContainedOnRemove,
        /// <summary>
        /// Applied by a code-driven task sequence that declared it via
        /// <c>TaskSequenceBuilder.WithAppliedEffects</c> (loaded into the data packet by
        /// an asset loader, applied inside the sequence body).
        /// </summary>
        SequenceApplication,
        /// <summary>
        /// Loaded into the data packet by an asset loader but not claimed by any
        /// sequence annotation — almost certainly applied somewhere at runtime; annotate
        /// the sequence with WithAppliedEffects to upgrade fidelity.
        /// </summary>
        LoadedAsset,
        /// <summary>Referenced by a custom task (found via reflection) — application manner unknown.</summary>
        Referenced,
    }

    /// <summary>An asset-loader task and the effects it loads into the data packet.</summary>
    public class AssetLoaderInfo
    {
        public string TaskType;
        /// <summary>The data-packet key the assets are loaded under (invalid when unknown).</summary>
        public Tag DataTag;
        public List<GameplayEffect> Effects = new();
    }

    /// <summary>A code-driven sequence referenced by a RunSequenceTask, with its declared behavior.</summary>
    public class SequenceInfo
    {
        public string Name;
        public string Description;
        public int StageIndex;
        public List<SequenceAnnotation> Annotations = new();
    }

    /// <summary>Who an effect application lands on, as statically determinable.</summary>
    public enum EEffectTargetScope
    {
        Caster,
        AcquiredTarget,
        Unknown,
    }

    /// <summary>
    /// Per-level evaluation of one impact entry: the magnitude the entry contributes at
    /// each level, and the total delivered over the effect's lifetime (ticks × magnitude
    /// for Periodic delivery).
    /// </summary>
    public class ImpactSeries
    {
        public Attribute Attribute;
        public EImpactDelivery Delivery;
        public ECalculationOperation Operation;
        public EAttributeTarget Slot;

        /// <summary>Per-application (or per-tick, or modifier) magnitude at each level.</summary>
        public float[] Magnitude;

        /// <summary>
        /// Total delivered per level: Periodic → magnitude × executed ticks
        /// (+1 application pulse when TickOnApplication); otherwise == Magnitude.
        /// For Continuous entries this is the modifier's live value, not a delivered sum.
        /// </summary>
        public float[] Total;

        /// <summary>True when the magnitude depends on runtime state (captured attributes, conditions, randomness).</summary>
        public bool IsDynamic;
        public string DynamicReason;

        /// <summary>The impact's number as its field's summary reads it, as "−0.1 × Target's Health (base)" (FEAT-32).</summary>
        public string Line;

        /// <summary>
        /// The number's read at each level, explained: each part, what it read and what it gave (FEAT-32). Set by the
        /// read against a caster and a target, <see cref="AbilityInspector.BuildEffectSeries(GameplayEffect, int, int, ISource, ITarget)"/>.
        /// </summary>
        public NumberTrace[] Parts;

        /// <summary>What the number reads that the read wasn't given, as "the target"; null where it had all it reads (FEAT-32).</summary>
        public string Needs;

        public string FormatMagnitude(string format = "0.##") => FormatSeries(Magnitude, format);
        public string FormatTotal(string format = "0.##") => FormatSeries(Total, format);

        /// <summary>"20/30/40/50" — collapses to a single value when constant across levels.</summary>
        public static string FormatSeries(float[] values, string format = "0.##")
        {
            if (values == null || values.Length == 0) return "0";
            if (values.All(v => Mathf.Approximately(v, values[0]))) return values[0].ToString(format);
            return string.Join("/", values.Select(v => v.ToString(format)));
        }
    }

    /// <summary>One effect evaluated across a level range: duration, ticks, and impacts.</summary>
    public class EffectLevelSeries
    {
        public GameplayEffect Effect;
        public int MinLevel;
        public int MaxLevel;
        public int LevelCount => MaxLevel - MinLevel + 1;

        public EEffectDurationPolicy DurationPolicy;
        public bool TickOnApplication;
        public bool PeriodicTicks;

        /// <summary>Total duration per level (Durational only; 0 for Instant, +∞ semantics for Infinite).</summary>
        public float[] Duration;

        /// <summary>Executed periodic ticks per level (Durational + periodic; 0 otherwise).</summary>
        public int[] Ticks;

        public List<ImpactSeries> Impacts = new();
        public string FormatDuration(string format = "0.##") => ImpactSeries.FormatSeries(Duration, format);

        /// <summary>Its chance to apply at each level, 1 where it always does (FEAT-32).</summary>
        public float[] Chance;

        /// <summary>The time between its ticks at each level, for an infinite effect that ticks (FEAT-32).</summary>
        public float[] Interval;

        /// <summary>The effects it contains, each read the same way, with when it lands (FEAT-32).</summary>
        public List<ContainedSeries> Contained = new();

        /// <summary>
        /// What the effect does at a level, an impact to a line, then its contained effects (FEAT-32): "−15 Health each
        /// tick, 12 ticks over 12 s", "+20 Armor while it lasts, 5 s", "and Crit as it lands, 15% of the time: −225 Health".
        /// A number that reads what the read wasn't given says so: "(needs the target)".
        /// </summary>
        public List<string> Describe(int level)
        {
            var lines = new List<string>();
            int i = Mathf.Clamp(level - MinLevel, 0, LevelCount - 1);
            foreach (var impact in Impacts) lines.Add(Line(impact, i));
            foreach (var contained in Contained)
            {
                if (contained.Series == null) continue;
                string when = contained.Policy switch
                {
                    EApplyTickRemove.OnApply => "as it lands",
                    EApplyTickRemove.OnTick => "each tick",
                    _ => "as it ends",
                };
                float chance = contained.Series.Chance?[Mathf.Clamp(level - contained.Series.MinLevel, 0, contained.Series.LevelCount - 1)] ?? 1f;
                string odds = chance < 1f ? $", {NumberText.Number(chance, percent: true)} of the time" : "";
                var inner = contained.Series.Describe(level);
                lines.Add($"and {contained.Series.Effect?.GetName()} {when}{odds}{(inner.Count > 0 ? ": " + string.Join("; ", inner) : "")}");
            }
            return lines;
        }

        private string Line(ImpactSeries impact, int i)
        {
            string attribute = impact.Attribute != null ? impact.Attribute.GetName() : "nothing";
            float value = impact.Magnitude != null && i < impact.Magnitude.Length ? impact.Magnitude[i] : 0f;
            string amount = impact.Operation switch
            {
                ECalculationOperation.Multiply => $"× {NumberText.Number(value)}",
                ECalculationOperation.Override => $"= {NumberText.Number(value)}",
                _ => value >= 0f ? $"+{NumberText.Number(value)}" : NumberText.Number(value),
            };
            string needs = string.IsNullOrEmpty(impact.Needs) ? "" : $" (needs {impact.Needs})";
            switch (impact.Delivery)
            {
                case EImpactDelivery.Periodic when DurationPolicy == EEffectDurationPolicy.Durational:
                    return $"{amount} {attribute} each tick, {Ticks[i]} ticks over {NumberText.Number(Duration[i])} s{needs}";
                case EImpactDelivery.Periodic:
                    return $"{amount} {attribute} every {NumberText.Number(Interval != null ? Interval[i] : 1f)} s{needs}";
                case EImpactDelivery.Continuous when DurationPolicy == EEffectDurationPolicy.Durational:
                    return $"{amount} {attribute} while it lasts, {NumberText.Number(Duration[i])} s{needs}";
                case EImpactDelivery.Continuous:
                    return $"{amount} {attribute} while it lasts{needs}";
                default:
                    return $"{amount} {attribute}{needs}";
            }
        }
    }

    /// <summary>An effect another contains, read level by level, with when it lands (FEAT-32).</summary>
    public class ContainedSeries
    {
        public EApplyTickRemove Policy;
        public EffectLevelSeries Series;
    }

    /// <summary>One effect's participation in an ability, with per-level numbers and contained effects.</summary>
    public class AbilityEffectUsage
    {
        public GameplayEffect Effect;
        public EAbilityEffectRole Role;

        /// <summary>Behaviour stage index (-1 for cost/cooldown/targeting-referenced).</summary>
        public int StageIndex = -1;

        /// <summary>Concrete task type that applies the effect (null for cost/cooldown).</summary>
        public string TaskType;

        public EEffectTargetScope Scope = EEffectTargetScope.Unknown;

        /// <summary>The effect's own affiliation gate (enemies/allies/anyone).</summary>
        public EAffiliationPolicy Affiliation;

        /// <summary>
        /// Upper bound on applications per activation (from a sequence annotation —
        /// e.g. Chain Lightning applies its damage effect up to 4 times). 1 otherwise.
        /// </summary>
        public int ApplicationsHint = 1;

        /// <summary>Author note from the sequence annotation ("per enemy struck").</summary>
        public string Annotation;

        public EffectLevelSeries Series;

        public List<AbilityEffectUsage> Contained = new();

        public bool IsUsageEffect => Role is EAbilityEffectRole.Cost or EAbilityEffectRole.Cooldown;
    }

    /// <summary>The full static description of an ability.</summary>
    public class AbilityBreakdown
    {
        public Ability Ability;
        public int MinLevel;
        public int MaxLevel;
        public int LevelCount => MaxLevel - MinLevel + 1;

        /// <summary>Type name of the targeting task, or null when the ability self-targets implicitly.</summary>
        public string TargetingTaskType;
        /// <summary>The targeting task's authored Description, when it provides one.</summary>
        public string TargetingDescription;
        public bool HasImplicitSelfTarget;

        /// <summary>Asset-loader tasks and what they load into the data packet.</summary>
        public List<AssetLoaderInfo> AssetLoaders = new();

        /// <summary>Code-driven sequences (RunSequenceTask) and their declared behavior.</summary>
        public List<SequenceInfo> Sequences = new();

        /// <summary>Everything the ability applies, stage-ordered; cost/cooldown last.</summary>
        public List<AbilityEffectUsage> Usages = new();

        public IEnumerable<AbilityEffectUsage> GameplayUsages => Usages.Where(u => !u.IsUsageEffect);
        public AbilityEffectUsage Cost => Usages.FirstOrDefault(u => u.Role == EAbilityEffectRole.Cost);
        public AbilityEffectUsage Cooldown => Usages.FirstOrDefault(u => u.Role == EAbilityEffectRole.Cooldown);

        /// <summary>
        /// Human-readable summary lines for a tooltip at one level (uses the series so
        /// callers wanting the "20/30/40/50" across-level form can format directly).
        /// </summary>
        public List<string> Describe(int level)
        {
            var lines = new List<string>();

            if (!string.IsNullOrEmpty(TargetingDescription))
            {
                lines.Add($"Targeting: {TargetingDescription}");
            }
            else if (TargetingTaskType != null)
            {
                lines.Add($"Targeting: {TargetingTaskType}");
            }

            foreach (var sequence in Sequences)
            {
                if (!string.IsNullOrEmpty(sequence.Description)) lines.Add(sequence.Description);
                foreach (var annotation in sequence.Annotations)
                {
                    if (!annotation.AppliesLoadedEffects && !string.IsNullOrEmpty(annotation.Text))
                    {
                        lines.Add(annotation.Text);
                    }
                }
            }

            foreach (var usage in GameplayUsages) DescribeUsage(usage, level, lines, depth: 0);

            var cd = Cooldown?.Series;
            if (cd?.Duration != null) lines.Add($"Cooldown: {ValueAt(cd.Duration, cd.MinLevel, level):0.##}s");

            var cost = Cost;
            if (cost?.Series != null)
            {
                foreach (var impact in cost.Series.Impacts)
                {
                    lines.Add($"Cost: {Mathf.Abs(ValueAt(impact.Total, cost.Series.MinLevel, level)):0.##} {impact.Attribute?.GetName()}");
                }
            }

            return lines;
        }

        private static void DescribeUsage(AbilityEffectUsage usage, int level, List<string> lines, int depth)
        {
            string indent = depth > 0 ? new string(' ', depth * 2) + "↳ " : "";
            string target = usage.Scope switch
            {
                EEffectTargetScope.Caster => "self",
                EEffectTargetScope.AcquiredTarget => "target",
                _ => "?",
            };

            string suffix = usage.ApplicationsHint > 1 ? $" ×{usage.ApplicationsHint}" : "";
            if (!string.IsNullOrEmpty(usage.Annotation)) suffix += $" ({usage.Annotation})";

            var series = usage.Series;
            if (series == null || series.Impacts.Count == 0)
            {
                lines.Add($"{indent}Applies {usage.Effect?.GetName()} to {target}{suffix}");
            }
            else
            {
                foreach (var impact in series.Impacts)
                {
                    float total = ValueAt(impact.Total, series.MinLevel, level);
                    string attr = impact.Attribute != null ? impact.Attribute.GetName() : "?";
                    string dyn = impact.IsDynamic ? "*" : "";

                    string line = impact.Delivery switch
                    {
                        EImpactDelivery.Continuous when impact.Operation == ECalculationOperation.Multiply =>
                            $"{indent}{FormatPercent(total)} {attr}{dyn} to {target} while active{suffix}",
                        EImpactDelivery.Continuous =>
                            $"{indent}{Signed(total)} {attr}{dyn} to {target} while active{suffix}",
                        EImpactDelivery.Periodic =>
                            $"{indent}{VerbFor(total)} {Mathf.Abs(total):0.##} {attr}{dyn} to {target} over {ValueAt(series.Duration, series.MinLevel, level):0.##}s{suffix}",
                        _ =>
                            $"{indent}{VerbFor(total)} {Mathf.Abs(total):0.##} {attr}{dyn} to {target}{suffix}",
                    };
                    lines.Add(line);
                }
            }

            foreach (var contained in usage.Contained) DescribeUsage(contained, level, lines, depth + 1);
        }

        private static float ValueAt(float[] series, int minLevel, int level)
        {
            if (series == null || series.Length == 0) return 0f;
            return series[Mathf.Clamp(level - minLevel, 0, series.Length - 1)];
        }

        private static string VerbFor(float value) => value < 0 ? "Deals" : "Restores";
        private static string Signed(float value) => value >= 0 ? $"+{value:0.##}" : value.ToString("0.##");
        private static string FormatPercent(float multiplier)
        {
            float pct = (multiplier - 1f) * 100f;
            return pct >= 0 ? $"+{pct:0.#}%" : $"{pct:0.#}%";
        }
    }

    public static class AbilityInspector
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // ENTRY POINTS
        // ═══════════════════════════════════════════════════════════════════════════

        public static AbilityBreakdown Build(Ability ability)
        {
            if (ability == null) return null;

            int minLevel = Mathf.Max(1, ability.GetLinkedStartingLevel());
            int maxLevel = Mathf.Max(minLevel, ability.GetLinkedMaxLevel());

            var breakdown = new AbilityBreakdown
            {
                Ability = ability,
                MinLevel = minLevel,
                MaxLevel = maxLevel,
                TargetingTaskType = ability.Behaviour?.Targeting?.GetType().Name,
                TargetingDescription = ability.Behaviour?.Targeting?.Description,
                HasImplicitSelfTarget = ability.Behaviour?.UseImplicitTargeting ?? false,
            };

            // With no targeting task the only target is the caster (implicit targeting);
            // with one, stage tasks act on whatever it acquired.
            var defaultScope = breakdown.TargetingTaskType == null
                ? (breakdown.HasImplicitSelfTarget ? EEffectTargetScope.Caster : EEffectTargetScope.Unknown)
                : EEffectTargetScope.AcquiredTarget;

            var visited = new HashSet<GameplayEffect>();

            // ─── Asset loaders — what gets loaded into the data packet, per tag ─────
            // Sequences apply these via data.TryApplyEffects(target, tag); sequence
            // annotations (WithAppliedEffects) claim them by tag below.
            var loadedByTag = new Dictionary<Tag, List<GameplayEffect>>();
            var claimedLoadedEffects = new HashSet<GameplayEffect>();

            if (ability.Behaviour?.AssetLoaders != null)
            {
                foreach (var loader in ability.Behaviour.AssetLoaders)
                {
                    if (loader == null) continue;

                    var info = new AssetLoaderInfo { TaskType = loader.GetType().Name };
                    switch (loader)
                    {
                        case SetEffectsTask setEffects:
                            info.DataTag = setEffects.Tag;
                            if (setEffects.Effects != null) info.Effects.AddRange(setEffects.Effects.Where(e => e != null));
                            break;
                        case SetAssetsTask setAssets:
                            info.DataTag = setAssets.Tag;
                            if (setAssets.Assets != null) info.Effects.AddRange(setAssets.Assets.OfType<GameplayEffect>());
                            break;
                        default:
                            info.Effects.AddRange(FindEffectReferences(loader));
                            break;
                    }

                    if (info.Effects.Count > 0 && info.DataTag.IsValid)
                    {
                        if (!loadedByTag.TryGetValue(info.DataTag, out var list))
                        {
                            loadedByTag[info.DataTag] = list = new List<GameplayEffect>();
                        }
                        list.AddRange(info.Effects);
                    }

                    breakdown.AssetLoaders.Add(info);
                }
            }

            if (ability.Behaviour?.Stages != null)
            {
                for (int stageIndex = 0; stageIndex < ability.Behaviour.Stages.Count; stageIndex++)
                {
                    var stage = ability.Behaviour.Stages[stageIndex];
                    if (stage?.Tasks == null) continue;

                    foreach (var task in stage.Tasks)
                    {
                        if (task == null) continue;
                        CollectTaskUsages(task, stageIndex, defaultScope, breakdown, minLevel, maxLevel, visited, loadedByTag, claimedLoadedEffects);
                    }
                }
            }

            // Loaded effects no sequence annotation claimed — surface them anyway so
            // nothing an ability can apply is invisible; annotating the sequence
            // upgrades these to SequenceApplication with counts and scope.
            foreach (var loaderInfo in breakdown.AssetLoaders)
            {
                foreach (var effect in loaderInfo.Effects)
                {
                    if (claimedLoadedEffects.Contains(effect)) continue;
                    if (breakdown.Usages.Any(u => u.Effect == effect)) continue;

                    AddUsage(breakdown, effect, EAbilityEffectRole.LoadedAsset, -1,
                        loaderInfo.TaskType, defaultScope, minLevel, maxLevel, visited);
                }
            }

            // Effects referenced by the targeting task or asset loaders (manner unknown).
            if (ability.Behaviour?.Targeting != null)
            {
                foreach (var effect in FindEffectReferences(ability.Behaviour.Targeting))
                {
                    AddUsage(breakdown, effect, EAbilityEffectRole.Referenced, -1,
                        ability.Behaviour.Targeting.GetType().Name, defaultScope, minLevel, maxLevel, visited);
                }
            }

            if (ability.Cost != null)
            {
                AddUsage(breakdown, ability.Cost, EAbilityEffectRole.Cost, -1, null,
                    EEffectTargetScope.Caster, minLevel, maxLevel, visited);
            }

            if (ability.Cooldown != null)
            {
                AddUsage(breakdown, ability.Cooldown, EAbilityEffectRole.Cooldown, -1, null,
                    EEffectTargetScope.Caster, minLevel, maxLevel, visited);
            }

            return breakdown;
        }

        /// <summary>Evaluate one effect across a level range (durations, ticks, per-entry magnitudes).</summary>
        public static EffectLevelSeries BuildEffectSeries(GameplayEffect effect, int minLevel, int maxLevel)
        {
            if (effect == null) return null;

            minLevel = Mathf.Max(1, minLevel);
            maxLevel = Mathf.Max(minLevel, maxLevel);
            int count = maxLevel - minLevel + 1;

            var duration = effect.DurationSpecification;
            effect.ImpactSpecification?.RebuildEntries(duration);

            var series = new EffectLevelSeries
            {
                Effect = effect,
                MinLevel = minLevel,
                MaxLevel = maxLevel,
                DurationPolicy = duration?.DurationPolicy ?? EEffectDurationPolicy.Instant,
                TickOnApplication = duration?.TickOnApplication ?? false,
                PeriodicTicks = duration?.EnablePeriodicTicks ?? false,
                Duration = new float[count],
                Ticks = new int[count],
            };

            for (int i = 0; i < count; i++)
            {
                var carrier = MakeLevelCarrier(minLevel + i, minLevel, maxLevel);

                if (series.DurationPolicy == EEffectDurationPolicy.Durational && duration != null)
                {
                    series.Duration[i] = SafeEvaluate(duration.DurationOperation, carrier, out _);
                    series.Ticks[i] = series.PeriodicTicks ? SafeEvaluateInt(duration.TicksOperation, carrier, out _) : 0;
                }
            }

            if (effect.ImpactSpecification != null)
            {
                foreach (var entry in effect.ImpactSpecification.Entries)
                {
                    if (entry == null) continue;

                    var impact = new ImpactSeries
                    {
                        Attribute = entry.AttributeTarget,
                        Delivery = entry.Delivery,
                        Operation = entry.Operation,
                        Slot = entry.TargetSlot,
                        Magnitude = new float[count],
                        Total = new float[count],
                    };

                    for (int i = 0; i < count; i++)
                    {
                        var carrier = MakeLevelCarrier(minLevel + i, minLevel, maxLevel);
                        float magnitude = SafeEvaluate(entry.Magnitude, carrier, out bool dynamic, out string reason);
                        if (dynamic && !impact.IsDynamic)
                        {
                            impact.IsDynamic = true;
                            impact.DynamicReason = reason;
                        }

                        impact.Magnitude[i] = magnitude;
                        impact.Total[i] = entry.Delivery == EImpactDelivery.Periodic
                            ? magnitude * (series.Ticks[i] + (series.TickOnApplication ? 1 : 0))
                            : magnitude;
                    }

                    series.Impacts.Add(impact);
                }
            }

            return series;
        }

        /// <summary>
        /// One effect across a level range, read against a caster and a target, either of which may be null (FEAT-32):
        /// its duration, ticks, tick interval and chance at each level; each impact's number at each level, with its line,
        /// its parts explained (<see cref="NumberTrace"/>), and what it needs that it wasn't given, as "the target"; and its
        /// contained effects, each read the same way. A number that reads the target, read with none, gives what its field
        /// falls back to, and says so.
        /// </summary>
        public static EffectLevelSeries BuildEffectSeries(GameplayEffect effect, int minLevel, int maxLevel, ISource caster, ITarget target) =>
            BuildEffectSeries(effect, minLevel, maxLevel, caster, target, 0);

        // How deep contained effects are read, past which a loop is assumed
        private const int MostContainedDepth = 4;

        private static EffectLevelSeries BuildEffectSeries(GameplayEffect effect, int minLevel, int maxLevel, ISource caster, ITarget target, int depth)
        {
            if (effect == null) return null;

            minLevel = Mathf.Max(1, minLevel);
            maxLevel = Mathf.Max(minLevel, maxLevel);
            int count = maxLevel - minLevel + 1;

            var duration = effect.DurationSpecification;
            effect.ImpactSpecification?.RebuildEntries(duration);

            var series = new EffectLevelSeries
            {
                Effect = effect,
                MinLevel = minLevel,
                MaxLevel = maxLevel,
                DurationPolicy = duration?.DurationPolicy ?? EEffectDurationPolicy.Instant,
                TickOnApplication = duration?.TickOnApplication ?? false,
                PeriodicTicks = duration?.EnablePeriodicTicks ?? false,
                Duration = new float[count],
                Ticks = new int[count],
                Interval = new float[count],
                Chance = new float[count],
            };

            for (int i = 0; i < count; i++)
            {
                var context = NumberContext.For(caster, target, minLevel + i, maxLevel);
                if (duration != null)
                {
                    if (series.DurationPolicy == EEffectDurationPolicy.Durational)
                    {
                        series.Duration[i] = NumberTrace.Of(duration.DurationOperation, context).Value;
                        series.Ticks[i] = series.PeriodicTicks ? Mathf.RoundToInt(NumberTrace.Of(duration.TicksOperation, context).Value) : 0;
                    }
                    if (series.PeriodicTicks) series.Interval[i] = NumberTrace.Of(duration.TickIntervalOperation, context).Value;
                }
                var chance = effect.ImpactSpecification?.ChanceToApply;
                series.Chance[i] = chance == null ? 1f : NumberTrace.Of(chance, context).Value;
            }

            if (effect.ImpactSpecification != null)
            {
                foreach (var entry in effect.ImpactSpecification.Entries)
                {
                    if (entry == null) continue;

                    var impact = new ImpactSeries
                    {
                        Attribute = entry.AttributeTarget,
                        Delivery = entry.Delivery,
                        Operation = entry.Operation,
                        Slot = entry.TargetSlot,
                        Magnitude = new float[count],
                        Total = new float[count],
                        Parts = new NumberTrace[count],
                        Line = NumberText.Of(entry.Magnitude),
                    };

                    var needs = new List<string>();
                    for (int i = 0; i < count; i++)
                    {
                        var trace = NumberTrace.Of(entry.Magnitude, NumberContext.For(caster, target, minLevel + i, maxLevel));
                        impact.Parts[i] = trace;
                        impact.Magnitude[i] = trace.Value;
                        impact.Total[i] = entry.Delivery == EImpactDelivery.Periodic
                            ? trace.Value * (series.Ticks[i] + (series.TickOnApplication ? 1 : 0))
                            : trace.Value;
                        foreach (var part in trace.Unresolved)
                            if (!needs.Contains(part.Missing)) needs.Add(part.Missing);
                    }
                    if (needs.Count > 0)
                    {
                        impact.Needs = NumberText.Join(needs);
                        impact.IsDynamic = true;
                        impact.DynamicReason = $"needs {impact.Needs}";
                    }

                    series.Impacts.Add(impact);
                }

                if (depth < MostContainedDepth && effect.ImpactSpecification.Packets != null)
                {
                    foreach (var packet in effect.ImpactSpecification.Packets)
                    {
                        if (packet.ContainedEffect == null) continue;
                        series.Contained.Add(new ContainedSeries
                        {
                            Policy = packet.Policy,
                            Series = BuildEffectSeries(packet.ContainedEffect, minLevel, maxLevel, caster, target, depth + 1),
                        });
                    }
                }
            }

            return series;
        }

        /// <summary>
        /// Evaluate a magnitude operation at an arbitrary level through the real scaler
        /// pipeline, using a synthetic leveler carrier. <paramref name="isDynamic"/> is
        /// true when the result depends on runtime state.
        /// </summary>
        public static float EvaluateAtLevel(ScalableFloat operation, int level, int minLevel, int maxLevel, out bool isDynamic)
        {
            var carrier = MakeLevelCarrier(level, minLevel, maxLevel);
            return SafeEvaluate(operation, carrier, out isDynamic);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // USAGE COLLECTION
        // ═══════════════════════════════════════════════════════════════════════════

        private static void CollectTaskUsages(AbstractAbilityTask task, int stageIndex, EEffectTargetScope defaultScope,
            AbilityBreakdown breakdown, int minLevel, int maxLevel, HashSet<GameplayEffect> visited,
            Dictionary<Tag, List<GameplayEffect>> loadedByTag = null, HashSet<GameplayEffect> claimedLoadedEffects = null)
        {
            string taskType = task.GetType().Name;

            switch (task)
            {
                case ApplyEffectsAbilityTask apply when apply.Effects != null:
                    foreach (var effect in apply.Effects)
                    {
                        AddUsage(breakdown, effect, EAbilityEffectRole.StageApplication, stageIndex, taskType, defaultScope, minLevel, maxLevel, visited);
                    }
                    break;

                case ApplyRemoveEffectsAbilityTask applyRemove when applyRemove.Effects != null:
                    foreach (var effect in applyRemove.Effects)
                    {
                        AddUsage(breakdown, effect, EAbilityEffectRole.StageApplicationWithRemoval, stageIndex, taskType, defaultScope, minLevel, maxLevel, visited);
                    }
                    break;

                case RunSequenceTask runSequence:
                    CollectSequenceUsages(runSequence, stageIndex, defaultScope, breakdown, minLevel, maxLevel, visited, loadedByTag, claimedLoadedEffects);
                    break;

                default:
                    // Custom tasks: find effect references via reflection; the application
                    // manner can't be known statically, so they land as Referenced.
                    foreach (var effect in FindEffectReferences(task))
                    {
                        AddUsage(breakdown, effect, EAbilityEffectRole.Referenced, stageIndex, taskType, defaultScope, minLevel, maxLevel, visited);
                    }
                    break;
            }
        }

        /// <summary>
        /// Resolve a RunSequenceTask's sequence (edit-time safe: the builder method only
        /// constructs the definition — runtime lambdas are not invoked) and turn its
        /// declared annotations into usages. WithAppliedEffects annotations claim the
        /// effects loaded under their tag by the ability's asset loaders.
        /// </summary>
        private static void CollectSequenceUsages(RunSequenceTask runSequence, int stageIndex, EEffectTargetScope defaultScope,
            AbilityBreakdown breakdown, int minLevel, int maxLevel, HashSet<GameplayEffect> visited,
            Dictionary<Tag, List<GameplayEffect>> loadedByTag, HashSet<GameplayEffect> claimedLoadedEffects)
        {
            TaskSequence sequence = null;
            try
            {
                sequence = runSequence.Sequence.GetActiveSequence() as TaskSequence;
            }
            catch (Exception)
            {
                // Builder method threw (missing scene context etc.) — fall through to
                // the name-only info below.
            }

            var metadata = sequence?.Definition?.Metadata;
            var info = new SequenceInfo
            {
                Name = metadata?.Name ?? runSequence.Sequence.GetDisplayName(),
                Description = metadata?.Description,
                StageIndex = stageIndex,
            };
            if (metadata?.Annotations != null) info.Annotations.AddRange(metadata.Annotations);
            breakdown.Sequences.Add(info);

            foreach (var annotation in info.Annotations)
            {
                if (!annotation.AppliesLoadedEffects) continue;
                if (loadedByTag == null || !loadedByTag.TryGetValue(annotation.AppliedEffectsTag, out var effects)) continue;

                foreach (var effect in effects)
                {
                    var usage = BuildUsage(effect, EAbilityEffectRole.SequenceApplication, stageIndex,
                        nameof(RunSequenceTask), annotation.Scope, minLevel, maxLevel, visited);
                    if (usage == null) continue;

                    usage.ApplicationsHint = annotation.MaxApplications;
                    usage.Annotation = annotation.Text;
                    breakdown.Usages.Add(usage);
                    claimedLoadedEffects?.Add(effect);
                }
            }
        }

        private static void AddUsage(AbilityBreakdown breakdown, GameplayEffect effect, EAbilityEffectRole role,
            int stageIndex, string taskType, EEffectTargetScope scope, int minLevel, int maxLevel, HashSet<GameplayEffect> visited)
        {
            var usage = BuildUsage(effect, role, stageIndex, taskType, scope, minLevel, maxLevel, visited);
            if (usage != null) breakdown.Usages.Add(usage);
        }

        private static AbilityEffectUsage BuildUsage(GameplayEffect effect, EAbilityEffectRole role,
            int stageIndex, string taskType, EEffectTargetScope scope, int minLevel, int maxLevel, HashSet<GameplayEffect> visited)
        {
            if (effect == null) return null;

            var usage = new AbilityEffectUsage
            {
                Effect = effect,
                Role = role,
                StageIndex = stageIndex,
                TaskType = taskType,
                Scope = scope,
                Affiliation = effect.ImpactSpecification?.AffiliationPolicy ?? EAffiliationPolicy.AlwaysAllow,
                Series = BuildEffectSeries(effect, minLevel, maxLevel),
            };

            // Contained effects — cycle-guarded (an effect chain can reference itself).
            if (effect.ImpactSpecification?.Packets != null && visited.Add(effect))
            {
                foreach (var packet in effect.ImpactSpecification.Packets)
                {
                    if (packet.ContainedEffect == null) continue;

                    var containedRole = packet.Policy switch
                    {
                        EApplyTickRemove.OnApply => EAbilityEffectRole.ContainedOnApply,
                        EApplyTickRemove.OnTick => EAbilityEffectRole.ContainedOnTick,
                        EApplyTickRemove.OnRemove => EAbilityEffectRole.ContainedOnRemove,
                        _ => EAbilityEffectRole.ContainedOnApply,
                    };

                    var contained = BuildUsage(packet.ContainedEffect, containedRole, stageIndex, taskType, scope, minLevel, maxLevel, visited);
                    if (contained != null) usage.Contained.Add(contained);
                }

                visited.Remove(effect);
            }

            return usage;
        }

        /// <summary>Reflection scan for GameplayEffect references on a task (custom task support).</summary>
        public static IEnumerable<GameplayEffect> FindEffectReferences(object obj)
        {
            if (obj == null) yield break;

            var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                object value;
                try { value = field.GetValue(obj); }
                catch { continue; }

                switch (value)
                {
                    case GameplayEffect effect:
                        yield return effect;
                        break;
                    case IEnumerable<GameplayEffect> list:
                    {
                        foreach (var effect in list)
                        {
                            if (effect != null) yield return effect;
                        }
                        break;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // LEVEL-PINNED EVALUATION
        // ═══════════════════════════════════════════════════════════════════════════

        private static IAttributeImpactCarrier MakeLevelCarrier(int level, int minLevel, int maxLevel)
        {
            // No live source: scalers configured LockToLevelProvider fall back to the
            // origin's own level pair (see AbstractScaler.GetEffectiveMaxLevel).
            return IAttributeImpactCarrier.GenerateLevelerDerivation(
                null, new IntValuePairClamped(level, minLevel, maxLevel));
        }

        private static float SafeEvaluate(ScalableFloat operation, IAttributeImpactCarrier carrier, out bool isDynamic)
            => SafeEvaluate(operation, carrier, out isDynamic, out _);

        private static float SafeEvaluate(ScalableFloat operation, IAttributeImpactCarrier carrier, out bool isDynamic, out string reason)
        {
            isDynamic = false;
            reason = null;
            if (operation == null) return 0f;

            // Attribute-backed scalers read runtime attribute captures — the static
            // evaluation is best-effort and flagged.
            if (operation.Scaler is AttributeBackedScaler)
            {
                isDynamic = true;
                reason = "scales with a runtime attribute";
            }
            else if (operation.Scaler is RandomizedScaler)
            {
                isDynamic = true;
                reason = "randomized";
            }
            else if (operation.Scaler is ConditionalScaler)
            {
                isDynamic = true;
                reason = "conditional on runtime state";
            }

            try
            {
                return operation.Evaluate(carrier);
            }
            catch (Exception)
            {
                isDynamic = true;
                reason ??= "requires runtime context";
                return operation.Magnitude;
            }
        }

        private static int SafeEvaluateInt(ScalableInt operation, IAttributeImpactCarrier carrier, out bool isDynamic)
        {
            isDynamic = false;
            if (operation == null) return 0;

            try
            {
                return operation.Evaluate(carrier);
            }
            catch (Exception)
            {
                isDynamic = true;
                return operation.Magnitude;
            }
        }
    }
}
