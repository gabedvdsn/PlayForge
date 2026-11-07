using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;

namespace FarEmerald.PlayForge.Samples.Demo.Building
{
    public static partial class DemoBuilder
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Making abilities
        // ═══════════════════════════════════════════════════════════════════════════

        private enum Kind
        {
            Attack,
            Spell,
            Unstoppable,
            Item,
        }

        /// <summary>
        /// An ability's shell, saved first so its effects can take it as their level provider: levels 1 to 4, a stun
        /// keeping it from being cast, and a silence too for a spell.
        /// </summary>
        private static Ability NewAbility(string folder, string name, string description, Kind kind, int maxLevel = 4)
        {
            var ability = ScriptableObject.CreateInstance<Ability>();
            ability.Definition = new AbilityDefinition
            {
                Name = name, Description = description, ActivationPolicy = EAbilityActivationPolicyExtended.UseLocalPolicy,
                Textures = new List<TextureItem>(), Triggers = new List<AbilityTrigger>(),
            };
            var source = kind switch
            {
                Kind.Spell => Avoid(Stunned, Silenced),
                Kind.Unstoppable => Avoid(),
                _ => Avoid(Stunned),
            };
            ability.Tags = new AbilityTags
            {
                AssetTag = T($"Demo.Ability.{Key(name)}"),
                ContextTags = new List<Tag> { kind switch { Kind.Attack => AttackKind, Kind.Item => ItemKind, _ => Spell } },
                PassiveGrantedTags = new List<Tag>(),
                ActiveGrantedTags = new List<Tag>(),
                TagRequirements = new AbilityTagRequirements { SourceRequirements = source, WhileActiveRequirements = Avoid(), TargetRequirements = Avoid() },
            };
            ability.Behaviour = new AbilityBehaviour
            {
                Targeting = null, UseImplicitTargeting = true, ImplicitTag = Tags.TARGET, AssetLoaders = new List<AbstractSetAssetsTask>(),
                Stages = new List<AbilityTaskBehaviourStage>(), CancelledBy = new List<Tag>(), Blocks = new List<Tag>(),
            };
            ability.SourceActivationRules = new List<IAbilityValidationRule> { new CooldownValidation(), new CostValidation(), new IsAliveValidation() };
            ability.TargetActivationRules = new List<IAbilityValidationRule> { new IsAliveValidation() };
            ability.StartingLevel = 1;
            ability.MaxLevel = maxLevel;
            ability.IgnoreWhenLevelZero = true;
            ability.WorkerGroup = new StandardWorkerGroup { Name = "Workers" };
            ability.LocalData = new List<DataWrapper>();
            return Save($"{folder}/{name}", ability);
        }

        /// <summary>The ability's mana cost, by level, linked to it.</summary>
        private static void Costs(Ability ability, string folder, params float[] mana)
        {
            var cost = NewEffect($"{ability.GetName()} Cost", $"{ability.GetName()}'s mana, by its level.");
            cost.ImpactSpecification.AttributeTarget = Mana;
            cost.ImpactSpecification.MagnitudeOperation = Negated(Named(Levels(mana), "Mana, by level"));
            cost.LinkToProvider(ability);
            ability.Cost = Save($"{folder}/Usage/{ability.GetName()} Cost", cost);
        }

        /// <summary>
        /// The ability's cooldown, by level, which the caster's Cooldown Reduction shortens: its table times 1 less the
        /// reduction, read as it's cast. An attack's isn't shortened.
        /// </summary>
        private static void Cools(Ability ability, string folder, bool reducible, params float[] seconds)
        {
            var cooldown = NewEffect($"{ability.GetName()} Cooldown", reducible
                ? $"{ability.GetName()}'s cooldown, by its level, times 1 less the caster's Cooldown Reduction."
                : $"{ability.GetName()}'s time between uses.");
            cooldown.DurationSpecification.DurationPolicy = EEffectDurationPolicy.Durational;
            var table = Named(Levels(seconds), "Seconds, by level");
            cooldown.DurationSpecification.DurationOperation = reducible
                ? Scaled(Named(Group((table, Add), (Read(CooldownReduction, policy: EMathApplicationPolicy.OneMinus), Multiply)), "Cooldown"))
                : Scaled(table);
            cooldown.LinkToProvider(ability);
            ability.Cooldown = Save($"{folder}/Usage/{ability.GetName()} Cooldown", cooldown);
        }

        private static AbilityTaskBehaviourStage Stage(params AbstractAbilityTask[] tasks) => new()
        {
            StagePolicy = new AllProxyStagePolicy(), Tasks = tasks.ToList(), ApplyUsageEffects = false, ApplyUsageAtStageStart = true,
            Condition = new AbilityStageCondition(), CancelledBy = new List<Tag>(), Blocks = new List<Tag>(),
        };

        /// <summary>The stage that spends the ability's cost and starts its cooldown as it begins.</summary>
        private static AbilityTaskBehaviourStage Spend(params AbstractAbilityTask[] tasks)
        {
            var stage = Stage(tasks);
            stage.ApplyUsageEffects = true;
            return stage;
        }

