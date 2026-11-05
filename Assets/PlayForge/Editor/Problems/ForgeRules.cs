using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // The validator's own rules (ED-15). Each finds what an inspector warns about in place, in the inspector's words where
    // it has some, so the Problems view, the header's count and the inspector agree. Validate All's four checks are gone:
    // an effect with no workers, or an ability with no cost or cooldown, is fine.

    /// <summary>An asset holding data whose class Unity can't find, as after a class is renamed or removed.</summary>
    internal sealed class MissingClassRule : ForgeValidationRule
    {
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
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                $"Its asset tag is {was}, but its name gives {result}. Identity's Update stores it."));
        }
    }

    /// <summary>An asset set to take a provider's levels, with no provider, as Leveling warns.</summary>
    internal sealed class LinkRule : ForgeValidationRule
    {
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
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, message, "LinkMode"));
        }
    }

    /// <summary>
    /// An effect's impact that never lands as its author expects: the first impact's delivery note, as the Audit flagged
    /// it, and an additional impact whose delivery the duration can't make, as its entry warns.
    /// </summary>
    internal sealed class ImpactRule : ForgeValidationRule
    {
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
    /// A magnitude field that reads a scaler it hasn't got, as the field warns, and a Shared scaler with no Scaler asset.
    /// </summary>
    internal sealed class MagnitudeRule : ForgeValidationRule
    {
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
                        _ => null,
                    };
                    if (message != null)
                        problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"{ScalerFields.FieldLabel(iterator.propertyPath)} {message}", iterator.propertyPath));
                    continue;
                }
                if (iterator.propertyType != SerializedPropertyType.Generic) continue;
                if (iterator.type != nameof(ScalerMagnitudeOperation) && iterator.type != nameof(ScalerIntegerMagnitudeOperation)) continue;
                var real = iterator.FindPropertyRelative(nameof(ScalerMagnitudeOperation.RealMagnitude));
                var scaler = iterator.FindPropertyRelative(nameof(ScalerMagnitudeOperation.Scaler));
                if (real == null || scaler == null || scaler.managedReferenceValue != null) continue;
                if ((EMagnitudeOperation)real.intValue is EMagnitudeOperation.UseScaler or EMagnitudeOperation.MultiplyWithScaler)
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                        $"{ScalerFields.FieldLabel(iterator.propertyPath)} reads its scaler, but has none, so it reads 0.", iterator.propertyPath));
            }
        }
    }

    /// <summary>
    /// Two fields that hold one scaler, as after a list's "+" copied an element's, so an edit to one changes both, as the
    /// scaler drawer warns. An attribute set element's Current and Base, which Copy Base's Settings shares, are left.
    /// </summary>
    internal sealed class SharedScalerRule : ForgeValidationRule
    {
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
                    $"{ScalerFields.FieldLabel(path)} and {ScalerFields.FieldLabel(before)} hold one scaler, so an edit to one changes both.", path));
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
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"Element {i} has no attribute, so it gives nothing.", path));
                    continue;
                }
                var name = string.IsNullOrEmpty(element.Attribute.Name) ? element.Attribute.name : element.Attribute.Name;
                var clamped = element.Constraints?.AutoClamp ?? true;
                foreach (var message in AttributeSetElementDrawer.BoundsMessages(element.Overflow, clamped, element.Base.Magnitude,
                             element.LinkCurrentToBase, element.Current.Magnitude))
                    problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"{name}: {message}", path));
            }
        }
    }
}
