using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FarEmerald.PlayForge;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;

namespace FarEmerald.PlayForge.Tests
{
    /// <summary>
    /// Golden tests for the impact-model overhaul (IMPACT_MODEL_REFACTOR.md, Phase 0).
    ///
    /// Every scenario must pass. RunScenario's targetSemantics flag reports a failure as
    /// expected-red instead; no scenario uses it, and none may (SP-16 in Docs/REFACTOR_PLAN.md).
    /// S13 onward carry the tracker ID they pin in their names.
    ///
    /// Run from the menu (PlayForge → Run Golden Tests) or headless; RunAll exits batch mode
    /// itself, with 1 on failure:
    ///   Unity -batchmode -projectPath &lt;proj&gt;
    ///         -executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll
    ///
    /// A custom runner rather than the Unity Test Framework, which the asmdefs now allow;
    /// moving to it is planned for Phase 2 (SP-18).
    /// </summary>
    public static class PlayForgeGoldenTests
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // RUNNER
        // ═══════════════════════════════════════════════════════════════════════════

        private class Result
        {
            public string Name;
            public bool Passed;
            public bool TargetSemantics; // red-until-refactor scenarios
            public string Detail;
        }

        private static readonly List<Result> results = new();
        private static Result current;

        [MenuItem("PlayForge/Run Golden Tests")]
        public static void RunFromMenu() => RunAll();

        public static void RunAll()
        {
            results.Clear();

            RunScenario(nameof(S1_ContinuousMultiply_TwoBuffs_Multiplicative), S1_ContinuousMultiply_TwoBuffs_Multiplicative);
            RunScenario(nameof(S2_ContinuousMultiply_TwoBuffs_AdditiveBonus), S2_ContinuousMultiply_TwoBuffs_AdditiveBonus);
            RunScenario(nameof(S3_ContinuousAdd_ItemStyle_RegisterRemove), S3_ContinuousAdd_ItemStyle_RegisterRemove);
            RunScenario(nameof(S4_HoT_TicksArePermanentAfterExpiry), S4_HoT_TicksArePermanentAfterExpiry);
            RunScenario(nameof(S5_InstantDamage_ClampAndHistorySurvival), S5_InstantDamage_ClampAndHistorySurvival);
            RunScenario(nameof(S6_CommittedHistoryJoinsBaseline), S6_CommittedHistoryJoinsBaseline);
            RunScenario(nameof(S7_Stacking_MagnitudePerStack), S7_Stacking_MagnitudePerStack);
            RunScenario(nameof(S8_Regulation_DerivedAttributeFollowsSource), S8_Regulation_DerivedAttributeFollowsSource);
            RunScenario(nameof(S9_NullTargetEffect_TagGrantOnly), S9_NullTargetEffect_TagGrantOnly);
            RunScenario(nameof(S10_ReplaceOldContainer_Idempotent), S10_ReplaceOldContainer_Idempotent);
            RunScenario(nameof(S11_GroupAttribution_BaseVsBonus), S11_GroupAttribution_BaseVsBonus);
            RunScenario(nameof(S12_AbilityInspector_PerLevelSeries), S12_AbilityInspector_PerLevelSeries);
            RunScenario(nameof(S13_GAS_10_OngoingRequirement_PausesPeriodicTicks), S13_GAS_10_OngoingRequirement_PausesPeriodicTicks);
            RunScenario(nameof(S14_GAS_11_AbilitySourceTagRequirements_BlockActivation), S14_GAS_11_AbilitySourceTagRequirements_BlockActivation);
            RunScenario(nameof(S15_GAS_02_PreChangeWorkers_AdjustTheCommittedDelta), S15_GAS_02_PreChangeWorkers_AdjustTheCommittedDelta);
            RunScenario(nameof(S16_GAS_12_RawDelta_MovesBaseAndCurrentTogether), S16_GAS_12_RawDelta_MovesBaseAndCurrentTogether);
            RunScenario(nameof(S17_GAS_P1_IdleFrame_AllocatesNothing), S17_GAS_P1_IdleFrame_AllocatesNothing);
            RunScenario(nameof(S18_GAS_P2_TagChecks_AllocateNothing), S18_GAS_P2_TagChecks_AllocateNothing);
            RunScenario(nameof(S19_TS_04_BrokenAbility_IsReported), S19_TS_04_BrokenAbility_IsReported);
            RunScenario(nameof(S20_PC_01_ThrowingStep_DoesNotStallTheFrame), S20_PC_01_ThrowingStep_DoesNotStallTheFrame);
            RunScenario(nameof(S21_TS_03_RepeatedActivation_ReusesItsCompiledBehaviour), S21_TS_03_RepeatedActivation_ReusesItsCompiledBehaviour);
            RunScenario(nameof(S22_GAS_13_SystemInitialized_ReachesEarlySubscribers), S22_GAS_13_SystemInitialized_ReachesEarlySubscribers);
            RunScenario(nameof(S23_TS_01_Resume_LeavesTimeScaleAlone), S23_TS_01_Resume_LeavesTimeScaleAlone);
            RunScenario(nameof(S24_GAS_03_GiveItem_LandsOnTheShelf), S24_GAS_03_GiveItem_LandsOnTheShelf);
            RunScenario(nameof(S25_GAS_01_RuntimeGAS_RunsItsEndOfFrame), S25_GAS_01_RuntimeGAS_RunsItsEndOfFrame);

            Report();
        }