        /// <summary>A stage that runs only while a tag is there, or missing, on the caster, the target or the activation.</summary>
        private static AbilityTaskBehaviourStage When(EAbilityStageConditionSubject on, Tag tag, bool missing, params AbstractAbilityTask[] tasks)
        {
            var stage = Stage(tasks);
            stage.Condition = new AbilityStageCondition { On = on, Tag = tag, WhenMissing = missing };
            return stage;
        }

        private static ApplyEffectsAbilityTask Apply(params GameplayEffect[] effects) => new() { Effects = effects.ToList(), DelayBetweenApplicationMs = 0 };
        private static ApplyToCasterTask OnSelf(params GameplayEffect[] effects) => new() { Effects = effects.ToList() };
        private static DelayTask Wait(int milliseconds) => new() { DelayMilliseconds = milliseconds };

        private static CreateMonoProcessAbilityTask Spawn(AbstractMonoProcess process, CreateMonoProcessAbilityTask.ESpawnPoint at) => new()
        {
            DataTag = Tags.DATA, RegisterMany = false, DataTarget = EDataTarget.Primary, InternalIsOverride = true,
            InternalMonoProcesses = new List<AbstractMonoProcess> { process }, SpawnAt = at,
        };

        private static RunSequenceTask Sequence(string method) => new()
        {
            Sequence = new TaskSequenceReference(typeof(DemoSequences).GetMethod(method, BindingFlags.Public | BindingFlags.Static)),
        };

        // A target to click: an entity the other side can see. Handed one in the data, as the AI does, it takes it at once
        private static SelectGasTargetTask PickTarget() => new()
        {
            RaycastLayers = ~0, MaxDistance = 300f, RetryOnInvalid = true, MouseButton = 0, TakeTargetFromData = true,
            Requirements = Avoid(Invisible, Untargetable), BreakRuntimeOnInvalid = true,
        };

        private static SelectGasTargetInRangeTask PickInRange(float range) => new()
        {
            RaycastLayers = ~0, MaxRayDistance = 300f, MaxTargetRange = range, FlattenRangeCheck = true, RetryOnInvalid = true,
            MouseButton = 0, TakeTargetFromData = true, Requirements = Avoid(Invisible, Untargetable), BreakRuntimeOnInvalid = true,
        };

        private static SelectPositionTargetTask PickPoint() => new()
        {
            RaycastLayers = ~0, MaxDistance = 300f, RetryOnInvalid = true, MouseButton = 0, TakeTargetFromData = true, BreakRuntimeOnInvalid = true,
        };

        private static void Finish(Ability ability, AbstractTargetingAbilityTask targeting, params AbilityTaskBehaviourStage[] stages)
        {
            ability.Behaviour.Targeting = targeting;
            ability.Behaviour.UseImplicitTargeting = targeting == null;
            ability.Behaviour.Stages = stages.ToList();
            Dirty(ability);
        }

        private static void AiHint(Ability ability, string when, float reach)
        {
            ability.LocalData.RemoveAll(data => data.Key == CastWhen || data.Key == Reach);
            ability.LocalData.Add(Local(CastWhen, when));
            ability.LocalData.Add(Local(Reach, reach));
        }

        /// <summary>A projectile prefab: it flies at the speed it carries and applies its effects as it lands.</summary>
        private static ProjectileMonoProcess Projectile(string name, float speed, float size, Color color, params GameplayEffect[] effects) =>
            Prefab<ProjectileMonoProcess>($"Projectiles/{name}", (host, projectile) =>
            {
                projectile.ProcessLifecycle = EProcessLifecycle.SelfTerminating;
                projectile.ProcessTiming = EProcessStepTiming.None;
                projectile.Effects = effects.ToList();
                projectile.AttributePairs = new List<TagAttributePair>();
                projectile.Attributes = new List<ProcessAttribute>
                {
                    new() { Attribute = ProjectileSpeed, Role = Tags.PROJECTILE_SPEED, StartsFrom = EProcessAttributeStart.Own, Value = Fixed(speed) },
                };
                Look(host, PrimitiveType.Sphere, Vector3.one * size, new Vector3(0f, 1.2f, 0f), color);
            });

        // ═══════════════════════════════════════════════════════════════════════════
        // Default attacks, which every hero has one of
        // ═══════════════════════════════════════════════════════════════════════════

        private static Ability MeleeAttack(string hero, string name, string description, ForgeAttribute stat, float coefficient, float baseDamage,
            float seconds, out GameplayEffect blow)
        {
            string folder = $"Abilities/{hero}";
            var attack = NewAbility(folder, name, description, Kind.Attack);
            blow = Blow($"{folder}/Effects", $"{name} Damage", $"{name}'s blow: physical, by {stat.Name}, times the attack growth.", PhysicalStrike,
                Damage(new[] { baseDamage }, stat, coefficient, Physical, attack: true), attack, AttackHit);
            Cools(attack, folder, false, seconds);
            attack.Tags.ActiveGrantedTags.Add(Attacking);
            return attack;
        }

