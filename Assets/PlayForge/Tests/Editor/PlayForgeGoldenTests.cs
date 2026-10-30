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
    /// These encode the TARGET semantics of the new model. Scenarios marked
    /// [TargetSemantics] are EXPECTED to fail until the corresponding phase lands —
    /// they pin the decisions in §7 of the plan. Everything else must stay green the
    /// whole way through (regression tripwires).
    ///
    /// Run from the menu (PlayForge → Run Golden Tests) or headless:
    ///   Unity -batchmode -quit -projectPath &lt;proj&gt;
    ///         -executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll
    ///
    /// Deliberately NOT NUnit: the PlayForge runtime lives in Assembly-CSharp, which
    /// test-framework asmdefs cannot reference. A plain runner in Assembly-CSharp-Editor
    /// sidesteps the whole assembly problem; migrating to UTF later is mechanical.
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
            RunScenario(nameof(S6_CommittedHistoryJoinsBaseline), S6_CommittedHistoryJoinsBaseline, targetSemantics: true);
            RunScenario(nameof(S7_Stacking_MagnitudePerStack), S7_Stacking_MagnitudePerStack);
            // Target-semantics: modifier REMOVAL must propagate to regulated attributes.
            // Today RemoveAttributeDerivation recomputes silently (no AttributeChanged),
            // so the derived attribute goes stale — the user-reported "not recalculated"
            // symptom. Fixed when UnregisterModifier routes through the commit pipeline.
            RunScenario(nameof(S8_Regulation_DerivedAttributeFollowsSource), S8_Regulation_DerivedAttributeFollowsSource, targetSemantics: true);
            RunScenario(nameof(S9_NullTargetEffect_TagGrantOnly), S9_NullTargetEffect_TagGrantOnly);
            RunScenario(nameof(S10_ReplaceOldContainer_Idempotent), S10_ReplaceOldContainer_Idempotent);
            RunScenario(nameof(S11_GroupAttribution_BaseVsBonus), S11_GroupAttribution_BaseVsBonus, targetSemantics: true);
            RunScenario(nameof(S12_AbilityInspector_PerLevelSeries), S12_AbilityInspector_PerLevelSeries);
            RunScenario(nameof(S13_GAS_10_OngoingRequirement_PausesPeriodicTicks), S13_GAS_10_OngoingRequirement_PausesPeriodicTicks);
            RunScenario(nameof(S14_GAS_11_AbilitySourceTagRequirements_BlockActivation), S14_GAS_11_AbilitySourceTagRequirements_BlockActivation);
            RunScenario(nameof(S23_TS_01_Resume_LeavesTimeScaleAlone), S23_TS_01_Resume_LeavesTimeScaleAlone);

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
        /// [TargetSemantics — red until Phase 2] Plan §2.2/§7.1: committed history joins the
        /// BASELINE, so transient % modifiers scale permanent gains:
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
        /// [TargetSemantics — needs the per-group attribution surface] One health pool split
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
    }
}
