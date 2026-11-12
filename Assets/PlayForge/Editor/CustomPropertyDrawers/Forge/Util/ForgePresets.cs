using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A way to start a new asset (TW-24, Decision 64): one of PlayForge's presets, which fills the new asset's fields, or
    /// one of the project's own template assets, which the new asset starts as a copy of.
    /// </summary>
    public sealed class ForgePreset
    {
        internal ForgePreset(string name, Type type, string assetName, string description,
            Action<ScriptableObject, ForgePresetMaking> fill)
        {
            Name = name;
            Type = type;
            AssetName = assetName;
            Description = description;
            Fill = fill;
        }

        /// <summary>Its name in The Forge's Create menu, under its type.</summary>
        public string Name { get; }

        /// <summary>The type of asset it makes.</summary>
        public Type Type { get; }

        /// <summary>What a new one is called until it's named: "Damage Effect" gives "New Damage Effect".</summary>
        public string AssetName { get; }

        /// <summary>What it makes, as its menu's tooltip says.</summary>
        public string Description { get; }

        /// <summary>The project's template asset it copies, or null for one of PlayForge's.</summary>
        public ScriptableObject Template { get; internal set; }

        // Fills the new asset, after the type's default and before its name
        internal Action<ScriptableObject, ForgePresetMaking> Fill { get; }
    }

    /// <summary>
    /// What a preset reaches as it fills a new asset (TW-24): its name, the project, and a way to make the assets it needs,
    /// each saved in its type's folder, or in the one the new asset was asked for where that isn't its type's.
    /// </summary>
    public sealed class ForgePresetMaking
    {
        private readonly List<Action> then = new();

        internal ForgePresetMaking(string name, string folder)
        {
            Name = name;
            Folder = folder;
        }

        /// <summary>The new asset's name.</summary>
        public string Name { get; }

        /// <summary>The folder the assets it needs go in, or null for each type's own.</summary>
        public string Folder { get; }

        /// <summary>The project whose settings it reads.</summary>
        public ForgeProject Project => PlayForgeSettings.Project;

        /// <summary>The attribute playing the project's Health role, or null.</summary>
        public Attribute Health => Project ? Project.Settings?.AttributeFor(Tags.ROLE_HEALTH) : null;

        /// <summary>Makes another asset the new one needs, saved now, from a preset or as a copy of an asset where one's given.</summary>
        public T Make<T>(string name, ForgePreset preset = null, ScriptableObject copyOf = null) where T : ScriptableObject
        {
            if (copyOf != null) preset = ForgePresets.FromTemplate(copyOf);
            return (T)ForgeAssetCreation.CreateFrom(typeof(T), name, Folder, preset);
        }

        /// <summary>Something to do once the new asset is saved, as linking another asset to it.</summary>
        public void Then(Action action)
        {
            if (action != null) then.Add(action);
        }

        internal void Finish()
        {
            foreach (var action in then) action();
            then.Clear();
        }
    }

    /// <summary>The presets The Forge's Create menu lists for each type: PlayForge's, then the project's templates (TW-24).</summary>
    public static class ForgePresets
    {
        private static readonly List<ForgePreset> builtIn = new()
        {
            Effect("Damage", "Damage Effect", "An instant hit to the project's Health, on those of other sides",
                (effect, making) => Impact(effect, making.Health, -10f, EAffiliationPolicy.Unaffiliated)),
            Effect("Damage over Time", "Damage over Time", "5 seconds of hits to the project's Health, one a second, on those of other sides",
                (effect, making) =>
                {
                    Lasting(effect, EEffectDurationPolicy.Durational, 5f, ticks: 5);
                    Impact(effect, making.Health, -4f, EAffiliationPolicy.Unaffiliated);
                    effect.DurationSpecification.ReApplicationPolicy = EEffectReApplicationPolicy.ReplaceOldContainer;
                }),
            Effect("Buff", "Buff", "A bonus to an attribute you choose, while it lasts, 10 seconds, on its own side",
                (effect, _) =>
                {
                    Lasting(effect, EEffectDurationPolicy.Durational, 10f);
                    Impact(effect, null, 10f, EAffiliationPolicy.Affiliated);
                    effect.DurationSpecification.ReApplicationPolicy = EEffectReApplicationPolicy.ReplaceOldContainer;
                }),
            Effect("Debuff", "Debuff", "A penalty to an attribute you choose, while it lasts, 10 seconds, on those of other sides",
                (effect, _) =>
                {
                    Lasting(effect, EEffectDurationPolicy.Durational, 10f);
                    Impact(effect, null, -10f, EAffiliationPolicy.Unaffiliated);
                    effect.DurationSpecification.ReApplicationPolicy = EEffectReApplicationPolicy.ReplaceOldContainer;
                }),
            Effect("Stun", "Stun", "1.5 seconds under the tag of the project's crowd control rule that stops every ability",
                (effect, making) =>
                {
                    Lasting(effect, EEffectDurationPolicy.Durational, 1.5f);
                    Impact(effect, null, 0f, EAffiliationPolicy.Unaffiliated);
                    if (StunTag(making) is { IsValid: true } stunned) (effect.Tags.GrantedTags ??= new List<Tag>()).Add(stunned);
                }),
            Effect("Aura", "Aura", "An aura, held while it lasts, its effect yours to choose, on its holder's side within 5",
                (effect, _) =>
                {
                    Lasting(effect, EEffectDurationPolicy.Infinite, 0f);
                    Impact(effect, null, 0f, EAffiliationPolicy.Affiliated);
                    effect.Auras.Add(new AuraEntry());
                }),
            new("On-Hit", typeof(Item), "On-Hit Item", "An item whose holder's hits apply the effects its On Hit entry names",
                (asset, _) => ((Item)asset).OnHit.Add(new OnHitEntry { To = EOnHitTo.Target })),
            new("Active", typeof(Item), "Active Item", "An item that grants an ability of its own, with a cost and a cooldown",
                (asset, making) => ((Item)asset).ActiveAbility = making.Make<Ability>(making.Name, Active)),
            new("Passive (Triggered)", typeof(Ability), "Passive Ability",
                "An ability its holder's hits taken set off, which applies the effects its stage names", Passive),
            new("Hero", typeof(EntityIdentity), "Hero",
                "An entity with an attribute set of its own: the attributes playing the project's roles, Health first", Hero),
        };

        // The ability an Active Item grants, which the menu doesn't list
        private static readonly ForgePreset Active = new("Active Ability", typeof(Ability), "Ability",
            "An ability with a stage that applies its effects, a cost and a cooldown", ActiveAbility);

        /// <summary>The presets for a type: PlayForge's own, then the project's template assets of the type.</summary>
        public static IReadOnlyList<ForgePreset> For(Type type)
        {
            var presets = new List<ForgePreset>();
            if (type == null) return presets;
            foreach (var preset in builtIn)
                if (preset.Type == type) presets.Add(preset);
            var project = PlayForgeSettings.Project;
            var templates = project ? project.Settings?.Authoring?.Templates : null;
            if (templates != null)
                foreach (var template in templates)
                    if (template && type.IsInstanceOfType(template)) presets.Add(FromTemplate(template));
            return presets;
        }

        /// <summary>Makes a new asset from a preset, named, in the folder given or its type's.</summary>
        public static ScriptableObject Make(ForgePreset preset, string name, string folder = null) =>
            preset == null ? null : ForgeAssetCreation.CreateFrom(preset.Type, name, folder, preset);

        // A preset from a template asset: a new one starts as its copy, then takes its own name and tag
        internal static ForgePreset FromTemplate(ScriptableObject template)
        {
            string name = TheForge.NameOf(template);
            return new ForgePreset(name, template.GetType(), name, $"A copy of {name}, the project's template",
                (asset, _) => EditorUtility.CopySerialized(template, asset)) { Template = template };
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The built-ins' parts
        // ═══════════════════════════════════════════════════════════════════════════

        private static ForgePreset Effect(string name, string assetName, string description, Action<GameplayEffect, ForgePresetMaking> fill) =>
            new(name, typeof(GameplayEffect), assetName, description, (asset, making) =>
            {
                var effect = (GameplayEffect)asset;
                Lasting(effect, EEffectDurationPolicy.Instant, 0f);
                fill(effect, making);
            });

        // Its duration, from a whole one where the type's default left none
        private static void Lasting(GameplayEffect effect, EEffectDurationPolicy policy, float seconds, int ticks = 0)
        {
            var duration = effect.DurationSpecification ??= new GameplayEffectDuration();
            duration.DurationPolicy = policy;
            duration.DurationOperation = new ScalableFloat { Magnitude = seconds, RealMagnitude = EMagnitudeOperation.UseMagnitude };
            duration.EnablePeriodicTicks = ticks > 0;
            duration.TicksOperation = new ScalableInt { Magnitude = ticks };
            duration.TickIntervalOperation ??= new ScalableFloat { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude };
            duration.AdditionalExecuteTicksOperation ??= new ScalableInt { Magnitude = 0 };
            duration.StackAmountOperation ??= new ScalableInt { Magnitude = 1 };
            duration.StackingConfigs ??= new List<AbstractStackingConfig>();
        }

        // Its one impact, an add to the attribute named, or to none, yours to choose; on the sides given
        private static void Impact(GameplayEffect effect, Attribute attribute, float magnitude, EAffiliationPolicy sides)
        {
            var impact = effect.ImpactSpecification ??= new GameplayEffectImpact();
            impact.AttributeTarget = attribute;
            impact.TargetImpact = EAttributeTarget.Current;
            impact.ImpactOperation = ECalculationOperation.Add;
            impact.AffiliationPolicy = sides;
            impact.ImpactTypes ??= new List<Tag>();
            impact.MagnitudeOperation = new ScalableFloat { Magnitude = magnitude, RealMagnitude = EMagnitudeOperation.UseMagnitude };
            impact.Packets ??= Array.Empty<ContainedEffectPacket>();
        }

        // The tag of the project's first crowd control rule that stops every ability, as Stunned
        private static Tag StunTag(ForgePresetMaking making)
        {
            var rules = making.Project ? making.Project.Settings?.CrowdControl : null;
            if (rules == null || rules.Rules == null) return default;
            foreach (var rule in rules.Rules)
                if (rule != null && rule.Tag.IsValid && rule.Stops == ECrowdControlStops.EveryAbility) return rule.Tag;
            return default;
        }

        private static void Passive(ScriptableObject asset, ForgePresetMaking making)
        {
            var ability = (Ability)asset;
            ability.Definition ??= new AbilityDefinition();
            ability.Definition.Triggers ??= new List<AbilityTrigger>();
            ability.Definition.Triggers.Add(new AbilityTrigger
            {
                Source = EAbilityTriggerSource.GameplayEvent, Tag = Tags.IMPACT_TAKEN, Attribute = making.Health, Sign = ETriggerImpactSign.Loss,
            });
            Applying(ability);
        }

        private static void ActiveAbility(ScriptableObject asset, ForgePresetMaking making)
        {
            var ability = (Ability)asset;
            Applying(ability);

            // Its cost and its cooldown, each a copy of the project's template where it has one, linked to it as it's saved
            var authoring = making.Project ? making.Project.Settings?.Authoring : null;
            var cost = making.Make<GameplayEffect>($"{making.Name} Cost", authoring?.CostEffect ? null : CostPreset, authoring?.CostEffect);
            var cooldown = making.Make<GameplayEffect>($"{making.Name} Cooldown", authoring?.CooldownEffect ? null : CooldownPreset, authoring?.CooldownEffect);
            ability.Cost = cost;
            ability.Cooldown = cooldown;
            making.Then(() =>
            {
                foreach (var effect in new[] { cost, cooldown })
                {
                    if (!effect) continue;
                    effect.LinkToProvider(ability);
                    EditorUtility.SetDirty(effect);
                    AssetDatabase.SaveAssetIfDirty(effect);
                }
            });
        }

        private static readonly ForgePreset CostPreset = Effect("Cost", "Cost", "What an ability spends as it's used",
            (effect, _) => Impact(effect, null, -10f, EAffiliationPolicy.AlwaysAllow));

        private static readonly ForgePreset CooldownPreset = Effect("Cooldown", "Cooldown", "How long an ability waits after it's used",
            (effect, _) =>
            {
                Lasting(effect, EEffectDurationPolicy.Durational, 5f);
                Impact(effect, null, 0f, EAffiliationPolicy.AlwaysAllow);
            });

        // One stage that applies the effects it names, yours to choose
        private static void Applying(Ability ability)
        {
            ability.Behaviour ??= new AbilityBehaviour();
            ability.Behaviour.Stages ??= new List<AbilityTaskBehaviourStage>();
            ability.Behaviour.Stages.Add(new AbilityTaskBehaviourStage
            {
                Tasks = new List<AbstractAbilityTask> { new ApplyEffectsAbilityTask { Effects = new List<GameplayEffect>() } },
            });
        }

        private static void Hero(ScriptableObject asset, ForgePresetMaking making)
        {
            var hero = (EntityIdentity)asset;
            var set = making.Make<AttributeSet>($"{making.Name} Attributes");

            // The attributes playing the project's roles, Health first, at 100, the rest yours to set
            var attributes = new List<Attribute>();
            var health = making.Health;
            if (health) attributes.Add(health);
            var roles = making.Project ? making.Project.Settings?.Roles : null;
            if (roles != null)
                foreach (var role in roles)
                    if (role != null && role.Attribute && !attributes.Contains(role.Attribute)) attributes.Add(role.Attribute);
            set.Attributes = new List<AttributeSetElement>();
            foreach (var attribute in attributes) set.Attributes.Add(Element(attribute, attribute == health ? 100f : 0f));
            set.SubSets ??= new List<AttributeSet>();
            set.WorkerGroup ??= new StandardWorkerGroup();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);

            hero.AttributeSet = set;
            hero.StartingAbilities ??= new List<Ability>();
        }

        private static AttributeSetElement Element(Attribute attribute, float value) => new()
        {
            Attribute = attribute,
            Current = new AttributeMagnitudeSpec { Magnitude = value, RealMagnitude = EMagnitudeOperation.UseMagnitude },
            Base = new AttributeMagnitudeSpec { Magnitude = value, RealMagnitude = EMagnitudeOperation.UseMagnitude },
            Overflow = new AttributeOverflowData { Policy = EAttributeOverflowPolicy.ZeroToBase },
            RetentionGroup = Tags.DEFAULT,
            Constraints = new AttributeConstraints { AutoClamp = true, RoundingMode = EAttributeRoundingPolicy.None },
        };
    }
}