        private static Ability RangedAttack(string hero, string name, string description, GameplayEffect template, Tag kind, ForgeAttribute stat,
            float coefficient, float baseDamage, float seconds, float speed, Color color, AbstractScaler extra = null)
        {
            string folder = $"Abilities/{hero}";
            var attack = NewAbility(folder, name, description, Kind.Attack);
            var blow = Blow($"{folder}/Effects", $"{name} Damage", $"{name}'s blow, as its projectile lands.", template,
                Damage(new[] { baseDamage }, stat, coefficient, kind, attack: true, extra: extra), attack, AttackHit);
            Cools(attack, folder, false, seconds);
            attack.Tags.ActiveGrantedTags.Add(Attacking);
            var projectile = Projectile(name, speed, 0.35f, color, blow);
            Finish(attack, PickTarget(), Spend(Wait(120), Spawn(projectile, CreateMonoProcessAbilityTask.ESpawnPoint.Caster)));
            return attack;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The Vanguard: Strength, melee, physical
        // ═══════════════════════════════════════════════════════════════════════════

        private static readonly List<Ability> VanguardKit = new(), RangerKit = new(), PyromancerKit = new(), ShadeKit = new(), OracleKit = new();

        private static void MakeVanguard()
        {
            const string hero = "Vanguard", folder = "Abilities/Vanguard";
            VanguardKit.Clear();

            var strike = MeleeAttack(hero, "Strike", "A melee blow, physical, by Strength.", Strength, 1.2f, 30f, 1f, out var strikeBlow);
            Finish(strike, PickTarget(), Spend(Wait(150), Apply(strikeBlow)));

            var bash = NewAbility(folder, "Shield Bash", "Bashes an enemy in reach: physical damage, and a stun that grows with its level. Check Range keeps it to its reach.", Kind.Spell);
            var bashBlow = Blow($"{folder}/Effects", "Shield Bash Damage", "Shield Bash's blow: physical, by Strength.", PhysicalStrike,
                Damage(new[] { 60f, 90f, 120f, 150f }, Strength, 0.8f, Physical), bash, AbilityHit);
            var bashStun = Stun($"{folder}/Effects", "Shield Bash Stun", Scaled(Named(Levels(1f, 1.25f, 1.5f, 1.75f), "Seconds, by level")), bash);
            Costs(bash, folder, 40f, 45f, 50f, 55f);
            Cools(bash, folder, true, 9f, 8f, 7f, 6f);
            bash.TargetActivationRules.Add(new CheckRangeValidation { Tag = Reach });
            AiHint(bash, "InRange", 3.5f);
            Finish(bash, PickInRange(4f), Spend(Apply(bashBlow, bashStun)));

            var cry = NewAbility(folder, "Rallying Cry", "Armor for the Vanguard and its allies near it, for 6 seconds. The armor is a Bonus, so the HUD shows it apart.", Kind.Spell);
            var rallied = NewEffect("Rallied", "Armor, as a Bonus, for 6 seconds.");
            Lasts(rallied, Fixed(6f));
            Changes(rallied, Armor, Scaled(Named(Levels(30f, 45f, 60f, 75f), "Armor, by level")));
            rallied.Definition.RetentionGroup = Tags.ADDITIONAL;
            rallied.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Affiliated;
            Seen(rallied, Buff);
            rallied.Cues.Add(WhileOn(GoldAura));
            rallied.Cues.Add(Word("Rallied", BuffGreen));
            rallied.LinkToProvider(cry);
            rallied = Save($"{folder}/Effects/Rallied", rallied);
            Costs(cry, folder, 50f);
            Cools(cry, folder, true, 16f, 15f, 14f, 13f);
            AiHint(cry, "InRange", 6f);
            Finish(cry, null, Spend(new AreaEffectTask { Radius = 6f, AroundCaster = true, Affects = EDemoAffects.Allies, Effects = new List<GameplayEffect> { rallied }, Layers = ~0 }));

            var skin = NewAbility(folder, "Iron Skin", "Immune to stuns for a while, and any stun on the Vanguard comes off. It can be cast while stunned.", Kind.Unstoppable);
            var iron = NewEffect("Iron Skin", "Immune to stuns, which Blocks Effects Tagged keeps off; the stun on it came off as it landed, by Removes Effects Tagged.");
            Lasts(iron, Scaled(Named(Levels(2.5f, 3f, 3.5f, 4f), "Seconds, by level")));
            var tags = iron.Tags;
            tags.BlocksEffectsTagged.Add(Stunned);
            tags.RemovesEffectsTagged.Add(Stunned);
            tags.RemovesEffectsTagged.Add(Slowed);
            iron.Tags = tags;
            Seen(iron, Buff);
            iron.Cues.Add(WhileOn(SteelAura));
            iron.Cues.Add(Word("Iron Skin", BuffGreen));
            iron.LinkToProvider(skin);
            iron = Save($"{folder}/Effects/Iron Skin", iron);
            Costs(skin, folder, 60f);
            Cools(skin, folder, true, 18f, 16f, 14f, 12f);
            AiHint(skin, "Stunned", 0f);
            Finish(skin, null, Spend(Apply(iron)));

            VanguardKit.AddRange(new[] { strike, bash, cry, skin });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The Ranger: Agility, ranged, physical
        // ═══════════════════════════════════════════════════════════════════════════

        private static void MakeRanger()
        {
            const string hero = "Ranger", folder = "Abilities/Ranger";
            RangerKit.Clear();
            var spread = Named(Table(new RandomizedScaler { VarianceMode = EVarianceMode.Percentage, Variance = 0.15f, RollOncePerSpec = true }, 1f), "A spread of 15%");
            var arrow = RangedAttack(hero, "Arrow", "An arrow, physical, by Agility, with a spread to its damage.", PhysicalStrike, Physical, Agility, 1f, 24f,
                1.1f, 24f, new Color(0.55f, 0.85f, 0.45f), spread);

            var mark = NewAbility(folder, "Hunter's Mark", "Marks an enemy for 8 seconds: every physical blow it takes does a fifth more, by a Conditional scaler on each.", Kind.Spell);
            var marked = NewEffect("Marked", "Takes a fifth more physical damage: every physical blow's Conditional scaler reads Status.Marked.");
            Lasts(marked, Scaled(Named(Levels(6f, 7f, 8f, 9f), "Seconds, by level")));
            marked.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            Seen(marked, Debuff);
            Grants(marked, Marked);
            marked.Cues.Add(Word("Marked", DebuffRed));
            marked.LinkToProvider(mark);
            marked = Save($"{folder}/Effects/Marked", marked);
            Costs(mark, folder, 30f);
            Cools(mark, folder, true, 12f, 11f, 10f, 9f);
            AiHint(mark, "InRange", 10f);
            Finish(mark, PickTarget(), Spend(Apply(marked)));

            var volley = NewAbility(folder, "Volley", "Looses an arrow at each enemy around a point; each may slow. A task sequence looses them, reading the arrow, the radius and how many from the ability's Local Data.", Kind.Spell);
            var volleyBlow = Blow($"{folder}/Effects", "Volley Damage", "A volley arrow's blow: physical, by Agility.", PhysicalStrike,
                Damage(new[] { 40f, 55f, 70f, 85f }, Agility, 0.6f, Physical), volley, AbilityHit);
            var slow = NewEffect("Slowed", "Walks slower for 2 seconds. Half the volley's arrows slow, by Chance To Apply.");
            Lasts(slow, Fixed(2f));
            Changes(slow, MoveSpeed, Fixed(-2f));
            slow.ImpactSpecification.ChanceToApply = Fixed(0.5f);
            slow.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            Seen(slow, Debuff);
            Grants(slow, Slowed);
            slow.Cues.Add(Word("Slowed", DebuffRed));
            slow.LinkToProvider(volley);
            slow = Save($"{folder}/Effects/Slowed", slow);
            var volleyArrow = Projectile("Volley Arrow", 20f, 0.3f, new Color(0.7f, 0.95f, 0.5f), volleyBlow, slow);
            Costs(volley, folder, 60f);
            Cools(volley, folder, true, 14f, 13f, 12f, 11f);
            AiHint(volley, "InRange", 9f);
            volley.LocalData.Add(Local(DemoSequences.VolleyArrow, volleyArrow));
            volley.LocalData.Add(Local(DemoSequences.VolleyRadius, 3.5f));
            volley.LocalData.Add(Local(DemoSequences.VolleyArrows, 5));
            Finish(volley, PickPoint(), Spend(Sequence(nameof(DemoSequences.Volley))));

            var tumble = NewAbility(folder, "Tumble", "Rolls a short way toward a point, untargetable for half a second. The AI rolls away.", Kind.Spell);
            var tumbling = NewEffect("Tumbling", "Untargetable while it rolls: the targeting tasks' requirements avoid State.Untargetable.");
            Lasts(tumbling, Fixed(0.5f));
            Seen(tumbling, Buff);
            Grants(tumbling, Untargetable);
            tumbling.LinkToProvider(tumble);
            tumbling = Save($"{folder}/Effects/Tumbling", tumbling);
            Costs(tumble, folder, 30f);
            Cools(tumble, folder, true, 10f, 9f, 8f, 7f);
            AiHint(tumble, "Low", 0f);
            Finish(tumble, PickPoint(), Spend(OnSelf(tumbling), new DashTask { Distance = 5f, Duration = 0.25f, AwayFromTarget = false }));

            RangerKit.AddRange(new[] { arrow, mark, volley, tumble });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The Pyromancer: Intelligence, ranged, magic
        // ═══════════════════════════════════════════════════════════════════════════

        private static GroundFireProcess FlameWallFire;

        private static void MakePyromancer()
        {
            const string hero = "Pyromancer", folder = "Abilities/Pyromancer";
            PyromancerKit.Clear();
            var ember = RangedAttack(hero, "Ember", "A mote of fire, magic, by Intelligence.", MagicStrike, Magic, Intelligence, 0.9f, 20f, 1.2f, 16f, new Color(1f, 0.55f, 0.2f));

            var fireball = NewAbility(folder, "Fireball", "A fireball: magic damage, and a stack of Burning, which bursts as Flashpoint at its sixth.", Kind.Spell);
            BurningEffect.LinkToProvider(fireball);
            Dirty(BurningEffect);
            FlashpointEffect.LinkToProvider(fireball);
            Dirty(FlashpointEffect);
            var fireballBlow = Blow($"{folder}/Effects", "Fireball Damage", "Fireball's blow: magic, by Intelligence.", MagicStrike,
                Damage(new[] { 70f, 100f, 130f, 160f }, Intelligence, 0.7f, Magic), fireball, AbilityHit);
            var fireballShot = Projectile("Fireball", 14f, 0.6f, new Color(1f, 0.4f, 0.1f), fireballBlow, BurningEffect);
            Costs(fireball, folder, 50f);
            Cools(fireball, folder, true, 6f, 5.5f, 5f, 4.5f);
            AiHint(fireball, "InRange", 10f);
            Finish(fireball, PickTarget(), Spend(Wait(200), Spawn(fireballShot, CreateMonoProcessAbilityTask.ESpawnPoint.Caster)));

            var wall = NewAbility(folder, "Flame Wall", "Sets the ground alight for 4 seconds: whoever of the other side stands in it burns each second. The fire carries its radius, lifetime, pace and heat as attributes of its own, its radius and heat by the ability's level, and its burns read its heat.", Kind.Spell);
            FlameWallFire = Prefab<GroundFireProcess>("Flame Wall Fire", (host, fire) =>
            {
                fire.ProcessLifecycle = EProcessLifecycle.SelfTerminating;
                fire.ProcessTiming = EProcessStepTiming.None;
                fire.RadiusRole = FireRadius;
                fire.LifetimeRole = FireLifetime;
                fire.IntervalRole = FireInterval;
                fire.Radius = 2.5f;
                fire.Lifetime = 4f;
                fire.Interval = 1f;
                fire.Effects = new List<GameplayEffect> { BurningEffect };
                fire.Affects = EDemoAffects.Enemies;
                fire.Layers = ~0;
                fire.AttributePairs = new List<TagAttributePair>();
                fire.Attributes = new List<ProcessAttribute>
                {
                    new() { Attribute = AreaRadius, Role = FireRadius, StartsFrom = EProcessAttributeStart.Own, Value = Scaled(Named(Levels(2.5f, 3f, 3.5f, 4f), "Radius, by Flame Wall's level")) },
                    new() { Attribute = Lifetime, Role = FireLifetime, StartsFrom = EProcessAttributeStart.Own, Value = Fixed(4f) },
                    new() { Attribute = TickInterval, Role = FireInterval, StartsFrom = EProcessAttributeStart.Own, Value = Fixed(1f) },
                    new() { Attribute = Heat, Role = FireHeat, StartsFrom = EProcessAttributeStart.Own, Value = Scaled(Named(Levels(1f, 1.25f, 1.5f, 1.75f), "Heat, by Flame Wall's level")) },
                };
                var look = new GameObject("Fire");
                look.transform.SetParent(host.transform, false);
                var shape = look.AddComponent<DemoShape>();
                shape.Shape = PrimitiveType.Cylinder;
                shape.Size = new Vector3(1f, 0.02f, 1f);
                shape.Color = new Color(1f, 0.42f, 0.12f);
                fire.Look = look.transform;
            });
            Costs(wall, folder, 70f);
            Cools(wall, folder, true, 16f, 15f, 14f, 13f);
            AiHint(wall, "InRange", 9f);
            Finish(wall, PickPoint(), Spend(Spawn(FlameWallFire, CreateMonoProcessAbilityTask.ESpawnPoint.Target)));

            var combustion = NewAbility(folder, "Combustion", "Pure damage to an enemy; if it's burning, the burning ignites for more, and goes. A stage condition on the target's Status.Burning picks the second stage.", Kind.Spell);
            var blast = Blow($"{folder}/Effects", "Combustion", "Combustion's blow: pure, by Intelligence.", PureStrike,
                Damage(new[] { 50f, 75f, 100f, 125f }, Intelligence, 0.5f, Pure), combustion, AbilityHit);
            var ignite = Blow($"{folder}/Effects", "Ignition", "Combustion on a burning enemy: more pure damage, and the burning comes off, by Removes Effects Tagged.", PureStrike,
                Damage(new[] { 80f, 120f, 160f, 200f }, null, 0f, Pure), combustion, AbilityHit);
            var igniteTags = ignite.Tags;
            igniteTags.RemovesEffectsTagged = new List<Tag> { Burning };
            ignite.Tags = igniteTags;
            ignite.Cues.Add(Word("Ignited!", FireOrange));
            ignite.Cues.Add(Burst(FireBurst, 0.5f));
            Settle(Dirty(ignite));
            Costs(combustion, folder, 80f);
            Cools(combustion, folder, true, 12f, 11f, 10f, 9f);
            AiHint(combustion, "InRange", 9f);
            Finish(combustion, PickTarget(),
                Spend(Apply(blast)),
                When(EAbilityStageConditionSubject.Target, Burning, false, Apply(ignite)));

            PyromancerKit.AddRange(new[] { ember, fireball, wall, combustion });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The Shade: Agility, melee, pure
        // ═══════════════════════════════════════════════════════════════════════════

        private static GameplayEffect ShadowMend;

        private static void MakeShade()
        {
            const string hero = "Shade", folder = "Abilities/Shade";
            ShadeKit.Clear();

            var cut = MeleeAttack(hero, "Cut", "A melee blow, physical, by Agility. Empowered, it deals pure damage instead, by a stage condition on the caster's State.Empowered.", Agility, 1.1f, 28f, 0.9f, out var cutBlow);
            var empoweredCut = Blow($"{folder}/Effects", "Empowered Cut", "An empowered cut: pure, by Agility, and the empowerment's spent.", PureStrike,
                Damage(new[] { 60f, 80f, 100f, 120f }, Agility, 1f, Pure, attack: true), cut, AttackHit);
            var spend = NewEffect("Spend Empowerment", "Takes the empowerment off as the empowered cut lands, by Removes Effects Tagged.");
            var spendTags = spend.Tags;
            spendTags.RemovesEffectsTagged.Add(Empowered);
            spend.Tags = spendTags;
            spend.LinkToProvider(cut);
            spend = Save($"{folder}/Effects/Spend Empowerment", spend);
            Finish(cut, PickTarget(),
                Spend(Wait(120)),
                When(EAbilityStageConditionSubject.Caster, Empowered, true, Apply(cutBlow)),
                When(EAbilityStageConditionSubject.Caster, Empowered, false, Apply(empoweredCut), OnSelf(spend)));

            var step = NewAbility(folder, "Shadow Step", "Blinks behind an enemy, and empowers the next cut. If the enemy slips away first, an Inject task ends the step before the empowerment.", Kind.Spell);
            var empowered = NewEffect("Empowered", "The next cut deals pure damage.");
            Lasts(empowered, Fixed(5f));
            Seen(empowered, Buff);
            Grants(empowered, Empowered);
            empowered.Cues.Add(Word("Empowered", BuffGreen));
            empowered.LinkToProvider(step);
            empowered = Save($"{folder}/Effects/Empowered", empowered);
            Costs(step, folder, 40f, 40f, 45f, 45f);
            Cools(step, folder, true, 11f, 10f, 9f, 8f);
            step.Tags.ActiveGrantedTags.Add(Attacking);
            AiHint(step, "InRange", 8f);
            Finish(step, PickInRange(8f),
                Spend(new BlinkTask { Reach = 8f, Behind = 1.2f }),
                When(EAbilityStageConditionSubject.Target, Untargetable, false, new InjectAbilityTask { Injection = new InterruptSequenceInjection() }),
                Stage(OnSelf(empowered)));

            var eviscerate = NewAbility(folder, "Eviscerate", "Pure damage that grows with the enemy's missing health, capped at 400, for an enemy below half its health. A target below a tenth after it is finished.", Kind.Spell);
            var missing = Named(Group((Read(Health, ESourceTarget.Target, EAttributeTargetBinary.Base), Add),
                (Times(Read(Health, ESourceTarget.Target), -1f), Add)), "The target's missing health");
            var capped = new ClampedScaler
            {
                Name = "Capped at 400", MinValue = 0f, MaxValue = 400f, Behaviours = new List<AbstractScalerBehaviour>(),
                Inner = Named(Group((Named(Levels(80f, 110f, 140f, 170f), "Base, by level"), Add), (Named(Times(missing, 0.4f), "0.4 × missing health"), Add)), "Before the cap"),
            };
            var eviscerateBlow = Blow($"{folder}/Effects", "Eviscerate Damage", "Eviscerate's blow: pure, its base plus 0.4 × the target's missing health, capped at 400; and a finish below a tenth, by the effect's own worker.", PureStrike,
                Negated(Named(Group((capped, Add), (DifficultyScaler(), Multiply)), "Damage")), eviscerate, AbilityHit);
            eviscerateBlow.Workers.Add(new ExecuteEffectWorker { Health = Health, Below = 0.1f });
            Settle(Dirty(eviscerateBlow));
            Costs(eviscerate, folder, 60f);
            Cools(eviscerate, folder, true, 10f, 9f, 8f, 7f);
            eviscerate.TargetActivationRules.Add(new AttributeValidation { Attribute = Health, Comparison = EComparisonOperator.LessThan, Value = 0.5f, ShareOfBase = true });
            eviscerate.Tags.ActiveGrantedTags.Add(Attacking);
            AiHint(eviscerate, "FoeLow", 3f);
            Finish(eviscerate, PickInRange(3.5f), Spend(Wait(100), Apply(eviscerateBlow)));

            var veil = NewAbility(folder, "Smoke Veil", "Invisible for a few seconds, or until the Shade attacks: the veil's removal requirements end it on State.Attacking. While veiled, a tag worker mends the Shade.", Kind.Spell);
            var veiled = NewEffect("Smoke Veil", "Invisible: the other side can't see or target it. It ends early as the Shade attacks.");
            Lasts(veiled, Scaled(Named(Levels(3.5f, 4f, 4.5f, 5f), "Seconds, by level")));
            Seen(veiled, Buff);
            Grants(veiled, Invisible);
            veiled.TargetRequirements.RemovalRequirements = Require(Attacking);
            veiled.Cues.Add(WhileOn(SmokeAura));
            veiled.Cues.Add(Word("Veiled", BuffGreen));
            veiled.LinkToProvider(veil);
            veiled = Save($"{folder}/Effects/Smoke Veil", veiled);
            var mend = NewEffect("Shadow Mend", "Health each half second while the Shade is invisible, given and taken by its tag worker.");
            mend.DurationSpecification.DurationPolicy = EEffectDurationPolicy.Infinite;
            Ticks(mend, 0, 0.5f);
            mend.ImpactSpecification.AttributeTarget = Health;
            mend.ImpactSpecification.MagnitudeOperation = Fixed(8f);
            Seen(mend, Buff);
            ShadowMend = Save($"{folder}/Effects/Shadow Mend", mend);
            Costs(veil, folder, 70f);
            Cools(veil, folder, true, 18f, 17f, 16f, 15f);
            AiHint(veil, "Hurt", 0f);
            Finish(veil, null, Spend(Apply(veiled)));

            ShadeKit.AddRange(new[] { cut, step, eviscerate, veil });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The Oracle: Intelligence, ranged, magic, support
        // ═══════════════════════════════════════════════════════════════════════════

        private static void MakeOracle()
        {
            const string hero = "Oracle", folder = "Abilities/Oracle";
            OracleKit.Clear();
            var spark = RangedAttack(hero, "Spark", "A spark, magic, by Intelligence.", MagicStrike, Magic, Intelligence, 0.9f, 18f, 1.2f, 18f, new Color(0.5f, 0.85f, 1f));

            var drain = NewAbility(folder, "Drain Life", "Channels for 3 seconds: magic damage each second, and half of it back to the Oracle as Health, by the ability's own impact worker. A stun breaks the channel.", Kind.Spell);
            var pulse = Blow($"{folder}/Effects", "Drain Pulse", "One second of the drain: magic, by Intelligence. Its Damage.Drain context tag is what the ability's worker heals by.", MagicStrike,
                Damage(new[] { 25f, 35f, 45f, 55f }, Intelligence, 0.3f, Magic), drain, AbilityHit);
            var pulseTags = pulse.Tags;
            pulseTags.ContextTags.Add(DrainHit);
            pulse.Tags = pulseTags;
            Settle(Dirty(pulse));
            drain.WorkerGroup.ImpactWorkers.Add(new ShareBackWorker
            {
                Share = 0.5f, ImpactedAttribute = Health, AnyImpactType = true, ImpactType = new List<Tag>(), ImpactTarget = EAttributeTargetExpanded.CurrentOrBase,
                ImpactSign = ESignPolicy.Negative, AnyContextTag = false, ContextMatchingPolicy = EAnyAllPolicy.Any,
                ImpactAbilityContextTags = new List<Tag> { DrainHit }, WorkAttribute = Health, WorkImpactType = new List<Tag>(), WorkSignPolicy = ESignPolicy.Positive,
            });
            drain.Tags.ActiveGrantedTags.Add(Channeling);
            drain.Tags.TagRequirements.WhileActiveRequirements = Avoid(Stunned, Silenced);
            Costs(drain, folder, 80f);
            Cools(drain, folder, true, 12f, 11f, 10f, 9f);
            AiHint(drain, "InRange", 8f);
            Finish(drain, PickInRange(8.5f), Spend(new ApplyEffectsAbilityTask { Effects = new List<GameplayEffect> { pulse, pulse, pulse }, DelayBetweenApplicationMs = 1000 },
                new ChannelAbilityTask { ChannelDuration = 3f }));

            var hex = NewAbility(folder, "Hex", "Hexes an enemy for 6 seconds: lower Magic Resistance, a burst of magic damage as it lands by an Additional Impact, and a silence it contains.", Kind.Spell);
            var silence = NewEffect("Silenced", "Can't cast spells: every spell's requirements avoid State.Silenced.");
            Lasts(silence, Scaled(Named(Levels(1.5f, 2f, 2.5f, 3f), "Seconds, by level")));
            silence.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            Seen(silence, Debuff);
            Grants(silence, Silenced);
            silence.Cues.Add(Word("Silenced", DebuffRed));
            silence.LinkToProvider(hex);
            silence = Save($"{folder}/Effects/Silenced", silence);
            var hexed = NewEffect("Hexed", "Lower Magic Resistance while it lasts; a burst of magic damage as it lands, its Additional Impact; and a silence, its contained effect.");
            Lasts(hexed, Fixed(6f));
            Changes(hexed, MagicResist, Negated(Named(Levels(0.1f, 0.15f, 0.2f, 0.25f), "Magic Resistance, by level")));
            hexed.ImpactSpecification.AffiliationPolicy = EAffiliationPolicy.Unaffiliated;
            hexed.ImpactSpecification.AdditionalImpacts.Add(new AttributeImpactEntry
            {
                AttributeTarget = Health, Delivery = EImpactDelivery.OnApply, TargetSlot = EAttributeTarget.Current, Operation = ECalculationOperation.Add,
                Magnitude = Damage(new[] { 40f, 60f, 80f, 100f }, null, 0f, Magic), ImpactTypes = new List<Tag> { Magic },
            });
            hexed.ImpactSpecification.Packets = new[] { new ContainedEffectPacket { Policy = EApplyTickRemove.OnApply, ContainedEffect = silence } };
            Seen(hexed, Debuff);
            Context(hexed, AbilityHit);
            Grants(hexed, Hexed);
            hexed.Cues.Add(Word("Hexed", DebuffRed));
            hexed.LinkToProvider(hex);
            hexed = Save($"{folder}/Effects/Hexed", hexed);
            Costs(hex, folder, 70f);
            Cools(hex, folder, true, 14f, 13f, 12f, 11f);
            AiHint(hex, "InRange", 9f);
            Finish(hex, PickTarget(), Spend(Apply(hexed)));

            var renewal = NewAbility(folder, "Renewal", "Cleanses the Oracle's debuffs, then heals it in five pulses, one a second. It loads its cleanse and its pulse with Set Effects, and a task sequence applies them.", Kind.Spell);
            var renewalPulse = NewEffect("Renewal Pulse", "One of Renewal's five heals: Health, by level and Intelligence.");
            renewalPulse.ImpactSpecification.AttributeTarget = Health;
            renewalPulse.ImpactSpecification.MagnitudeOperation = Healing(new[] { 20f, 30f, 40f, 50f }, Intelligence, 0.25f);
            renewalPulse.Cues.Add(Burst(HealGlow, 0.6f));
            renewalPulse.LinkToProvider(renewal);
            renewalPulse = Save($"{folder}/Effects/Renewal Pulse", renewalPulse);
            var cleanse = NewEffect("Renewal Cleanse", "Takes every debuff off, by Removes Effects Tagged Kind.Debuff.");
            var cleanseTags = cleanse.Tags;
            cleanseTags.RemovesEffectsTagged.Add(Debuff);
            cleanse.Tags = cleanseTags;
            cleanse.Cues.Add(Word("Cleansed", BuffGreen));
            cleanse.LinkToProvider(renewal);
            cleanse = Save($"{folder}/Effects/Renewal Cleanse", cleanse);
            renewal.Behaviour.AssetLoaders = new List<AbstractSetAssetsTask>
            {
                new SetEffectsTask { Tag = DemoSequences.RenewalCleanse, Effects = new[] { cleanse } },
                new SetEffectsTask { Tag = DemoSequences.RenewalPulse, Effects = new[] { renewalPulse } },
            };
            Costs(renewal, folder, 90f);
            Cools(renewal, folder, true, 20f, 18f, 16f, 14f);
            AiHint(renewal, "Hurt", 0f);
            Finish(renewal, null, Spend(Sequence(nameof(DemoSequences.Renewal))));

            OracleKit.AddRange(new[] { spark, drain, hex, renewal });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What every hero has besides
        // ═══════════════════════════════════════════════════════════════════════════

        private static Ability Passive;

        private static void MakePassive()
        {
            const string folder = "Abilities/Shared";
            var passive = NewAbility(folder, "Second Wind", "Every hero's passive: it activates as the hero comes in, and gives it Regeneration for good.", Kind.Unstoppable, maxLevel: 1);
            passive.Definition.ActivateImmediately = true;
            passive.SourceActivationRules = new List<IAbilityValidationRule> { new IsAliveValidation() };
            var regeneration = NewEffect("Regeneration", "Health and Mana back each second, for good: its main impact restores Health, and an Additional Impact Mana, each tick.");
            regeneration.DurationSpecification.DurationPolicy = EEffectDurationPolicy.Infinite;
            Ticks(regeneration, 0, 1f);
            regeneration.ImpactSpecification.AttributeTarget = Health;
            regeneration.ImpactSpecification.MagnitudeOperation = Fixed(3f);
            regeneration.ImpactSpecification.AdditionalImpacts.Add(new AttributeImpactEntry
            {
                AttributeTarget = Mana, Delivery = EImpactDelivery.Periodic, TargetSlot = EAttributeTarget.Current, Operation = ECalculationOperation.Add,
                Magnitude = Fixed(4f), ImpactTypes = new List<Tag>(),
            });
            regeneration.LinkToProvider(passive);
            Regeneration = Save($"{folder}/Effects/Regeneration", regeneration);
            Finish(passive, null, Stage(Apply(Regeneration)));
            Passive = passive;
        }
    }
}
