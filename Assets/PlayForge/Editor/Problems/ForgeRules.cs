using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The validator's own rules (ED-15). Each finds what an inspector warns about in place, in the inspector's words where
    // it has some, so the Problems view, the header's count and the inspector agree. Validate All's four checks are gone:
    // an effect with no workers, or an ability with no cost or cooldown, is fine. Each names what it finds, which the
    // Problems view's By Rule view groups by, and a problem a simple step fixes carries the step (FEAT-25).

    /// <summary>
    /// A quick fix's step: a write through a SerializedObject of the asset's own, which records Undo, so the step the
    /// Problems view opens takes it back, and the validator, which follows each edit, checks the asset again (FEAT-25).
    /// </summary>
    internal static class QuickFixes
    {
        public static void Write(Object asset, Action<SerializedObject> write)
        {
            if (asset == null) return;
            var serialized = new SerializedObject(asset);
            write(serialized);
            serialized.ApplyModifiedProperties();
        }
    }

    /// <summary>An asset holding data whose class Unity can't find, as after a class is renamed or removed.</summary>
    internal sealed class MissingClassRule : ForgeValidationRule
    {
        // Its fix would throw the data away, which only its author can choose
        public override string Name => "Data whose class is missing";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(asset))
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Error,
                    "It holds data whose class Unity can't find, as after a class is renamed or removed."));
        }
    }

    /// <summary>An asset with no name, which has no asset tag of its own.</summary>
    internal sealed class NameRule : ForgeValidationRule
    {
        public override string Name => "Assets with no name";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (asset is BaseForgeAsset forge && string.IsNullOrWhiteSpace(forge.GetName()))
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, "It has no name."));
        }
    }

    /// <summary>
    /// An asset whose stored tag isn't the one its name gives, as the Identity section's asset tag shows it, with Update.
    /// </summary>
    internal sealed class AssetTagRule : ForgeValidationRule
    {
        public override string Name => "Asset tags their names don't give";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            var (name, typeName, stored) = asset switch
            {
                Ability ability => (ability.Definition?.Name, "Ability", ability.Tags.AssetTag),
                GameplayEffect effect => (effect.Definition?.Name, "Effect", effect.Tags.AssetTag),
                Item item => (item.Definition?.Name, "Item", item.Tags.AssetTag),
                EntityIdentity entity => (entity.Name, "Entity", entity.AssetTag),
                AttributeSet set => (set.Name, "AttributeSet", set.AssetTag),
                _ => (null, null, default(Tag)),
            };
            if (typeName == null) return;
            var (result, display, isUnknown) = ForgeAssetCreation.AssetTagFor(name, typeName);
            if (isUnknown || (stored.Name == result && stored.DisplayName == display)) return;
            var was = string.IsNullOrEmpty(stored.Name) ? "not set" : stored.Name;
            // A tag whose path is right reads by its display name, which its name gives too: the message named the same
            // path twice (FEAT-25)
            var message = stored.Name != result
                ? $"Its asset tag is {was}, but its name gives {result}. Identity's Update stores it."
                : string.IsNullOrEmpty(stored.DisplayName)
                    ? $"Its asset tag {result} has no display name, but its name gives it {display}. Identity's Update stores it."
                    : $"Its asset tag {result} reads as {stored.DisplayName}, but its name gives {display}. Identity's Update stores it.";
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, message)
            {
                // As Identity's Update stores it
                Fix = new ForgeQuickFix("Store the Tag", () => QuickFixes.Write(asset, serialized =>
                {
                    var tag = serialized.FindProperty("Tags.AssetTag") ?? serialized.FindProperty("AssetTag");
                    if (tag == null) return;
                    var from = tag.FindPropertyRelative(nameof(Tag.Name)).stringValue;
                    tag.FindPropertyRelative(nameof(Tag.Name)).stringValue = result;
                    tag.FindPropertyRelative(nameof(Tag.DisplayName)).stringValue = display;
                    // What stored its old tag stores the new one, or it finds the asset no more (ED-84)
                    ForgeTagRenames.Rename(from, result, display, asset);
                })),
            });
        }
    }

    /// <summary>An asset set to take a provider's levels, with no provider, as Leveling warns.</summary>
    internal sealed class LinkRule : ForgeValidationRule
    {
        public override string Name => "Links with no level provider";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            var (linked, kind) = asset switch
            {
                Ability ability => (ability.LinkMode == EAbilityLinkMode.LinkedToProvider, "ability"),
                Item item => (item.LinkMode == EItemLinkMode.LinkedToProvider, "item"),
                EntityIdentity entity => (entity.LinkMode == EItemLinkMode.LinkedToProvider, "entity"),
                AttributeSet set => (set.LinkMode == EAttributeSetLinkMode.LinkedToProvider, "set"),
                GameplayEffect effect => (effect.LinkMode == EEffectLinkMode.LinkedToProvider, "effect"),
                _ => (false, null),
            };
            if (!linked || asset is not BaseForgeLevelProvider provider || provider.LinkedProvider != null) return;
            var message = kind == "effect"
                ? "No level provider is set, so the effect takes the level of what applies it."
                : $"No level provider is set, so the {kind} uses its own levels.";
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, message, "LinkMode")
            {
                // With no provider, it already uses its own levels, or its applier's: the link goes, and nothing reads
                // differently
                Fix = new ForgeQuickFix("Use Own Levels", () => QuickFixes.Write(asset, serialized =>
                {
                    var mode = serialized.FindProperty("LinkMode");
                    if (mode == null) return;
                    int standalone = Array.IndexOf(mode.enumNames, "Standalone");
                    mode.enumValueIndex = standalone >= 0 ? standalone : 0;
                })),
            });
        }
    }

    /// <summary>
    /// An effect's impact that never lands as its author expects: the first impact's delivery note, as the Audit flagged
    /// it, and an additional impact whose delivery the duration can't make, as its entry warns.
    /// </summary>
    internal sealed class ImpactRule : ForgeValidationRule
    {
        // Its fix is the author's choice: the delivery, or the duration
        public override string Name => "Impacts that don't land as set";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (asset is not GameplayEffect effect || effect.ImpactSpecification == null) return;
            var impact = effect.ImpactSpecification;
            var duration = effect.DurationSpecification;

            bool authored = impact.AttributeTarget != null ||
                            impact.MagnitudeOperation != null && (impact.MagnitudeOperation.Scaler != null || impact.MagnitudeOperation.Magnitude != 0f);
            if (authored)
            {
                impact.ResolveDelivery(duration, out var note);
                if (!string.IsNullOrEmpty(note))
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, note, nameof(GameplayEffect.ImpactSpecification)));
            }

            if (duration == null) return;
            var entries = impact.AdditionalImpacts ?? new List<AttributeImpactEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null) continue;
                var problem = ImpactFields.DeliveryProblem(duration, entries[i].Delivery);
                if (problem == null) continue;
                var on = entries[i].AttributeTarget != null ? $" on {entries[i].AttributeTarget.Name}" : "";
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"Additional impact {i + 1}{on}: {problem}",
                    $"{nameof(GameplayEffect.ImpactSpecification)}.{nameof(GameplayEffectImpact.AdditionalImpacts)}.Array.data[{i}]"));
            }
        }
    }

    /// <summary>
    /// A magnitude field that reads a scaler it hasn't got, as the field warns, a Shared scaler with no Scaler asset, and a
    /// Group whose members multiply with none that adds, which multiply 0 (ED-80).
    /// </summary>
    internal sealed class MagnitudeRule : ForgeValidationRule
    {
        public override string Name => "Fields with no scaler to read";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            var iterator = new SerializedObject(asset).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    var message = iterator.managedReferenceValue switch
                    {
                        SharedScaler { Asset: null } => "shares no Scaler asset, so it reads 0.",
                        SharedCachedScaler { Asset: null } => "shares no Scaler asset, so it reads its root value.",
                        AbstractScaler group when ScalerFields.MultipliesNothing(group) =>
                            "is a Group with members that multiply and none that adds, so they multiply 0.",
                        _ => null,
                    };
                    if (message != null)
                        problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"{ScalerFields.FieldLabel(iterator.propertyPath)} {message}", iterator.propertyPath));
                    continue;
                }
                if (iterator.propertyType != SerializedPropertyType.Generic) continue;
                if (!NumberFields.IsNumber(iterator)) continue;
                var real = iterator.FindPropertyRelative(nameof(ScalableFloat.RealMagnitude));
                var scaler = iterator.FindPropertyRelative(nameof(ScalableFloat.Scaler));
                if (real == null || scaler == null || scaler.managedReferenceValue != null) continue;
                if ((EMagnitudeOperation)real.intValue is EMagnitudeOperation.UseScaler or EMagnitudeOperation.MultiplyWithScaler)
                {
                    var path = iterator.propertyPath;
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                        $"{ScalerFields.FieldLabel(path)} reads its scaler, but has none, so it reads 0.", path)
                    {
                        // As the field's own message says: Constant reads its number
                        Fix = new ForgeQuickFix("Make It Constant", () => QuickFixes.Write(asset, serialized =>
                        {
                            var reads = serialized.FindProperty($"{path}.{nameof(ScalableFloat.RealMagnitude)}");
                            if (reads != null) reads.intValue = (int)EMagnitudeOperation.UseMagnitude;
                        })),
                    });
                }
            }
        }
    }

    /// <summary>
    /// A number whose kind its field's context can't resolve (FEAT-29), as a kind that reads the target in a field that
    /// declares none, often one copied from another field. At runtime it reads its number, as the field warns. Its fix
    /// is a choice of kind, so it has no Quick Fix.
    /// <para>
    /// A relative number is flagged where its field's code gives it no owner, as any kind its field can't resolve, and
    /// where the object that holds it has no field of the name it reads, as after a rename (NUM-9).
    /// </para>
    /// </summary>
    internal sealed class NumberContextRule : ForgeValidationRule
    {
        public override string Name => "Numbers whose kind can't be read where they are";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            var iterator = new SerializedObject(asset).GetIterator();
            string outer = null;
            while (iterator.Next(true))
            {
                if (!NumberFields.IsNumber(iterator)) continue;
                var path = iterator.propertyPath;
                // A number inside another's scaler is read with it, so the outer number's check covers what it reads
                bool inner = outer != null && path.StartsWith(outer, StringComparison.Ordinal);
                if (!inner) outer = $"{path}.";
                var kind = iterator.FindPropertyRelative(nameof(ScalableFloat.Scaler))?.managedReferenceValue as AbstractScaler;
                if (kind == null) continue;
                var provides = NumberFields.ProvidesOf(iterator);
                var missing = inner ? "" : NumberFields.Missing(NumberFields.ReadsOf(kind), provides);
                if (missing.Length > 0)
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                        $"{ScalerFields.FieldLabel(path)} reads {missing}, which its field doesn't have, so it reads its number.", path));

                // With an owner to read, a relative number still needs the field it names: each number's own, inside a
                // scaler's or a condition's too, read on the object its owner is (FEAT-29)
                if ((provides & NumberReads.Owner) == 0) continue;
                Type owner = null;
                foreach (var relative in NumberFields.ScalersIn(kind, NumberFields.Walk.SameNumber).OfType<RelativeToFieldKind>())
                {
                    owner ??= NumberFields.OwnerTypeOf(iterator);
                    if (owner == null) break;
                    if (string.IsNullOrEmpty(relative.Field))
                        problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                            $"{ScalerFields.FieldLabel(path)} is relative to a field, and names none.", path));
                    else if (RelativeToFieldKind.SiblingField(owner, relative.Field) == null)
                        problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                            $"{ScalerFields.FieldLabel(path)} reads the field \"{relative.Field}\", which " +
                            $"{ObjectNames.NicifyVariableName(owner.Name)} has no number field by the name of, so it reads its number.", path));
                }
            }
        }
    }

    /// <summary>
    /// Two fields that hold one scaler, as after a list's "+" copied an element's, so an edit to one changes both, as the
    /// scaler drawer warns. An attribute set element's Current and Base, which Copy Base's Settings shares, are left.
    /// </summary>
    internal sealed class SharedScalerRule : ForgeValidationRule
    {
        public override string Name => "Scalers two fields share";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            var first = new Dictionary<long, string>();
            var iterator = new SerializedObject(asset).GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference) continue;
                if (iterator.managedReferenceValue is not AbstractScaler) continue;
                long id = iterator.managedReferenceId;
                var path = iterator.propertyPath;
                if (!first.TryGetValue(id, out var before))
                {
                    first[id] = path;
                    continue;
                }
                if (ElementOf(before) == ElementOf(path)) continue;
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                    $"{ScalerFields.FieldLabel(path)} and {ScalerFields.FieldLabel(before)} hold one scaler, so an edit to one changes both.", path)
                {
                    // The second field gets a copy of its own, with the same values
                    Fix = new ForgeQuickFix("Give It Its Own", () => QuickFixes.Write(asset, serialized =>
                    {
                        var field = serialized.FindProperty(path);
                        if (field?.managedReferenceValue is not AbstractScaler scaler) return;
                        var copy = (AbstractScaler)Activator.CreateInstance(scaler.GetType());
                        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(scaler), copy);
                        field.managedReferenceValue = copy;
                    })),
                });
            }
        }

        // An attribute set element's scaler, Current's or Base's, as the one path both share
        private static string ElementOf(string path) =>
            path.Replace($".{nameof(AttributeSetElement.Current)}.", ".").Replace($".{nameof(AttributeSetElement.Base)}.", ".");
    }

    /// <summary>
    /// An attribute set that gives nothing, an element with no attribute, which gives nothing (GAS-54), and a magnitude
    /// outside its bounds, which the runtime clamps, as the element warns.
    /// </summary>
    internal sealed class AttributeSetRule : ForgeValidationRule
    {
        public override string Name => "Attribute sets and their elements";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (asset is not AttributeSet set) return;
            var elements = set.Attributes ?? new List<AttributeSetElement>();
            if (elements.Count == 0 && (set.SubSets == null || set.SubSets.All(sub => sub == null)))
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, "It gives no attributes: it has none of its own, and no sub-sets."));

            for (int i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                var path = $"{nameof(AttributeSet.Attributes)}.Array.data[{i}]";
                if (element?.Attribute == null)
                {
                    int index = i;
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"Element {i} has no attribute, so it gives nothing.", path)
                    {
                        // It gives nothing, so it goes. Fix All takes a rule's problems from the last, so the elements
                        // before keep their places
                        Fix = new ForgeQuickFix("Remove the Element", () => QuickFixes.Write(asset, serialized =>
                        {
                            var list = serialized.FindProperty(nameof(AttributeSet.Attributes));
                            if (list != null && index < list.arraySize) list.DeleteArrayElementAtIndex(index);
                        })),
                    });
                    continue;
                }
                var name = string.IsNullOrEmpty(element.Attribute.Name) ? element.Attribute.name : element.Attribute.Name;
                var clamped = element.Constraints?.AutoClamp ?? true;
                bool linked = element.LinkCurrentToBase;
                foreach (var message in AttributeSetElementDrawer.BoundsMessages(element.Overflow, clamped, element.Base.Magnitude,
                             linked, element.Current.Magnitude))
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"{name}: {message}", path)
                    {
                        // The magnitudes the runtime would clamp them to, stored, Current within the bounds Base sets
                        Fix = new ForgeQuickFix("Clamp to Bounds", () => QuickFixes.Write(asset, serialized =>
                        {
                            var each = serialized.FindProperty(path);
                            if (each == null) return;
                            var overflow = AttributeSetElementDrawer.ReadOverflowFromProperty(each.FindPropertyRelative(nameof(AttributeSetElement.Overflow)));
                            var baseMagnitude = each.FindPropertyRelative($"{nameof(AttributeSetElement.Base)}.{nameof(AttributeMagnitudeSpec.Magnitude)}");
                            var currentMagnitude = each.FindPropertyRelative($"{nameof(AttributeSetElement.Current)}.{nameof(AttributeMagnitudeSpec.Magnitude)}");
                            baseMagnitude.floatValue = overflow.ClampBase(baseMagnitude.floatValue);
                            if (!linked) currentMagnitude.floatValue = overflow.ClampCurrent(currentMagnitude.floatValue, baseMagnitude.floatValue);
                        })),
                    });
            }
        }
    }

    /// <summary>
    /// Charges that a built-in recharge brings back with no Cooldown to bring them (TW-17): they never run out, so they
    /// limit nothing. A recharge of one's own decides for itself. It took the place of ED-41's rule, of a cost or a
    /// cooldown no stage applied, as TW-17 applies them as the ability starts.
    /// </summary>
    internal sealed class ChargesRule : ForgeValidationRule
    {
        public override string Name => "Charges that never run out";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (asset is not Ability ability) return;
            var message = NeverRunOut(ability);
            if (message != null) problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, message, nameof(Ability.Components)));
        }

        /// <summary>
        /// Whether its charges may be counted, as far as the asset says: more than 1, or a number that scales, or a recharge
        /// that counts a single charge. With 1 and a built-in recharge, it's an ordinary cooldown.
        /// </summary>
        internal static bool Counted(Ability ability) =>
            ability.Charges != null && (!ability.Charges.IsConstant || ability.Charges.Constant > 1)
            || (ability.Recharge?.CountsASingleCharge ?? false);

        // Each Charge and All At Once, exactly: a recharge of one's own, even one made from them, decides for itself
        private static bool BuiltIn(AbstractChargeRecharge recharge) =>
            recharge == null || recharge.GetType() == typeof(EachChargeRecharge) || recharge.GetType() == typeof(AllAtOnceRecharge);

        /// <summary>Why its charges never run out, said as the inspector says it, or null where they do or aren't counted.</summary>
        internal static string NeverRunOut(Ability ability)
        {
            if (ability.Cooldown != null || !BuiltIn(ability.Recharge) || !Counted(ability)) return null;
            return "Its charges never run out, as it has no Cooldown to bring them back. Give it a Cooldown, or a recharge of " +
                   "your own.";
        }
    }

    /// <summary>
    /// A transfer that never moves as it's set (TW-20): one whose delivery its effect's duration can't make, as an additional
    /// impact's, or one that names no attribute to give or to get; and an ability that toggles an Instant effect, which is
    /// never on to turn off.
    /// </summary>
    internal sealed class TransfersRule : ForgeValidationRule
    {
        public override string Name => "Transfers and toggles that never work";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            if (asset is Ability ability && ability.Toggles != null
                && ability.Toggles.DurationSpecification?.DurationPolicy == EEffectDurationPolicy.Instant)
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                    "It toggles an Instant effect, which is never on to turn off. Toggle a lasting one.", nameof(Ability.Components)));

            if (asset is not GameplayEffect effect || effect.Transfers == null) return;
            for (int i = 0; i < effect.Transfers.Count; i++)
            {
                var transfer = effect.Transfers[i];
                if (transfer == null) continue;
                var problem = !transfer.FromAttribute || !transfer.ToAttribute
                    ? "It names no attribute to give or to get, so it moves nothing."
                    : effect.DurationSpecification != null ? ImpactFields.DeliveryProblem(effect.DurationSpecification, transfer.Delivery) : null;
                if (problem == null) continue;
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"Transfer {i + 1}: {problem}",
                    $"{nameof(GameplayEffect.Transfers)}.Array.data[{i}]"));
            }
        }
    }

    /// <summary>
    /// An asset that leans on its project's roles where they can't serve it (TW-29; TW-28's fixed attributes before it): an
    /// ability's attribute cost that names nothing to spend, or a role no attribute plays in its project; an entity whose
    /// starting abilities spend attributes its set doesn't give; and an entity that stays when it dies, whom the Health
    /// role's attribute can't kill, as its set lacks it, with no death watcher of its own. Each asset is read against its
    /// own project, the one whose folder holds it.
    /// </summary>
    internal sealed class ProjectRolesRule : ForgeValidationRule
    {
        public override string Name => "The project's roles";

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            switch (asset)
            {
                case Ability ability:
                    CheckCosts(ability, problems);
                    break;
                case EntityIdentity entity:
                    CheckEntity(entity, problems);
                    break;
            }
        }

        // Whether a cost asks anything: an amount above 0, or one a scaler reads
        private static bool Asks(AttributeCost cost) =>
            cost?.Amount != null && (cost.Amount.Magnitude > 0f || !AbstractScaler.IsNone(cost.Amount.Scaler));

        private static void CheckCosts(Ability ability, List<ForgeProblem> problems)
        {
            var costs = ability.AttributeCosts;
            if (costs == null) return;
            ForgeProject project = null;
            bool looked = false;
            for (int i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (cost == null || cost.Attribute != null || !Asks(cost)) continue;
                if (!cost.Role.IsValid)
                {
                    problems.Add(new ForgeProblem(ability, ForgeProblemSeverity.Warning,
                        $"Its attribute cost {i + 1} names no attribute and no role, so it spends nothing.", nameof(Ability.AttributeCosts)));
                    continue;
                }

                // In no project, which roles it can have isn't known
                if (!looked)
                {
                    project = ForgeProjects.Of(ability);
                    looked = true;
                }
                if (project == null || project.Settings?.AttributeFor(cost.Role) != null) continue;
                problems.Add(new ForgeProblem(ability, ForgeProblemSeverity.Warning,
                    $"Its attribute cost {i + 1} spends the {cost.Role} role, and no attribute plays it in {project.name}, so it's " +
                    "refused with Cost. Fill the role in the project's settings, or name an attribute.", nameof(Ability.AttributeCosts)));
            }
        }

        private static void CheckEntity(EntityIdentity entity, List<ForgeProblem> problems)
        {
            var project = ForgeProjects.Of(entity);
            var given = entity.AttributeSet != null ? entity.AttributeSet.GetUnique() : new HashSet<IAttribute>();

            // Its starting abilities' costs, each of which spends an attribute its set must give
            foreach (var ability in entity.StartingAbilities ?? new List<Ability>())
            {
                if (ability == null || ability.AttributeCosts == null) continue;
                foreach (var cost in ability.AttributeCosts)
                {
                    if (!Asks(cost)) continue;
                    var spends = cost.Attribute != null ? cost.Attribute : project != null ? project.Settings?.AttributeFor(cost.Role) : null;
                    if (spends == null || given.Contains(spends)) continue;
                    problems.Add(new ForgeProblem(entity, ForgeProblemSeverity.Warning,
                        $"{ability.GetName()} spends {spends.Name}, which its attribute set doesn't give, so it's refused with Cost.",
                        nameof(EntityIdentity.StartingAbilities)));
                    break;
                }
            }

            var health = project != null ? project.Settings?.AttributeFor(Tags.ROLE_HEALTH) : null;
            var death = entity.Death;
            if (health == null || !EntityDeath.Stays(death) || !death.DiesAtZeroHealth || given.Contains(health) || WatchesForDeath(entity)) return;
            problems.Add(new ForgeProblem(entity, ForgeProblemSeverity.Warning,
                $"It stays when it dies, but its attribute set has no {health.Name}, which plays its project's Health role, and no " +
                "death watcher of its own watches, so only code can kill it.", nameof(EntityIdentity.Death)));
        }

        // Whether a death watcher of its own, or of its attribute sets', watches for its death
        private static bool WatchesForDeath(EntityIdentity entity)
        {
            if (entity.WorkerGroup?.AnalysisWorkers?.Any(worker => worker is DeathWatcherAnalysisWorker) == true) return true;
            if (entity.AttributeSet == null) return false;
            return entity.AttributeSet.SelfAndSubSets().Any(set =>
                set != null && set.WorkerGroup?.AnalysisWorkers?.Any(worker => worker is DeathWatcherAnalysisWorker) == true);
        }
    }
}
