using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// The effects the lab makes for itself, as an asset's inspector would fill one in: a dummy's mend, where the project
    /// gives none (LAB-4). Each lives in memory alone and goes with what made it.
    /// </summary>
    public static class LabEffects
    {
        /// <summary>
        /// An instant effect that adds to an attribute's current value, by the amount <see cref="SetAmount"/> last gave:
        /// a mend that brings Health back to its base, as much as is missing.
        /// </summary>
        public static GameplayEffect Restore(Attribute attribute)
        {
            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            effect.hideFlags = HideFlags.DontSave;
            effect.name = $"Lab Mend {attribute.GetName()}";
            effect.Definition = new GameplayEffectDefinition { Name = "Mend", Textures = new List<TextureItem>(), Visibility = Tags.NOT_VISIBLE };
            effect.Tags = new GameplayEffectTags
            {
                AssetTag = Tag.GenerateAsUnique($"Lab Mend {attribute.GetName()}"),
                ContextTags = new List<Tag>(),
                GrantedTags = new List<Tag>(),
            };
            effect.Workers = new List<AbstractEffectWorker>();
            effect.SourceRequirements = NoRequirements();
            effect.TargetRequirements = NoRequirements();
            effect.ImpactSpecification = new GameplayEffectImpact
            {
                AttributeTarget = attribute,
                TargetImpact = EAttributeTarget.Current,
                ImpactOperation = ECalculationOperation.Add,
                AffiliationPolicy = EAffiliationPolicy.AlwaysAllow,
                ImpactTypes = new List<Tag>(),
                MagnitudeOperation = new ScalableFloat { Magnitude = 0f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                Packets = System.Array.Empty<ContainedEffectPacket>(),
            };
            effect.DurationSpecification = new GameplayEffectDuration
            {
                DurationPolicy = EEffectDurationPolicy.Instant,
                DurationOperation = new ScalableFloat { Magnitude = 0f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                TicksOperation = new ScalableInt { Magnitude = 0 },
                TickIntervalOperation = new ScalableFloat { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                AdditionalExecuteTicksOperation = new ScalableInt { Magnitude = 0 },
                StackAmountOperation = new ScalableInt { Magnitude = 1 },
                StackingConfigs = new List<AbstractStackingConfig>(),
                StackPolicy = EEffectStackPolicy.StacksShareOneDuration,
                RealDeltaTime = EMagnitudeOperation.AddScaler,
            };
            return effect;
        }

        /// <summary>The amount a <see cref="Restore"/> effect adds as it's next applied, which its entries take as they're made.</summary>
        public static void SetAmount(GameplayEffect effect, float amount)
        {
            if (effect?.ImpactSpecification?.MagnitudeOperation == null) return;
            effect.ImpactSpecification.MagnitudeOperation.Magnitude = amount;
        }

        // Groups with no queries: an application passes, and nothing removes the effect early
        private static EffectTagRequirements NoRequirements() => new()
        {
            ApplicationRequirements = new AvoidRequireTagGroup(),
            OngoingRequirements = new AvoidRequireTagGroup(),
            RemovalRequirements = new AvoidRequireTagGroup(),
        };
    }
}