        private static void RunScenario(string name, System.Action scenario, bool targetSemantics = false)
        {
            current = new Result { Name = name, Passed = true, TargetSemantics = targetSemantics, Detail = "" };
            try
            {
                scenario();
            }
            catch (System.Exception ex)
            {
                current.Passed = false;
                current.Detail += $"\n    EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            }
            results.Add(current);
            current = null;
        }

        private static void Report()
        {
            int failed = 0, expectedRed = 0;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("══════════ PlayForge Golden Tests ══════════");
            foreach (var r in results)
            {
                string tag = r.Passed ? "PASS" : (r.TargetSemantics ? "RED (target semantics, expected until refactor)" : "FAIL");
                if (!r.Passed && r.TargetSemantics) expectedRed++;
                else if (!r.Passed) failed++;
                sb.AppendLine($"  [{tag}] {r.Name}{r.Detail}");
            }
            sb.AppendLine($"══════════ {results.Count} scenarios: {results.Count - failed - expectedRed} pass, {failed} FAIL, {expectedRed} expected-red ══════════");

            if (failed > 0) Debug.LogError(sb.ToString());
            else Debug.Log(sb.ToString());

            if (Application.isBatchMode) EditorApplication.Exit(failed > 0 ? 1 : 0);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ASSERTIONS
        // ═══════════════════════════════════════════════════════════════════════════

        private static void AssertApprox(string what, float actual, float expected, float tol = 0.001f)
        {
            if (Mathf.Abs(actual - expected) > tol)
            {
                current.Passed = false;
                current.Detail += $"\n    {what}: expected {expected}, got {actual}";
            }
        }

        private static void AssertTrue(string what, bool condition)
        {
            if (!condition)
            {
                current.Passed = false;
                current.Detail += $"\n    {what}: expected true";
            }
        }

        private static void AssertValue(RuntimeGameplayAbilitySystem gas, ForgeAttribute attr, string what, float expectedCurrent, float? expectedBase = null)
        {
            AssertTrue($"{what}: attribute defined", gas.TryGetAttributeValue(attr, out var v));
            AssertApprox($"{what} (Current)", v.CurrentValue, expectedCurrent);
            if (expectedBase.HasValue) AssertApprox($"{what} (Base)", v.BaseValue, expectedBase.Value);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // FIXTURES
        // ═══════════════════════════════════════════════════════════════════════════

        private static int uniqueCounter;
        private static string Unique(string stem) => $"{stem}_{uniqueCounter++}";

        private static ForgeAttribute MakeAttribute(string name)
        {
            var a = ScriptableObject.CreateInstance<ForgeAttribute>();
            a.name = name;
            a.Name = name;
            a.Textures = new List<TextureItem>();
            return a;
        }

        private static AttributeSetElement MakeElement(
            ForgeAttribute attribute, float current, float @base,
            EAttributeOverflowPolicy overflow = EAttributeOverflowPolicy.Unlimited,
            AttributeMagnitudeSpec currentSpecOverride = null,
            AttributeMagnitudeSpec baseSpecOverride = null,
            bool linkCurrentToBase = false)
        {
            return new AttributeSetElement
            {
                Attribute = attribute,
                Current = currentSpecOverride ?? new AttributeMagnitudeSpec { Magnitude = current, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                Base = baseSpecOverride ?? new AttributeMagnitudeSpec { Magnitude = @base, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                LinkCurrentToBase = linkCurrentToBase,
                Overflow = new AttributeOverflowData { Policy = overflow },
                RetentionGroup = Tags.DEFAULT,
                Constraints = new AttributeConstraints { AutoClamp = true, RoundingMode = EAttributeRoundingPolicy.None },
            };
        }

        private static AttributeSet MakeSet(params AttributeSetElement[] elements)
        {
            var set = ScriptableObject.CreateInstance<AttributeSet>();
            set.name = Unique("TestSet");
            set.Name = set.name;
            set.AssetTag = Tag.Generate(Unique("Test.Set"));
            set.Attributes = new List<AttributeSetElement>(elements);
            set.SubSets = new List<AttributeSet>();
            set.WorkerGroup = new StandardWorkerGroup();
            return set;
        }

        private static RuntimeGameplayAbilitySystem MakeGAS(AttributeSet set)
        {
            var identity = new RuntimeEntityIdentity
            {
                Name = Unique("TestEntity"),
                AssetTag = Tag.Generate(Unique("Test.Entity")),
                GrantedTags = new List<Tag>(),
                ContextTags = new List<Tag>(),
                Affiliation = new List<Tag>(),
                AttributeSet = set,
                MaxAbilitiesOperation = new ScalerIntegerMagnitudeOperation { Magnitude = 8 },
                MaxItemsOperation = new ScalerIntegerMagnitudeOperation { Magnitude = 8 },
                MaxEquippedItemsOperation = new ScalerIntegerMagnitudeOperation { Magnitude = 8 },
            };
            return new RuntimeGameplayAbilitySystem(identity);
        }

        private class EffectConfig
        {
            public EEffectDurationPolicy Policy = EEffectDurationPolicy.Infinite;
            public ForgeAttribute Target;
            public ECalculationOperation Op = ECalculationOperation.Add;
            public EAttributeTarget Slot = EAttributeTarget.Current;
            public EMultiplyBehaviour MultiplyBehaviour = EMultiplyBehaviour.Multiplicative;
            public float Magnitude;
            public AbstractScaler MagnitudeScaler;
            public EMagnitudeOperation MagnitudeOp = EMagnitudeOperation.UseMagnitude;
            public bool ReverseOnRemoval;

            public float Duration;
            public bool PeriodicTicks;
            public int Ticks;
            public bool TickOnApplication;
            public float FixedDeltaTime; // >0 → deterministic per-frame time via ConstantScaler

            public EEffectReApplicationPolicy Reapply = EEffectReApplicationPolicy.AppendNewContainer;
            public EEffectInteractionPolicy Interaction = EEffectInteractionPolicy.DoNothing;
            public EEffectStackPolicy StackPolicy = EEffectStackPolicy.StacksShareOneDuration;
            public int StackAmount = 1;

            public List<Tag> GrantedTags = new();
        }

        private static GameplayEffect MakeEffect(string name, EffectConfig cfg)
        {
            var fx = ScriptableObject.CreateInstance<GameplayEffect>();
            fx.name = name;
            fx.Definition = new GameplayEffectDefinition { Name = name, Textures = new List<TextureItem>() };
            fx.Tags = new GameplayEffectTags
            {
                AssetTag = Tag.Generate(Unique($"Test.Effect.{name}")),
                ContextTags = new List<Tag>(),
                GrantedTags = cfg.GrantedTags,
            };
            fx.Workers = new List<AbstractEffectWorker>();
            fx.SourceRequirements = EmptyRequirements();
            fx.TargetRequirements = EmptyRequirements();

            fx.ImpactSpecification = new GameplayEffectImpact
            {
                AttributeTarget = cfg.Target,
                TargetImpact = cfg.Slot,
                ImpactOperation = cfg.Op,
                MultiplyBehaviour = cfg.MultiplyBehaviour,
                AffiliationPolicy = EAffiliationPolicy.AlwaysAllow,
                ImpactTypes = new List<Tag>(),
                ReverseImpactOnRemoval = cfg.ReverseOnRemoval,
                MagnitudeOperation = new ScalerMagnitudeOperation
                {
                    Magnitude = cfg.Magnitude,
                    Scaler = cfg.MagnitudeScaler,
                    RealMagnitude = cfg.MagnitudeOp,
                },
                Packets = System.Array.Empty<ContainedEffectPacket>(),
            };

            fx.DurationSpecification = new GameplayEffectDuration
            {
                DurationPolicy = cfg.Policy,
                DurationOperation = new ScalerMagnitudeOperation { Magnitude = cfg.Duration, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                EnablePeriodicTicks = cfg.PeriodicTicks,
                TickOnApplication = cfg.TickOnApplication,
                TicksOperation = new ScalerIntegerMagnitudeOperation { Magnitude = cfg.Ticks },
                TickIntervalOperation = new ScalerMagnitudeOperation { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                AdditionalExecuteTicksOperation = new ScalerIntegerMagnitudeOperation { Magnitude = 0 },
                StackAmountOperation = new ScalerIntegerMagnitudeOperation { Magnitude = cfg.StackAmount },
                StackingConfigs = new List<AbstractStackingConfig>(),
                ReApplicationPolicy = cfg.Reapply,
                ReApplicationInteraction = cfg.Interaction,
                StackPolicy = cfg.StackPolicy,
                RealDeltaTime = cfg.FixedDeltaTime > 0f ? EMagnitudeOperation.UseScaler : EMagnitudeOperation.AddScaler,
                DeltaTimeScaler = cfg.FixedDeltaTime > 0f ? new ConstantScaler { Value = cfg.FixedDeltaTime } : null,
            };

            return fx;
        }

        private static EffectTagRequirements EmptyRequirements()
        {
            // Non-null groups with no queries: application passes, removal never
            // self-triggers. (Null groups would flag immediate removal — see
            // EffectTagRequirements.CheckRemovalRequirements's null fallback.)
            return new EffectTagRequirements
            {
                ApplicationRequirements = new AvoidRequireTagGroup(),
                OngoingRequirements = new AvoidRequireTagGroup(),
                RemovalRequirements = new AvoidRequireTagGroup(),
            };
        }

        private static bool Apply(RuntimeGameplayAbilitySystem gas, GameplayEffect fx)
        {
            var origin = IEffectOrigin.GenerateSourceDerivation(gas);
            return gas.ApplyGameplayEffect(gas.GenerateEffectSpec(origin, fx));
        }

        private static void Remove(RuntimeGameplayAbilitySystem gas, GameplayEffect fx)
        {
            gas.RemoveGameplayEffect(fx);
            Frame(gas); // removal is deferred through FinishedEffects → next update
        }

        private static void Frame(RuntimeGameplayAbilitySystem gas, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                gas.WhenUpdate();
                gas.WhenLateUpdate();
            }
        }

        /// <summary>Test-only scaler: magnitude follows the container's stack count.</summary>
        private class StackCountScaler : AbstractScaler
        {
            public override void Initialize(IAttributeImpactCarrier deriv) { }
            public override float Evaluate(IAttributeImpactCarrier deriv) => 1f;
            public override float Evaluate(AbstractStackingEffectContainer container) => container.Stacks;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SCENARIOS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// THE motivating bug shape (plan §0), authored correctly as continuous modifiers:
        /// two ×1.2 buffs (distinct assets) on a 50 attribute compound to 72; removing the
        /// FIRST must land on 60 (recompute), never 62 (delta rollback).
        /// </summary>
        private static void S1_ContinuousMultiply_TwoBuffs_Multiplicative()
        {
            var attr = MakeAttribute("Damage");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 50, 50)));

            var buffA = MakeEffect("BuffA", new EffectConfig { Target = attr, Op = ECalculationOperation.Multiply, Magnitude = 1.2f });
            var buffB = MakeEffect("BuffB", new EffectConfig { Target = attr, Op = ECalculationOperation.Multiply, Magnitude = 1.2f });

            AssertValue(gas, attr, "initial", 50, 50);
            AssertTrue("apply A", Apply(gas, buffA));
            AssertValue(gas, attr, "after A", 60);
            AssertTrue("apply B", Apply(gas, buffB));
            AssertValue(gas, attr, "after B", 72);

            Remove(gas, buffA);
            AssertValue(gas, attr, "after removing A", 60);
            Remove(gas, buffB);
            AssertValue(gas, attr, "after removing B", 50);
        }

        /// <summary>AdditiveBonus multiply pooling: two +20% pool to +40%, and reverse cleanly.</summary>
        private static void S2_ContinuousMultiply_TwoBuffs_AdditiveBonus()
        {
            var attr = MakeAttribute("Damage");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 50, 50)));

            var cfg = new EffectConfig { Target = attr, Op = ECalculationOperation.Multiply, Magnitude = 1.2f, MultiplyBehaviour = EMultiplyBehaviour.AdditiveBonus };
            var buffA = MakeEffect("BuffA", cfg);
            var buffB = MakeEffect("BuffB", new EffectConfig { Target = attr, Op = ECalculationOperation.Multiply, Magnitude = 1.2f, MultiplyBehaviour = EMultiplyBehaviour.AdditiveBonus });

            Apply(gas, buffA);
            AssertValue(gas, attr, "after A", 60);
            Apply(gas, buffB);
            AssertValue(gas, attr, "after B (pooled +40%)", 70);

            Remove(gas, buffA);
            AssertValue(gas, attr, "after removing A", 60);
            Remove(gas, buffB);
            AssertValue(gas, attr, "after removing B", 50);
        }

        /// <summary>Item-style infinite +50 flat modifier: registered while present, exact reversal.</summary>
        private static void S3_ContinuousAdd_ItemStyle_RegisterRemove()
        {
            var attr = MakeAttribute("Health");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 100, 100)));

            var item = MakeEffect("ItemHealth", new EffectConfig
            {
                Target = attr, Op = ECalculationOperation.Add, Slot = EAttributeTarget.CurrentAndBase, Magnitude = 50,
            });

            Apply(gas, item);
            AssertValue(gas, attr, "equipped", 150, 150);
            Remove(gas, item);
            AssertValue(gas, attr, "unequipped", 100, 100);
        }

        /// <summary>
        /// "5 HP per second for 5 seconds": periodic commits are PERMANENT — the value
        /// stays after the effect expires naturally. Deterministic time via a UseScaler
        /// delta-time override (1 s per simulated frame).
        /// </summary>
        private static void S4_HoT_TicksArePermanentAfterExpiry()
        {
            var attr = MakeAttribute("Health");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 50, 100, EAttributeOverflowPolicy.ZeroToBase)));

            var hot = MakeEffect("Regen", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Durational,
                Target = attr, Op = ECalculationOperation.Add, Magnitude = 5,
                Duration = 5f, PeriodicTicks = true, Ticks = 5, FixedDeltaTime = 1f,
            });

            Apply(gas, hot);
            AssertValue(gas, attr, "on apply (no TickOnApplication)", 50);

            Frame(gas, 5); // 5 s → 5 ticks → +25, duration exhausted
            AssertValue(gas, attr, "after 5 ticks", 75);

            Frame(gas, 2); // expiry cleanup
            AssertValue(gas, attr, "after natural expiry — ticks are permanent", 75, 100);
        }

        /// <summary>
        /// Instant commits: clamped damage/heal history must survive unrelated recomputes
        /// (modifier add/remove on the same attribute) without resetting or drifting.
        /// </summary>
        private static void S5_InstantDamage_ClampAndHistorySurvival()
        {
            var attr = MakeAttribute("Health");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 100, 100, EAttributeOverflowPolicy.ZeroToBase)));

            var damage30 = MakeEffect("Damage30", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Instant, Target = attr, Op = ECalculationOperation.Add, Magnitude = -30,
            });
            var maxHp20 = MakeEffect("MaxHp20", new EffectConfig
            {
                Target = attr, Op = ECalculationOperation.Add, Slot = EAttributeTarget.Base, Magnitude = 20,
            });
            var heal50 = MakeEffect("Heal50", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Instant, Target = attr, Op = ECalculationOperation.Add, Magnitude = 50,
            });

            Apply(gas, damage30);
            AssertValue(gas, attr, "after 30 damage", 70, 100);

            Apply(gas, maxHp20);
            AssertValue(gas, attr, "max-HP buff must not touch damage history", 70, 120);

            Remove(gas, maxHp20);
            AssertValue(gas, attr, "buff removed — damage still applied", 70, 100);

            Apply(gas, heal50);
            AssertValue(gas, attr, "overheal clamps to Base", 100, 100);

            Apply(gas, damage30);
            AssertValue(gas, attr, "post-clamp history stays consistent", 70, 100);
        }

        /// <summary>
        /// Plan §2.2/§7.1: committed history joins the
        /// BASELINE, so transient % modifiers scale permanent gains. This pins today's answer to
        /// the refactor plan's open Decision 4 (GAS-15); change it with that decision:
        /// 50 +(instant ×1.2 → +10 committed) → baseline 60; ×1.5 modifier → 90 (not 85).
        /// </summary>
        private static void S6_CommittedHistoryJoinsBaseline()
        {
            var attr = MakeAttribute("Damage");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 50, 50)));

            var tome = MakeEffect("PermanentTome", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Instant, Target = attr, Op = ECalculationOperation.Multiply, Magnitude = 1.2f,
            });
            var aura = MakeEffect("Aura", new EffectConfig
            {
                Target = attr, Op = ECalculationOperation.Multiply, Magnitude = 1.5f,
            });

            Apply(gas, tome);
            AssertValue(gas, attr, "permanent ×1.2 commit", 60);

            Apply(gas, aura);
            AssertValue(gas, attr, "aura multiplies committed history: (50+10)×1.5", 90);

            Remove(gas, aura);
            AssertValue(gas, attr, "aura removed", 60);
        }

        /// <summary>Stack count feeds modifier magnitude (+10 per stack) and reverses fully.</summary>
        private static void S7_Stacking_MagnitudePerStack()
        {
            var attr = MakeAttribute("Armor");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 50, 50)));

            var stacking = MakeEffect("StackingBuff", new EffectConfig
            {
                Target = attr, Op = ECalculationOperation.Add,
                Magnitude = 10, MagnitudeScaler = new StackCountScaler(), MagnitudeOp = EMagnitudeOperation.MultiplyWithScaler,
                Reapply = EEffectReApplicationPolicy.StackExistingContainers,
                StackPolicy = EEffectStackPolicy.StacksShareOneDuration,
            });

            Apply(gas, stacking);
            AssertValue(gas, attr, "1 stack", 60);

            Apply(gas, stacking);
            AssertValue(gas, attr, "2 stacks", 70);

            Remove(gas, stacking);
            AssertValue(gas, attr, "all stacks removed", 50);
        }

        /// <summary>
        /// Regulation graph: B is blueprint-derived as 2 × A (cached attribute scaler,
        /// Current linked to Base). Modifying A recomputes B automatically; removal reverts.
        /// </summary>
        private static void S8_Regulation_DerivedAttributeFollowsSource()
        {
            var attrA = MakeAttribute("Strength");
            var attrB = MakeAttribute("AttackPower");

            var elementA = MakeElement(attrA, 10, 10);
            var elementB = MakeElement(attrB, 0, 0,
                baseSpecOverride: new AttributeMagnitudeSpec
                {
                    Magnitude = 2f,
                    RealMagnitude = EMagnitudeOperation.MultiplyWithScaler,
                    Scaling = new CachedAttributeScaler { CaptureAttribute = attrA },
                },
                linkCurrentToBase: true);

            var gas = MakeGAS(MakeSet(elementA, elementB));
            AssertValue(gas, attrB, "derived initial (2×10)", 20, 20);

            var strBuff = MakeEffect("StrBuff", new EffectConfig
            {
                Target = attrA, Op = ECalculationOperation.Add, Slot = EAttributeTarget.CurrentAndBase, Magnitude = 5,
            });

            Apply(gas, strBuff);
            AssertValue(gas, attrA, "source buffed", 15, 15);
            AssertValue(gas, attrB, "derived follows source (2×15)", 30, 30);

            Remove(gas, strBuff);
            AssertValue(gas, attrB, "derived reverts", 20, 20);
        }

        /// <summary>Durational effect with NO attribute target (pure tag grant): apply/remove without exceptions.</summary>
        private static void S9_NullTargetEffect_TagGrantOnly()
        {
            var attr = MakeAttribute("Unused");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 1, 1)));

            var grantedTag = Tag.Generate(Unique("Test.Status.Shielded"));
            var tagFx = MakeEffect("ShieldTag", new EffectConfig
            {
                Target = null, GrantedTags = new List<Tag> { grantedTag },
            });

            AssertTrue("apply tag-only effect", Apply(gas, tagFx));
            AssertTrue("tag granted while active", gas.GetTagWeight(grantedTag) > 0);

            Remove(gas, tagFx);
            // TagCache.GetWeight returns -1 for an absent tag (0 would be a present tag
            // at zero weight) — "gone" means weight <= 0.
            AssertTrue("tag gone after removal", gas.GetTagWeight(grantedTag) <= 0);
        }

        /// <summary>
        /// Per-group attribution. One health pool split
        /// into base (attribute-set root) and bonus (item-style modifier + regen commits)
        /// retention groups. Slices must sum to the total and answer "how much of this
        /// attribute comes from group X" for UI.
        /// </summary>
        private static void S11_GroupAttribution_BaseVsBonus()
        {
            var health = MakeAttribute("Health");
            var gas = MakeGAS(MakeSet(MakeElement(health, 100, 100)));

            var bonusGroup = Tag.Generate(Unique("Test.Group.Bonus"));

            var itemHp = MakeEffect("ItemHealth", new EffectConfig
            {
                Target = health, Op = ECalculationOperation.Add, Slot = EAttributeTarget.CurrentAndBase, Magnitude = 50,
            });
            itemHp.Definition.RetentionGroup = bonusGroup;

            Apply(gas, itemHp);
            AssertValue(gas, health, "total pool", 150, 150);

            var attrSystem = gas.GetAttributeSystem();
            AssertTrue("bonus slice query", attrSystem.TryGetGroupValue(health, bonusGroup, out var bonus));
            AssertApprox("bonus slice (Current)", bonus.CurrentValue, 50);
            AssertApprox("bonus slice (Base)", bonus.BaseValue, 50);

            AssertTrue("base slice query", attrSystem.TryGetGroupValue(health, Tags.DEFAULT, out var baseSlice));
            AssertApprox("base slice (Current)", baseSlice.CurrentValue, 100);

            float sum = 0f;
            foreach (var slice in attrSystem.GetBreakdown(health)) sum += slice.Value.CurrentValue;
            gas.TryGetAttributeValue(health, out var total);
            AssertApprox("breakdown sums to ActiveValue", sum, total.CurrentValue);

            Remove(gas, itemHp);
            attrSystem.TryGetGroupValue(health, bonusGroup, out var bonusAfter);
            AssertApprox("bonus slice after removal", bonusAfter.CurrentValue, 0);
        }

        /// <summary>
        /// Ability introspection: a HoT ability ("X HP per tick, 5 ticks over N seconds")
        /// must decipher to per-level series — 20/30/40/50 total healing over 3/4/5/6
        /// seconds across 4 levels — with correct role and target scope.
        /// </summary>
        private static void S12_AbilityInspector_PerLevelSeries()
        {
            var attr = MakeAttribute("Health");

            SimpleScaler MakeLevelScaler(params float[] values)
            {
                var scaler = new SimpleScaler
                {
                    LevelValues = values,
                    MaxLevel = values.Length,
                    Configuration = ELevelConfig.LockToLevelProvider,
                    Interpolation = EScalerInterpolation.Linear,
                };
                scaler.RegenerateCurve();
                return scaler;
            }

            var hot = MakeEffect("Regen", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Durational,
                Target = attr, Op = ECalculationOperation.Add,
                Magnitude = 0f, MagnitudeScaler = MakeLevelScaler(4f, 6f, 8f, 10f), MagnitudeOp = EMagnitudeOperation.UseScaler,
                Duration = 0f, PeriodicTicks = true, Ticks = 5,
            });
            hot.DurationSpecification.DurationOperation = new ScalerMagnitudeOperation
            {
                Magnitude = 0f,
                Scaler = MakeLevelScaler(3f, 4f, 5f, 6f),
                RealMagnitude = EMagnitudeOperation.UseScaler,
            };

            var ability = ScriptableObject.CreateInstance<Ability>();
            ability.name = "TestRegenAbility";
            ability.Definition = new AbilityDefinition { Name = "Regen Aura" };
            ability.StartingLevel = 1;
            ability.MaxLevel = 4;
            ability.Behaviour = new AbilityBehaviour
            {
                Targeting = null,
                UseImplicitTargeting = true,
                Stages = new List<AbilityTaskBehaviourStage>
                {
                    new()
                    {
                        Tasks = new List<AbstractAbilityTask>
                        {
                            new ApplyEffectsAbilityTask { Effects = new List<GameplayEffect> { hot } },
                        },
                    },
                },
            };

            var breakdown = AbilityInspector.Build(ability);

            AssertTrue("one gameplay usage", breakdown.GameplayUsages.Count() == 1);
            var usage = breakdown.GameplayUsages.First();
            AssertTrue("role is stage application", usage.Role == EAbilityEffectRole.StageApplication);
            AssertTrue("scope is caster (implicit self target)", usage.Scope == EEffectTargetScope.Caster);

            var series = usage.Series;
            AssertTrue("series built", series != null);
            AssertTrue("duration series", series.FormatDuration() == "3/4/5/6");
            AssertTrue("one impact", series.Impacts.Count == 1);

            var impactSeries = series.Impacts[0];
            AssertTrue("delivery derived Periodic", impactSeries.Delivery == EImpactDelivery.Periodic);
            AssertTrue("not flagged dynamic", !impactSeries.IsDynamic);
            AssertTrue($"per-tick magnitudes 4/6/8/10 (got {impactSeries.FormatMagnitude()})", impactSeries.FormatMagnitude() == "4/6/8/10");
            AssertTrue($"totals 20/30/40/50 (got {impactSeries.FormatTotal()})", impactSeries.FormatTotal() == "20/30/40/50");

            var lines = breakdown.Describe(2);
            AssertTrue("tooltip line mentions healing 30 over 4s",
                lines.Any(l => l.Contains("30") && l.Contains("4")));
        }

        /// <summary>ReplaceOldContainer re-application is idempotent for an identical spec: no double-retain, clean removal.</summary>
        private static void S10_ReplaceOldContainer_Idempotent()
        {
            var attr = MakeAttribute("Speed");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 50, 50)));

            var buff = MakeEffect("SpeedBuff", new EffectConfig
            {
                Target = attr, Op = ECalculationOperation.Multiply, Magnitude = 1.2f,
                Reapply = EEffectReApplicationPolicy.ReplaceOldContainer,
            });

            Apply(gas, buff);
            AssertValue(gas, attr, "first apply", 60);

            Apply(gas, buff);
            AssertValue(gas, attr, "re-apply replaces, never compounds", 60);

            Remove(gas, buff);
            AssertValue(gas, attr, "removed", 50);
        }

        /// <summary>
        /// Ongoing requirements gate periodic ticks: a regen that avoids a tag stops ticking while
        /// another effect grants that tag, and resumes once it is gone. The granting effect lands AFTER
        /// the regen — the order that used to leave the regen ticking, because ongoing state was
        /// evaluated before the new effect's tags were compiled (PlayForge log PF-002).
        /// </summary>
        private static void S13_GAS_10_OngoingRequirement_PausesPeriodicTicks()
        {
            var attr = MakeAttribute("Poise");
            var gas = MakeGAS(MakeSet(MakeElement(attr, 0, 100)));
            var blocked = Tag.Generate(Unique("Test.Status.RegenBlocked"));

            var regen = MakeEffect("Regen", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Infinite, Target = attr, Op = ECalculationOperation.Add, Magnitude = 5,
                PeriodicTicks = true, FixedDeltaTime = 1f,
            });
            regen.TargetRequirements.OngoingRequirements.AvoidTags = new List<TagQuery> { TagQuery.HasTag(blocked) };

            var blocker = MakeEffect("Blocker", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Durational, Duration = 2f, Target = null,
                GrantedTags = new List<Tag> { blocked }, FixedDeltaTime = 1f,
            });

            Apply(gas, regen);
            Frame(gas);
            AssertValue(gas, attr, "regen ticks while unblocked", 5);

            Apply(gas, blocker);
            Frame(gas);
            AssertValue(gas, attr, "regen paused while the avoided tag is granted", 5);

            Frame(gas, 4);
            AssertTrue("regen resumes once the tag is gone", gas.TryGetAttributeValue(attr, out var value) && value.CurrentValue > 5);
        }

        /// <summary>
        /// An ability's own source tag requirements gate activation: avoiding "stunned" blocks it
        /// while the caster has that tag, and allows it again once the tag is gone. These used to be
        /// authored but never checked (PlayForge log PF-003).
        /// </summary>
        private static void S14_GAS_11_AbilitySourceTagRequirements_BlockActivation()
        {
            var gas = MakeGAS(MakeSet(MakeElement(MakeAttribute("Unused"), 1, 1)));
            var stunned = Tag.Generate(Unique("Test.Status.Stunned"));

            var ability = ScriptableObject.CreateInstance<Ability>();
            ability.name = "Strike";
            ability.Definition = new AbilityDefinition { Name = "Strike", Textures = new List<TextureItem>() };
            ability.Tags = new AbilityTags
            {
                AssetTag = Tag.Generate(Unique("Test.Ability.Strike")),
                ContextTags = new List<Tag>(),
                PassiveGrantedTags = new List<Tag>(),
                ActiveGrantedTags = new List<Tag>(),
                TagRequirements = new AbilityTagRequirements
                {
                    SourceRequirements = new AvoidRequireTagGroup
                    {
                        AvoidTags = new List<TagQuery> { TagQuery.HasTag(stunned) },
                        RequireTags = new List<TagQuery>(),
                    },
                    TargetRequirements = new AvoidRequireTagGroup { AvoidTags = new List<TagQuery>(), RequireTags = new List<TagQuery>() },
                },
            };
            ability.Behaviour = new AbilityBehaviour { Stages = new List<AbilityTaskBehaviourStage>() };
            ability.WorkerGroup = new StandardWorkerGroup();

            var abilities = gas.GetAbilitySystem();
            AssertTrue("ability granted", abilities.GiveAbility(ability, 1, out int index));
            AssertTrue("activatable while not stunned", abilities.CanActivateAbility(index));

            var stun = MakeEffect("Stun", new EffectConfig { Target = null, GrantedTags = new List<Tag> { stunned } });
            Apply(gas, stun);
            AssertTrue("blocked while the avoided tag is on the caster", !abilities.CanActivateAbility(index));

            Remove(gas, stun);
            AssertTrue("activatable again once the tag is gone", abilities.CanActivateAbility(index));
        }

        /// <summary>
        /// Pre-change workers and the pre-change callback adjust what a commit records. Armor is a
        /// pre-change <see cref="MathAttributeWorker"/> scaling incoming Health loss by (1 − Armor):
        /// it sees the INCOMING delta, the commit clamps after, and history records the clamped
        /// result. Both used to be silent no-ops — the commit ran from the original value
        /// (PlayForge log PF-004).
        /// </summary>
        private static void S15_GAS_02_PreChangeWorkers_AdjustTheCommittedDelta()
        {
            var health = MakeAttribute("Health");
            var armor = MakeAttribute("Armor");
            var gas = MakeGAS(MakeSet(
                MakeElement(health, 100, 100, EAttributeOverflowPolicy.ZeroToBase),
                MakeElement(armor, 0.5f, 0.5f)));
            var attrSystem = gas.GetAttributeSystem();

            attrSystem.ProvideWorker(new MathAttributeWorker
            {
                TimingConfig = AbstractAttributeWorker.EChangeEventTiming.PreChange,
                TargetAttribute = health,
                AllowSelfModification = true,
                TargetModification = EAttributeTargetExpanded.Current,
                SignPolicy = ESignPolicyExtended.Negative,
                From = EFromSelfSource.FromSelf,
                RelativeTo = armor,
                Operation = ECalculationOperation.Multiply,
                OperationTarget = EAttributeTarget.Current,
                OperationPolicy = EMathApplicationPolicy.OneMinus,
            });

            var damageGroup = Tag.Generate(Unique("Test.Group.Damage"));
            var hit30 = MakeEffect("Hit30", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Instant, Target = health, Op = ECalculationOperation.Add, Magnitude = -30,
            });
            hit30.Definition.RetentionGroup = damageGroup;
            var hit150 = MakeEffect("Hit150", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Instant, Target = health, Op = ECalculationOperation.Add, Magnitude = -150,
            });
            var heal50 = MakeEffect("Heal50", new EffectConfig
            {
                Policy = EEffectDurationPolicy.Instant, Target = health, Op = ECalculationOperation.Add, Magnitude = 50,
            });

            Apply(gas, hit30);
            AssertValue(gas, health, "armor halves a 30 hit", 85);
            Apply(gas, hit30);
            AssertValue(gas, health, "second 30 hit", 70);
            attrSystem.TryGetGroupValue(health, damageGroup, out var damageSlice);
            AssertApprox("adjusted commits land in the effect's group", damageSlice.CurrentValue, -30);

            // Incoming −150 → −75, THEN clamp: 70 → 0. Halving the clamped −70 would leave 35.
            Apply(gas, hit150);
            AssertValue(gas, health, "armor applies before the clamp", 0);

            // History holds the clamped −70, not −75: the heal lands on 50, not 45. The worker
            // ignores heals (Negative sign policy), so this commit takes the untouched path.
            Apply(gas, heal50);
            AssertValue(gas, health, "heal after an overkill hit", 50);

            // Raw deltas go through the same path. The pre-change callback is the original repro: it
            // halves a −30 delta to −15. Workers see raw deltas too (Decision 9), so the armor worker
            // then halves what the callback left, and Health falls by 7.5.
            AttributeSystemCallbacks.AttributeChangeDelegate halveLoss = (attribute, change) =>
            {
                if (ReferenceEquals(attribute, health) && change.Value.CurrentValue < 0f)
                    change.Override(change.Value.ToAttributeValue() * 0.5f);
            };
            attrSystem.Callbacks.OnAttributePreChange += halveLoss;
            gas.TryModifyAttribute(health, SourcedModifiedAttributeValue.GenerateSimple(gas, health, -30f, 0f));
            attrSystem.Callbacks.OnAttributePreChange -= halveLoss;
            AssertValue(gas, health, "the callback and then armor halve a raw −30", 42.5f, 100);

            float sum = 0f;
            foreach (var slice in attrSystem.GetBreakdown(health)) sum += slice.Value.CurrentValue;
            AssertApprox("breakdown sums to ActiveValue", sum, 42.5f);
        }

        /// <summary>
        /// A raw delta that moves Base and Current together lands whole on a pool clamped to its Base.
        /// Raising both (a max-Health bonus that also heals; an enemy's dials) used to lose the
        /// Current gain: it was clamped against the old Base before the Base rose (PlayForge log
        /// PF-005). Lowering both still ends with Current inside the new Base.
        /// </summary>
        private static void S16_GAS_12_RawDelta_MovesBaseAndCurrentTogether()
        {
            var poise = MakeAttribute("Poise");
            var dial = MakeAttribute("Dial");
            var gas = MakeGAS(MakeSet(
                MakeElement(poise, 25, 25, EAttributeOverflowPolicy.ZeroToBase),
                MakeElement(dial, 0, 0, EAttributeOverflowPolicy.ZeroToBase)));

            gas.TryModifyAttribute(dial, SourcedModifiedAttributeValue.GenerateSimple(gas, dial, 2f, 2f));
            AssertValue(gas, dial, "raising an empty pool by 2/2", 2, 2);

            gas.TryModifyAttribute(poise, SourcedModifiedAttributeValue.GenerateSimple(gas, poise, 12.5f, 12.5f));
            AssertValue(gas, poise, "raising a full pool by 12.5/12.5", 37.5f, 37.5f);

            gas.TryModifyAttribute(poise, SourcedModifiedAttributeValue.GenerateSimple(gas, poise, -10f, -10f));
            AssertValue(gas, poise, "lowering it by 10/10", 27.5f, 27.5f);

            gas.TryModifyAttribute(poise, SourcedModifiedAttributeValue.GenerateSimple(gas, poise, 0f, -20f));
            AssertValue(gas, poise, "lowering only the Base pulls Current down with it", 7.5f, 7.5f);
        }

        /// <summary>
        /// A GAS with nothing going on costs no garbage per frame. Every fighter's GAS runs its end of
        /// frame each frame, and that used to allocate about 1 KB apiece: the action queue enumerated a
        /// SortedDictionary to learn it was empty, and a frame summary snapshot copied six lists for a
        /// callback nobody listened to (PlayForge log PF-006).
        /// </summary>
        private static void S17_GAS_P1_IdleFrame_AllocatesNothing()
        {
            var health = MakeAttribute("Health");
            var gas = MakeGAS(MakeSet(MakeElement(health, 100, 100, EAttributeOverflowPolicy.ZeroToBase)));

            const int Frames = 2000;
            long bytes = long.MaxValue;
            for (int attempt = 0; attempt < 3 && bytes > 0; attempt++)
            {
                for (int i = 0; i < 50; i++) gas.EndOfFrame();

                int collections = System.GC.CollectionCount(0);
                long before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < Frames; i++) gas.EndOfFrame();
                long after = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

                // A collection mid-measure hides allocations; measure again
                if (System.GC.CollectionCount(0) == collections) bytes = after - before;
            }

            AssertTrue($"an idle end of frame allocates nothing (measured {bytes / (float)Frames:0.#} bytes per frame)", bytes <= 0);
        }

        /// <summary>
        /// Compiling granted tags and checking tag requirements allocate nothing, while the applied tags
        /// they read still follow tags as they come and go. Both run several times for every effect
        /// applied or removed, and they used to allocate about 15 KB per effect between them: the
        /// applied tags were copied into a new list for every check (twice over), the granted tags were
        /// gathered through iterators, and tag queries counted with LINQ (PlayForge log PF-007).
        /// </summary>
        private static void S18_GAS_P2_TagChecks_AllocateNothing()
        {
            var health = MakeAttribute("Health");
            var gas = MakeGAS(MakeSet(MakeElement(health, 100, 100, EAttributeOverflowPolicy.ZeroToBase)));

            var marked = Tag.Generate(Unique("Test.Status.Marked"));
            var parent = Tag.Generate(Unique("Test.Status"));
            var markFx = MakeEffect("Mark", new EffectConfig { Target = null, GrantedTags = new List<Tag> { marked } });
            AssertTrue("apply the marking effect", Apply(gas, markFx));
            AssertTrue("the applied tags include a granted tag", gas.GetAppliedTags().Contains(marked));

            var required = new List<TagQuery> { TagQuery.HasTag(marked) };
            var avoid = new List<TagQuery> { TagQuery.HasTag(parent) };
            AssertTrue("requirements read the applied tags", ForgeHelper.ValidateAvoidRequireQueries(gas.GetAppliedTags(), required, avoid));

            const int Checks = 2000;
            long bytes = long.MaxValue;
            for (int attempt = 0; attempt < 3 && bytes > 0; attempt++)
            {
                for (int i = 0; i < 20; i++) Check();

                int collections = System.GC.CollectionCount(0);
                long before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < Checks; i++) Check();
                long after = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

                if (System.GC.CollectionCount(0) == collections) bytes = after - before;
            }

            AssertTrue($"compiling and checking tags allocates nothing (measured {bytes / (float)Checks:0.#} bytes per round)", bytes <= 0);

            // The cached list follows the tags: gone once the effect that granted it is
            Remove(gas, markFx);
            AssertTrue("the applied tags drop a removed tag", !gas.GetAppliedTags().Contains(marked));
            AssertTrue("requirements see it gone", !ForgeHelper.ValidateAvoidRequireQueries(gas.GetAppliedTags(), required, avoid));

            void Check()
            {
                gas.CompileGrantedTags();
                ForgeHelper.ValidateAvoidRequireQueries(gas.GetAppliedTags(), required, avoid);
                ForgeHelper.ValidateAllQueries(required, (IEnumerable<Tag>)gas.GetAppliedTags());
            }
        }

        /// <summary>
        /// An exception thrown while an ability runs is reported, not swallowed. A bare catch around
        /// execution, meant for cancellation, also ate real failures, such as an asset loader that throws
        /// before any stage starts, so a broken ability looked like one that did nothing (PlayForge log
        /// PF-008, was PF-B08). Cancellation stays silent.
        /// </summary>
        private static void S19_TS_04_BrokenAbility_IsReported()
        {
            var gas = MakeGAS(MakeSet(MakeElement(MakeAttribute("Unused"), 1, 1)));
            var ability = MakeLoaderAbility("Broken", new ThrowingLoader());

            var abilities = gas.GetAbilitySystem();
            AssertTrue("ability granted", abilities.GiveAbility(ability, 1, out int index));

            // Record what gets logged, without printing it, so a passing run leaves the console clean
            var logger = Debug.unityLogger;
            var original = logger.logHandler;
            var recorder = new RecordingLogHandler();
            logger.logHandler = recorder;
            try
            {
                abilities.TryActivateAbility(abilities.CreateActivationRequest(index));
            }
            finally
            {
                logger.logHandler = original;
            }

            AssertTrue($"the loader's exception is reported (logged: {recorder.Errors.Count} errors)",
                recorder.Errors.Exists(message => message.Contains(ThrowingLoader.Message)));
        }

        /// <summary>An ability with no stages, whose only work is <paramref name="loader"/>, run as it activates.</summary>
        private static Ability MakeLoaderAbility(string name, AbstractSetAssetsTask loader)
        {
            var ability = ScriptableObject.CreateInstance<Ability>();
            ability.name = name;
            ability.Definition = new AbilityDefinition { Name = name, Textures = new List<TextureItem>() };
            ability.Tags = new AbilityTags
            {
                AssetTag = Tag.Generate(Unique($"Test.Ability.{name}")),
                ContextTags = new List<Tag>(),
                PassiveGrantedTags = new List<Tag>(),
                ActiveGrantedTags = new List<Tag>(),
                TagRequirements = new AbilityTagRequirements
                {
                    SourceRequirements = new AvoidRequireTagGroup { AvoidTags = new List<TagQuery>(), RequireTags = new List<TagQuery>() },
                    TargetRequirements = new AvoidRequireTagGroup { AvoidTags = new List<TagQuery>(), RequireTags = new List<TagQuery>() },
                },
            };
            ability.Behaviour = new AbilityBehaviour
            {
                Stages = new List<AbilityTaskBehaviourStage>(),
                AssetLoaders = new List<AbstractSetAssetsTask> { loader },
            };
            ability.WorkerGroup = new StandardWorkerGroup();
            return ability;
        }

        /// <summary>
        /// Activating an ability again reuses the behaviour an earlier activation compiled, once that activation
        /// is over, instead of compiling it anew. Compiling was most of what an activation allocated, about
        /// 2.8 KB each, and a fighter activates several abilities a second (PlayForge log PF-012, was PF-B09).
        /// The activations here end at once, on their loader failing, which they do the same way every time.
        /// </summary>
        private static void S21_TS_03_RepeatedActivation_ReusesItsCompiledBehaviour()
        {
            var gas = MakeGAS(MakeSet(MakeElement(MakeAttribute("Unused"), 1, 1)));
            var ability = MakeLoaderAbility("Repeated", new ThrowingLoader());

            var abilities = gas.GetAbilitySystem();
            AssertTrue("ability granted", abilities.GiveAbility(ability, 1, out int index));
            AssertTrue("its container", abilities.TryGetAbilityContainer(index, out var container));

            const int Activations = 5;
            var logger = Debug.unityLogger;
            var original = logger.logHandler;
            var recorder = new RecordingLogHandler();
            logger.logHandler = recorder;
            try
            {
                for (int i = 0; i < Activations; i++) abilities.TryActivateAbility(abilities.CreateActivationRequest(index));
            }
            finally
            {
                logger.logHandler = original;
            }

            int ran = recorder.Errors.FindAll(message => message.Contains(ThrowingLoader.Message)).Count;
            AssertTrue($"every activation ran its behaviour ({ran} of {Activations})", ran == Activations);
            AssertTrue($"the behaviour was compiled once, not once per activation (compiled {container.CompiledProxies} times)", container.CompiledProxies == 1);
        }

        /// <summary>An asset loader that fails, as a broken ability would.</summary>
        private class ThrowingLoader : AbstractSetAssetsTask
        {
            public const string Message = "S19: the loader broke";

            public override bool IsCriticalSection => false;
            public override void Prepare(AbilityDataPacket data) => throw new System.InvalidOperationException(Message);
        }

        /// <summary>
        /// A process whose step throws is reported, and the frame goes on without it: the processes stepped
        /// after it still step, and a state queued for it, such as a termination, still applies. A throwing
        /// step used to end the whole frame's stepping and skip its own flush, so a fighter despawned on a
        /// frame where something else threw kept stepping its destroyed body for good, throwing every frame
        /// and starving every process stepped after it (PlayForge log PF-011).
        /// </summary>
        private static void S20_PC_01_ThrowingStep_DoesNotStallTheFrame()
        {
            // A process control of its own, so the test steps only its processes
            var previous = ProcessControl.Instance;
            var host = new GameObject("S20 Process Control") { hideFlags = HideFlags.HideAndDontSave };
            var control = host.AddComponent<ProcessControl>();
            var options = new SerializedObject(control);
            options.FindProperty("DontDestroyOnLoad").boolValue = false;
            options.FindProperty("CollectOnAwake").boolValue = false;
            options.ApplyModifiedPropertiesWithoutUndo();

            var logger = Debug.unityLogger;
            var original = logger.logHandler;
            var recorder = new RecordingLogHandler();

            try
            {
                ProcessControl.Instance = null;
                control.Bootstrap();

                // Stepped in the order registered: the broken one first
                var broken = new SteppedProcess("S20 Broken", throws: true);
                var after = new SteppedProcess("S20 After", throws: false);
                AssertTrue("register the broken process", ProcessControl.Register(broken, control, ProcessDataPacket.Default(), out var brokenRelay));
                AssertTrue("register the process after it", ProcessControl.Register(after, control, ProcessDataPacket.Default(), out _));
                AssertTrue("both are running", brokenRelay.IsRunning && after.Relay.IsRunning);

                var stepProcesses = typeof(ProcessControl).GetMethod("StepProcesses",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                AssertTrue("ProcessControl steps through StepProcesses", stepProcesses != null);
                if (stepProcesses == null) return;

                // Torn down while running, as a despawned fighter is: the termination waits for its step
                control.Terminate(brokenRelay.CacheIndex);

                bool escaped = false;
                logger.logHandler = recorder;
                try
                {
                    stepProcesses.Invoke(control, new object[] { EProcessStepTiming.Update });
                }
                catch (System.Reflection.TargetInvocationException)
                {
                    escaped = true;
                }
                finally
                {
                    logger.logHandler = original;
                }

                AssertTrue("the broken step's exception stays in its step", !escaped);
                AssertTrue($"the process after the broken one still steps that frame (stepped {after.Steps} times)", after.Steps == 1);
                AssertTrue($"the broken process's termination applies (state {brokenRelay.State})", brokenRelay.State == EProcessState.Terminated);
                AssertTrue($"the broken step is reported (logged: {recorder.Errors.Count} errors)",
                    recorder.Errors.Exists(message => message.Contains(SteppedProcess.Message)));
            }
            finally
            {
                logger.logHandler = original;
                control.TerminateAllImmediately();
                Object.DestroyImmediate(host);
                ProcessControl.Instance = previous;
            }
        }

        /// <summary>
        /// PF-013: a MonoBehaviour GAS raises <c>OnSystemInitialized</c> to a subscriber that attached before it
        /// was registered, once it's ready to use. Its callbacks used to be created partway through
        /// initialization, just before the event fired, so nobody outside could subscribe in time (PF-C18).
        /// </summary>
        private static void S22_GAS_13_SystemInitialized_ReachesEarlySubscribers()
        {
            // A process control of its own, as in S20
            var previous = ProcessControl.Instance;
            var host = new GameObject("S22 Process Control") { hideFlags = HideFlags.HideAndDontSave };
            var control = host.AddComponent<ProcessControl>();
            var options = new SerializedObject(control);
            options.FindProperty("DontDestroyOnLoad").boolValue = false;
            options.FindProperty("CollectOnAwake").boolValue = false;
            options.ApplyModifiedPropertiesWithoutUndo();

            var entity = ScriptableObject.CreateInstance<EntityIdentity>();
            entity.name = entity.Name = Unique("S22 Entity");
            entity.Description = "";
            entity.Textures = new List<TextureItem>();
            entity.AssetTag = Tag.Generate(Unique("Test.Entity.S22"));
            entity.Affiliation = new List<Tag>();
            entity.GrantedTags = new List<Tag>();
            entity.ContextTags = new List<Tag>();
            entity.MaxLevel = 1;
            entity.MaxAbilitiesOperation = new ScalerIntegerMagnitudeOperation { Magnitude = 8 };
            entity.MaxItemsOperation = new ScalerIntegerMagnitudeOperation { Magnitude = 8 };
            entity.MaxEquippedItemsOperation = new ScalerIntegerMagnitudeOperation { Magnitude = 8 };
            entity.StartingAbilities = new List<Ability>();
            entity.StartingItems = new List<StartingItemContainer>();
            entity.AttributeSet = MakeSet(MakeElement(MakeAttribute("S22 Health"), 100f, 100f));
            entity.WorkerGroup = new StandardWorkerGroup();

            // Hidden but in the scene: a GameObject outside any scene (HideAndDontSave) is cloned on registration
            // and the clone initialized instead (PF-C19)
            var body = new GameObject("S22 Body") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor };

            try
            {
                ProcessControl.Instance = null;
                control.Bootstrap();

                var gas = GameplayAbilitySystem.AddToGameObject(body, entity);
                gas.ProcessLifecycle = EProcessLifecycle.Synchronous;
                gas.ProcessTiming = EProcessStepTiming.Update;

                // Subscribed after the GAS is added and before it's registered, as game code would
                var callbacks = gas.Callbacks;
                AssertTrue("the GAS has its callbacks before it's initialized", callbacks != null);
                if (callbacks == null) return;

                int fired = 0;
                bool readyWhenFired = false;
                callbacks.OnSystemInitialized += () =>
                {
                    fired++;
                    readyWhenFired = gas.IsInitialized && gas.AttributeSystem != null;
                };

                AssertTrue("register the GAS", ProcessControl.Register(gas, control, ProcessDataPacket.Default(), out _));

                var stepProcesses = typeof(ProcessControl).GetMethod("StepProcesses",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (int frame = 0; frame < 3 && !gas.IsInitialized && stepProcesses != null; frame++)
                {
                    stepProcesses.Invoke(control, new object[] { EProcessStepTiming.Update });
                }

                AssertTrue($"the GAS initializes (initialized {gas.IsInitialized})", gas.IsInitialized);
                AssertTrue($"OnSystemInitialized reaches the early subscriber once (fired {fired} times)", fired == 1);
                AssertTrue("the GAS is ready to use when it fires", readyWhenFired);
                AssertTrue("initialization keeps the callbacks it had", ReferenceEquals(callbacks, gas.Callbacks));
            }
            finally
            {
                control.TerminateAllImmediately();
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(entity.AttributeSet);
                Object.DestroyImmediate(entity);
                ProcessControl.Instance = previous;
            }
        }

        /// <summary>A synchronous process stepped every Update that counts its steps; a broken one throws in each.</summary>
        private class SteppedProcess : LazyRuntimeProcess
        {
            public const string Message = "S20: the step broke";

            private readonly bool throws;
            public int Steps;

            public SteppedProcess(string name, bool throws)
                : base(name, EProcessStepPriorityMethod.Manual, 0, EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
            {
                this.throws = throws;
            }

            public override void WhenUpdate()
            {
                Steps++;
                if (throws) throw new System.InvalidOperationException(Message);
            }
        }

        /// <summary>Keeps errors and exceptions logged while it's installed, and prints nothing.</summary>
        private class RecordingLogHandler : ILogHandler
        {
            public readonly List<string> Errors = new();

            public void LogFormat(LogType logType, Object context, string format, params object[] args)
            {
                if (logType is LogType.Error or LogType.Exception or LogType.Assert) Errors.Add(string.Format(format, args));
            }

            public void LogException(System.Exception exception, Object context) => Errors.Add(exception.ToString());
        }

        /// <summary>
        /// Resuming a sequence leaves Time.timeScale alone (TS-1, PlayForge log PF-001). Time scale is
        /// global, so a sequence resuming during a hit-stop or slow motion must not end it.
        /// </summary>
        private static void S23_TS_01_Resume_LeavesTimeScaleAlone()
        {
            float saved = Time.timeScale;
            try
            {
                var process = new TaskSequenceProcess(new TaskSequenceChain());
                typeof(AbstractRuntimeProcess)
                    .GetField("processActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(process, true);

                Time.timeScale = 0.25f;
                AssertTrue("an active sequence accepts the resume", process.TryHandleResume());
                AssertApprox("time scale after the resume", Time.timeScale, 0.25f);
            }
            finally
            {
                Time.timeScale = saved;
                // Setting the time scale dirties the TimeManager, and the editor would then save it into
                // ProjectSettings. The value is restored, so the dirty flag goes too.
                foreach (var settings in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset"))
                    EditorUtility.ClearDirty(settings);
            }
        }

        /// <summary>
        /// A GAS can be given an item (GAS-3). ItemSystemComponent never created its shelf, so GiveItem
        /// threw a NullReferenceException, and every read of the empty inventory allocated a new list.
        /// </summary>
        private static void S24_GAS_03_GiveItem_LandsOnTheShelf()
        {
            var gas = MakeGAS(MakeSet(MakeElement(MakeAttribute("Unused"), 1, 1)));
            var items = gas.GetItemSystem();

            var sword = ScriptableObject.CreateInstance<Item>();
            sword.name = "TestSword";
            sword.Tags.AssetTag = Tag.Generate(Unique("Test.Item.Sword"));

            AssertTrue("an empty inventory holds nothing", items.ItemCount == 0 && items.GetItemContainers().Count == 0);
            AssertTrue("give the item", items.GiveItem(sword, out int index));
            AssertTrue("it is on the shelf at index 0", index == 0 && items.HasItem(sword) && items.ItemCount == 1);
            AssertTrue("the containers list it", items.GetItemContainers().Count == 1);
        }

        /// <summary>
        /// A runtime GAS registered with ProcessControl runs its end of frame each frame (GAS-1). It stepped
        /// in Update only, so WhenLateUpdate, and with it EndOfFrame (queued actions, tag workers, analysis
        /// workers, death), never ran. The other scenarios call WhenLateUpdate by hand, which is how this
        /// stayed hidden, so this one steps the GAS through a ProcessControl's own loop.
        /// </summary>
        private static void S25_GAS_01_RuntimeGAS_RunsItsEndOfFrame()
        {
            // A process control of its own, as in S20, so the test steps only its own process
            var previous = ProcessControl.Instance;
            var host = new GameObject("S25 Process Control") { hideFlags = HideFlags.HideAndDontSave };
            var control = host.AddComponent<ProcessControl>();
            var options = new SerializedObject(control);
            options.FindProperty("DontDestroyOnLoad").boolValue = false;
            options.FindProperty("CollectOnAwake").boolValue = false;
            options.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                ProcessControl.Instance = null;
                control.Bootstrap();

                var gas = MakeGAS(MakeSet(MakeElement(MakeAttribute("Unused"), 1, 1)));
                int framesCompleted = 0;
                gas.Callbacks.OnFrameComplete += _ => framesCompleted++;
                AssertTrue("register the GAS", ProcessControl.Register(gas, control, ProcessDataPacket.Default(), out var relay));
                AssertTrue("it is running", relay.IsRunning);

                var stepProcesses = typeof(ProcessControl).GetMethod("StepProcesses",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                AssertTrue("ProcessControl steps through StepProcesses", stepProcesses != null);
                if (stepProcesses == null) return;

                stepProcesses.Invoke(control, new object[] { EProcessStepTiming.Update });
                stepProcesses.Invoke(control, new object[] { EProcessStepTiming.LateUpdate });

                AssertTrue($"one stepped frame runs one end of frame (ran {framesCompleted})", framesCompleted == 1);
            }
            finally
            {
                control.TerminateAllImmediately();
                Object.DestroyImmediate(host);
                ProcessControl.Instance = previous;
            }
        }
    }
}
