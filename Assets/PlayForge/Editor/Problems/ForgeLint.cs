using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What the project's assets give each other, which the content linter's checks read (TW-25, Decision 65): the impact
    /// types its effects and impact workers deal, the gameplay events its thresholds and stop stages send, and the effects
    /// its items apply. It's gathered from every PlayForge asset, and the shared Value Pipelines and Effect Modifiers, as a
    /// check first asks, and forgotten as the validator checks the project afresh, or as an asset changes what it gives,
    /// which has every asset checked again against what's now true.
    /// </summary>
    public static class ForgeLint
    {
        // The core's own impact types and events, as Tags marks them: an impact type every filter takes, as Always Allow,
        // or one the core deals, as Returned; an event the core sends, as Kill
        private static readonly HashSet<string> CoreImpactTypes = CoreTags(ForgeContext.Impact);
        private static readonly HashSet<string> CoreEvents = CoreTags(ForgeContext.Trigger);

        // The events that carry an impact, which a trigger's impact types filter; another's data holds none
        private static readonly string[] ImpactEvents = { Tags.IMPACT_DEALT.Name, Tags.IMPACT_TAKEN.Name };

        private static Facts facts;

        private sealed class Facts
        {
            public readonly HashSet<string> Dealt = new(StringComparer.Ordinal);
            public readonly HashSet<string> Sent = new(StringComparer.Ordinal);
            public readonly HashSet<GameplayEffect> FromItems = new();

            // What each asset gave, by its instance ID, to tell when that changes
            public readonly Dictionary<int, string> Gave = new();
        }

        // What one asset gives: the impact types it deals, the events it sends, and the effects it applies
        private sealed class Given
        {
            public readonly List<string> Dealt = new();
            public readonly List<string> Sent = new();
            public readonly List<GameplayEffect> Applies = new();

            public string Signature => Dealt.Count + Sent.Count + Applies.Count == 0
                ? ""
                : string.Join(",", Dealt.OrderBy(name => name, StringComparer.Ordinal)) + "|" +
                  string.Join(",", Sent.OrderBy(name => name, StringComparer.Ordinal)) + "|" +
                  string.Join(",", Applies.Where(effect => effect != null).Select(effect => effect.GetInstanceID()).OrderBy(id => id));
        }

        /// <summary>
        /// Forgets what the linter knows of the project, so its next check gathers it again, as after a script changes assets
        /// in bulk. The validator forgets it as it checks the project afresh.
        /// </summary>
        public static void Forget() => facts = null;

        private static Facts Gathered => facts ??= Gather();

        /// <summary>Whether something deals an impact type, by its tag's name: an effect in the project, an impact worker's work, or the core.</summary>
        internal static bool Deals(string impactType) =>
            CoreImpactTypes.Contains(impactType) || Gathered.Dealt.Contains(impactType);

        /// <summary>
        /// Whether something sends an event a trigger on a tag takes, by the tag's name: the core, or an asset in the project,
        /// under the tag or one under it.
        /// </summary>
        internal static bool Sends(string trigger) =>
            CoreEvents.Any(sent => Under(sent, trigger)) || Gathered.Sent.Any(sent => Under(sent, trigger));

        /// <summary>Whether an item in the project applies an effect, or an effect an item applies does, as a contained effect.</summary>
        internal static bool FromAnItem(GameplayEffect effect) => effect != null && Gathered.FromItems.Contains(effect);

        /// <summary>Whether a trigger on a tag, by its name, takes events that carry an impact: Impact Dealt and Impact Taken.</summary>
        internal static bool TakesImpacts(string trigger) => ImpactEvents.Any(sent => Under(sent, trigger));

        /// <summary>
        /// Whether an asset gives the linter facts: every asset the validator checks, and a shared Value Pipeline or Effect
        /// Modifier, whose stages and changes give as an asset's own do.
        /// </summary>
        internal static bool Gives(Object asset) => ForgeValidator.Checks(asset) || asset is ValuePipeline or EffectModifier;

        /// <summary>
        /// Whether an asset gives other than it gave as the facts were gathered, as an effect that now deals another impact
        /// type; then the facts are forgotten. Before they're gathered, nothing has read them.
        /// </summary>
        internal static bool Moved(Object asset)
        {
            if (facts == null || asset == null || !Gives(asset)) return false;
            facts.Gave.TryGetValue(asset.GetInstanceID(), out var before);
            if ((before ?? "") == GivenBy(asset).Signature) return false;
            facts = null;
            return true;
        }

        // The assets that give: the validator's, and the shared pipelines and modifiers they name
        private static IEnumerable<ScriptableObject> Givers() => ForgeValidator.ProjectAssets().Concat(
            new[] { nameof(ValuePipeline), nameof(EffectModifier) }
                .SelectMany(type => AssetDatabase.FindAssets("t:" + type))
                .Distinct()
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .Where(asset => asset is ValuePipeline or EffectModifier));

        private static Facts Gather()
        {
            var gathered = new Facts();
            var fromItems = new List<GameplayEffect>();
            var applies = new Dictionary<GameplayEffect, List<GameplayEffect>>();
            foreach (var asset in Givers())
            {
                var given = GivenBy(asset);
                gathered.Gave[asset.GetInstanceID()] = given.Signature;
                gathered.Dealt.UnionWith(given.Dealt);
                gathered.Sent.UnionWith(given.Sent);
                switch (asset)
                {
                    case Item:
                        fromItems.AddRange(given.Applies);
                        break;
                    case GameplayEffect effect:
                        applies[effect] = given.Applies;
                        break;
                }
            }

            // An item's effects, then the effects they apply in turn
            var next = new Queue<GameplayEffect>(fromItems);
            while (next.Count > 0)
            {
                var effect = next.Dequeue();
                if (effect == null || !gathered.FromItems.Add(effect) || !applies.TryGetValue(effect, out var further)) continue;
                foreach (var each in further) next.Enqueue(each);
            }
            return gathered;
        }

        private static Given GivenBy(Object asset)
        {
            var given = new Given();
            if (asset is GameplayEffect effect && effect.ImpactSpecification != null)
            {
                NamesOf(effect.ImpactSpecification.ImpactTypes, given.Dealt);
                if (effect.ImpactSpecification.AdditionalImpacts != null)
                    foreach (var impact in effect.ImpactSpecification.AdditionalImpacts)
                        if (impact != null) NamesOf(impact.ImpactTypes, given.Dealt);
            }

            bool applies = asset is Item or GameplayEffect;
            using var serialized = new SerializedObject(asset);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    switch (iterator.managedReferenceValue)
                    {
                        // The impact types an impact worker's work deals, as a thorns worker's return
                        case AbstractContextImpactWorker worker:
                            NamesOf(worker.WorkImpactType, given.Dealt);
                            break;
                        // The event a threshold sends as it fires
                        case ThresholdAnalysisWorker threshold when threshold.Event.IsValid:
                            given.Sent.Add(threshold.Event.Name);
                            break;
                        // The events a stop stage sends as it stops a hit, as a project's own parry's
                        case AbstractStopStage stop:
                            if (stop.StoppedEvent.IsValid) given.Sent.Add(stop.StoppedEvent.Name);
                            if (stop.DealerEvent.IsValid) given.Sent.Add(stop.DealerEvent.Name);
                            break;
                    }
                }
                else if (applies && iterator.propertyType == SerializedPropertyType.ObjectReference
                                 && iterator.objectReferenceValue is GameplayEffect referenced && referenced != asset)
                    given.Applies.Add(referenced);
            }
            return given;
        }

        private static void NamesOf(List<Tag> tags, List<string> into)
        {
            if (tags == null) return;
            foreach (var tag in tags)
                if (tag.IsValid) into.Add(tag.Name);
        }

        // Whether an event is the trigger's tag or one under it, by their names, as a trigger takes it
        private static bool Under(string sent, string trigger) =>
            sent == trigger || sent.Length > trigger.Length && sent.StartsWith(trigger, StringComparison.Ordinal) && sent[trigger.Length] == '.';

        private static HashSet<string> CoreTags(string context) => new(
            typeof(Tags).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.PropertyType == typeof(Tag)
                                   && property.GetCustomAttribute<ForgeContextDefault>()?.Context?.Contains(context) == true)
                .Select(property => ((Tag)property.GetValue(null)).Name)
                .Where(name => !string.IsNullOrEmpty(name)),
            StringComparer.Ordinal);

        // ═══════════════════════════════════════════════════════════════════════════
        // What the linter's checks share
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Whether a number reads a scaler of a type: it reads its kind, and the kind is one, or holds one.</summary>
        internal static bool Reads<T>(ScalableFloat number) where T : AbstractScaler =>
            number != null && !number.IsConstant && NumberFields.ScalersIn(number).Any(scaler => scaler is T);

        /// <summary>The path of the first number in an asset that reads a scaler of a type, or null where none does.</summary>
        internal static string FirstReading<T>(Object asset) where T : AbstractScaler
        {
            using var serialized = new SerializedObject(asset);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (!NumberFields.IsNumber(iterator)) continue;
                var reads = iterator.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
                var kind = iterator.FindPropertyRelative(nameof(ScalableFloat.Scaler))?.managedReferenceValue as AbstractScaler;
                if (reads == null || AbstractScaler.IsNone(kind) || (EMagnitudeOperation)reads.intValue == EMagnitudeOperation.UseMagnitude) continue;
                if (NumberFields.ScalersIn(kind).Any(scaler => scaler is T)) return iterator.propertyPath;
            }
            return null;
        }

        /// <summary>An entry's name, as its list numbers it from 1: "On Hit 2" for a list's second.</summary>
        internal static string Numbered(string name, string path)
        {
            var index = Regex.Match(path ?? "", @"\[(\d+)\]$");
            return index.Success ? $"{name} {int.Parse(index.Groups[1].Value) + 1}" : name;
        }
    }

    /// <summary>
    /// A number that counts its stacks twice (TW-25): a stacking effect's periodic impact, or transfer, ticks once for each
    /// stack already, so one that reads its own stacks hits 9 times as hard with 3; and an effect that reads an item's
    /// stacks where no item applies it, which reads 1.
    /// </summary>
    internal sealed class StacksReadTwiceRule : ForgeValidationRule
    {
        public override string Name => "Stacks read twice";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (asset is not GameplayEffect effect) return;
            CheckEffectStacks(effect, problems);

            // An effect not yet saved may yet be an item's
            if (!EditorUtility.IsPersistent(effect)) return;
            var path = ForgeLint.FirstReading<ItemStacksScaler>(effect);
            if (path == null || ForgeLint.FromAnItem(effect)) return;
            problems.Add(new ForgeProblem(effect, ForgeProblemSeverity.Warning,
                "It reads an item's stacks, but no item in the project applies it, so they read 1.", path));
        }

        private static void CheckEffectStacks(GameplayEffect effect, List<ForgeProblem> problems)
        {
            var duration = effect.DurationSpecification;
            var impact = effect.ImpactSpecification;
            if (duration == null || impact == null || duration.DurationPolicy == EEffectDurationPolicy.Instant
                || duration.ReApplicationPolicy != EEffectReApplicationPolicy.StackExistingContainers) return;

            const string ticks = "but ticks once for each stack already, so 3 stacks hit 9 times as hard as 1.";
            const string impactPath = nameof(GameplayEffect.ImpactSpecification);

            // The first impact, as the effect derives its entry (ED-3)
            bool authored = impact.AttributeTarget != null ||
                            impact.MagnitudeOperation != null && (impact.MagnitudeOperation.Scaler != null || impact.MagnitudeOperation.Magnitude != 0f);
            if (authored && impact.ResolveDelivery(duration, out _) == EImpactDelivery.Periodic)
                Add(effect, impact.MagnitudeOperation, $"{impactPath}.{nameof(GameplayEffectImpact.MagnitudeOperation)}",
                    $"Its impact reads its stacks, {ticks}", problems);

            var entries = impact.AdditionalImpacts ?? new List<AttributeImpactEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry?.AttributeTarget == null || entry.Delivery != EImpactDelivery.Periodic || !entry.IsDeliveryValidFor(duration)) continue;
                Add(effect, entry.Magnitude,
                    $"{impactPath}.{nameof(GameplayEffectImpact.AdditionalImpacts)}.Array.data[{i}].{nameof(AttributeImpactEntry.Magnitude)}",
                    $"Additional impact {i + 1} on {entry.AttributeTarget.Name} reads its stacks, {ticks}", problems);
            }

            var transfers = effect.Transfers ?? new List<AttributeTransfer>();
            for (int i = 0; i < transfers.Count; i++)
            {
                var transfer = transfers[i];
                if (transfer == null || transfer.Delivery != EImpactDelivery.Periodic || ImpactFields.DeliveryProblem(duration, transfer.Delivery) != null) continue;
                Add(effect, transfer.Amount, $"{nameof(GameplayEffect.Transfers)}.Array.data[{i}].{nameof(AttributeTransfer.Amount)}",
                    $"Transfer {i + 1} reads its stacks, but moves once for each stack already, so 3 stacks move 9 times what 1 does.", problems);
            }
        }

        private static void Add(GameplayEffect effect, ScalableFloat number, string path, string message, List<ForgeProblem> problems)
        {
            if (!ForgeLint.Reads<EffectStacksScaler>(number)) return;
            var problem = new ForgeProblem(effect, ForgeProblemSeverity.Warning, message, path);

            // Where its stacks are what it reads, it reads what one stack does, a plain number; a stacks read inside another
            // kind, as a Group's member, is the author's to rework
            if (number.Scaler is EffectStacksScaler)
                problem.Fix = new ForgeQuickFix("Count Stacks Once", () => QuickFixes.Write(effect, serialized =>
                {
                    var magnitude = serialized.FindProperty($"{path}.{nameof(ScalableFloat.Magnitude)}");
                    var reads = serialized.FindProperty($"{path}.{nameof(ScalableFloat.RealMagnitude)}");
                    var scaler = serialized.FindProperty($"{path}.{nameof(ScalableFloat.Scaler)}");
                    if (magnitude == null || reads == null || scaler == null) return;
                    magnitude.floatValue = ForgeHelper.MagnitudeAndScalerOperation(magnitude.floatValue, 1f, (EMagnitudeOperation)reads.intValue);
                    reads.intValue = (int)EMagnitudeOperation.UseMagnitude;
                    scaler.managedReferenceValue = null;
                }));
            problems.Add(problem);
        }
    }

    /// <summary>
    /// A filter's impact type that no effect in the project deals, so it never matches (TW-25): an On Hit's, a lifesteal's,
    /// a trigger's that takes events, or a modifier's Change Impact's. Where the filter keeps a type that's dealt, its fix
    /// removes the one that isn't; one that has only types none deals would take every hit without them, so it's the
    /// author's to fix.
    /// </summary>
    internal sealed class FiltersThatNeverMatchRule : ForgeValidationRule
    {
        public override string Name => "Filters that never match";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            using var serialized = new SerializedObject(asset);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                string kind = iterator.propertyType switch
                {
                    SerializedPropertyType.Generic when iterator.type == nameof(OnHitEntry) => "On Hit",
                    SerializedPropertyType.Generic when iterator.type == nameof(LifestealEntry) => "Lifesteal",
                    SerializedPropertyType.Generic when iterator.type == nameof(AbilityTrigger) => "Trigger",
                    SerializedPropertyType.ManagedReference when iterator.managedReferenceValue is ChangeImpactChange => "Change Impact",
                    _ => null,
                };
                if (kind == null) continue;
                // A trigger reads its impact types only for an event that carries an impact: not a tag gained, a Kill, or an
                // event of the project's own, which code may send with any data
                if (kind == "Trigger" && (iterator.FindPropertyRelative(nameof(AbilityTrigger.Source))?.intValue != (int)EAbilityTriggerSource.GameplayEvent
                                          || !ForgeLint.TakesImpacts(iterator.FindPropertyRelative($"{nameof(AbilityTrigger.Tag)}.{nameof(Tag.Name)}")?.stringValue ?? "")))
                    continue;
                CheckFilter(asset, iterator.FindPropertyRelative(nameof(OnHitEntry.ImpactTypes)), ForgeLint.Numbered(kind, iterator.propertyPath), problems);
            }
        }

        private static void CheckFilter(Object asset, SerializedProperty types, string label, List<ForgeProblem> problems)
        {
            if (types == null || !types.isArray || types.arraySize == 0) return;
            var undealt = new List<(string name, string display)>();
            bool keepsOne = false;
            for (int i = 0; i < types.arraySize; i++)
            {
                var type = types.GetArrayElementAtIndex(i);
                var name = type.FindPropertyRelative(nameof(Tag.Name))?.stringValue;
                if (string.IsNullOrEmpty(name)) continue;
                if (ForgeLint.Deals(name))
                {
                    keepsOne = true;
                    continue;
                }
                var display = type.FindPropertyRelative(nameof(Tag.DisplayName))?.stringValue;
                undealt.Add((name, string.IsNullOrEmpty(display) ? name : display));
            }

            var path = types.propertyPath;
            foreach (var (name, display) in undealt)
            {
                var problem = new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                    $"{label} filters for {display}, which no effect in the project deals, so {(keepsOne ? "that type" : "it")} never matches.", path);
                // It never matched, so it goes, and the filter takes what it took; Fix All takes each such type
                if (keepsOne)
                    problem.Fix = new ForgeQuickFix("Remove the Type", () => QuickFixes.Write(asset, written =>
                    {
                        var list = written.FindProperty(path);
                        if (list == null || !list.isArray) return;
                        for (int i = list.arraySize - 1; i >= 0; i--)
                            if (list.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(Tag.Name))?.stringValue == name)
                                list.DeleteArrayElementAtIndex(i);
                    }));
                problems.Add(problem);
            }
        }
    }

    /// <summary>
    /// A trigger on a gameplay event that the core doesn't send and no asset in the project does, as a threshold sends its
    /// event (TW-25). Code may send it, so it's a note.
    /// </summary>
    internal sealed class EventsNothingSendsRule : ForgeValidationRule
    {
        public override string Name => "Events nothing sends";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (asset is not Ability ability || ability.Definition?.Triggers == null) return;
            var triggers = ability.Definition.Triggers;
            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger == null || trigger.Source != EAbilityTriggerSource.GameplayEvent || !trigger.Tag.IsValid) continue;
                if (ForgeLint.Sends(trigger.Tag.Name)) continue;
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Note,
                    $"Trigger {i + 1} waits for {trigger.Tag}, an event no asset in the project sends, so only code can set it off.",
                    $"{nameof(Ability.Definition)}.{nameof(AbilityDefinition.Triggers)}.Array.data[{i}]"));
            }
        }
    }
}
