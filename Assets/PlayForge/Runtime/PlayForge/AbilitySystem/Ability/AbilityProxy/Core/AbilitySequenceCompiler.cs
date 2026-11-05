using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Compiles an AbilityBehaviour into a reusable TaskSequence.
    ///
    /// Each AbilityTaskBehaviourStage becomes a SequenceStage with:
    /// - Ability tasks wrapped via AbilityTaskAdapter
    /// - Stage policy mapped from IAbilityProxyStagePolicy to ISequenceStagePolicy
    /// - Usage effects wired into stage OnTerminate callbacks
    /// - AbilitySystemCallbacks wired into stage event hooks
    ///
    /// The compiled TaskSequence is reusable: each Run() creates a fresh
    /// TaskSequenceRuntime, so the immutable definition is safe to share.
    ///
    /// IMPORTANT: Ability tasks must be stateless. All per-activation data
    /// flows through the AbilityDataPacket, not as fields on the task.
    /// </summary>
    public static class AbilitySequenceCompiler
    {
        /// <summary>
        /// Compiles an AbilityBehaviour into a TaskSequence.
        /// Called once when an ability is granted; the result is reused per activation.
        /// </summary>
        /// <param name="behaviour">The ability behaviour to compile.</param>
        /// <param name="abilityName">Display name for ProcessControl visibility.</param>
        /// <returns>A reusable TaskSequence representing the ability's execution stages.</returns>
        public static TaskSequence Compile(AbilityBehaviour behaviour, string abilityName = null)
        {
            var name = abilityName ?? "Anon-Ability";

            var builder = TaskSequenceBuilder.Create($"Ability:{name}");

            if (behaviour.Stages == null || behaviour.Stages.Count == 0)
            {
                return builder.BuildSequence();
            }

            for (int i = 0; i < behaviour.Stages.Count; i++)
            {
                var abilityStage = behaviour.Stages[i];
                int stageIndex = i;

                // A stage that asks for a tag is a branch, taken while its condition holds and skipped otherwise, with
                // its own settings, a critical flag among them. The compiler emitted none, so there was no on-hit or
                // on-miss stage (TS-7)
                var condition = abilityStage.Condition;
                if (condition != null && condition.IsSet)
                {
                    builder.Branch(branch => branch.If(
                        data => condition.Holds(data as AbilityDataPacket),
                        stage => ConfigureStage(stage, abilityStage, stageIndex)));
                    continue;
                }

                builder.Stage(stage =>
                {
                    ConfigureStage(stage, abilityStage, stageIndex);
                });
            }

            return builder.BuildSequence();
        }

        /// <summary>
        /// Compiles a targeting task into a single-stage TaskSequence.
        /// Returns null if there is no targeting task assigned.
        /// </summary>
        /// <param name="behaviour">The ability behaviour containing the targeting task.</param>
        /// <param name="abilityName">Display name for ProcessControl visibility.</param>
        /// <returns>A reusable TaskSequence for the targeting phase, or null.</returns>
        public static TaskSequence CompileTargeting(AbilityBehaviour behaviour, string abilityName = null)
        {
            if (behaviour.Targeting == null) return null;

            var name = abilityName ?? "Anon-Ability";

            var builder = TaskSequenceBuilder.Create($"Targeting:{name}")
                .WithCriticalFlag(true); // Targeting is always a critical section

            builder.Stage(stage =>
            {
                stage.WithName("Targeting");

                // Wrap the targeting task as an ISequenceTask
                stage.Task(new TargetingTaskAdapter(behaviour.Targeting));
            });

            return builder.BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TARGETING TASK ADAPTER
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adapts an AbstractTargetingAbilityTask to ISequenceTask.
        /// Delegates Prepare/Activate/Clean to the targeting task.
        /// </summary>
        private class TargetingTaskAdapter : ISequenceTask
        {
            private readonly AbstractTargetingAbilityTask _targetingTask;

            public TargetingTaskAdapter(AbstractTargetingAbilityTask targetingTask)
            {
                _targetingTask = targetingTask;
            }

            public void Prepare(SequenceDataPacket data)
            {
                if (data is AbilityDataPacket abilityData)
                {
                    _targetingTask.Prepare(abilityData);
                }
            }

            public async UniTask Execute(SequenceDataPacket data, System.Threading.CancellationToken token)
            {
                if (data is AbilityDataPacket abilityData)
                {
                    await _targetingTask.Activate(abilityData, token);
                    abilityData.TargetingFinished = true;
                }
            }

            public bool Step(SequenceDataPacket data, float deltaTime) => true; // Targeting tasks are async-only

            public void Clean(SequenceDataPacket data)
            {
                if (data is AbilityDataPacket abilityData)
                {
                    _targetingTask.Clean(abilityData);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE CONFIGURATION
        // ═══════════════════════════════════════════════════════════════════════════

        private static void ConfigureStage(
            StageBuilder stage,
            AbilityTaskBehaviourStage abilityStage,
            int stageIndex)
        {
            stage.WithName($"Stage {stageIndex.ToString()}");
            
            // Map stage policy
            MapStagePolicy(stage, abilityStage.StagePolicy);

            // Adapt and add each ability task
            if (abilityStage.Tasks != null)
            {
                foreach (var abilityTask in abilityStage.Tasks)
                {
                    if (abilityTask == null) continue;

                    var adapter = new AbilityTaskAdapter(abilityTask, abilityStage);
                    stage.Task(adapter);

                    // If the task is a critical section, mark the stage as critical
                    if (abilityTask.IsCriticalSection)
                    {
                        stage.WithCriticalFlag(true);
                    }
                }
            }

            // Wire ability system callbacks and usage effects into a single OnTerminate
            WireStageCallbacks(stage, abilityStage, stageIndex);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE POLICY MAPPING
        // ═══════════════════════════════════════════════════════════════════════════

        private static void MapStagePolicy(
            StageBuilder stage,
            IAbilityProxyStagePolicy abilityPolicy)
        {
            switch (abilityPolicy)
            {
                case AnyProxyStagePolicy:
                    stage.WhenAny();
                    break;
                case AllProxyStagePolicy:
                    stage.WhenAll();
                    break;
                default:
                    // Unknown policy — default to WhenAll for safety
                    stage.WhenAll();
                    if (abilityPolicy != null)
                    {
                        Debug.LogWarning(
                            $"[AbilitySequenceCompiler] Unknown stage policy type '{abilityPolicy.GetType().Name}', " +
                            "defaulting to WhenAll.");
                    }
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CALLBACK WIRING
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wires AbilitySystemCallbacks and usage effects into the generated sequence stage.
        /// - Stage activated fires when the stage begins, with its tasks prepared
        /// - Stage ended fires when it terminates
        /// - Usage effects apply at whichever of the two the stage asks for
        /// These are stage hooks rather than a task, because a task counts toward the stage's policy: a task
        /// that finished at once ended every Any stage as it began (TS-B).
        /// </summary>
        private static void WireStageCallbacks(
            StageBuilder stage,
            AbilityTaskBehaviourStage abilityStage,
            int stageIndex)
        {
            bool applyUsage = abilityStage.ApplyUsageEffects;
            bool applyAtStart = abilityStage.ApplyUsageAtStageStart;

            // The stage's milestone on the packet's path, formatted once as it compiles: it was formatted on every run
            // (TS-5)
            string pathPart = $"Stage[{stageIndex},{abilityStage.Tasks?.Count ?? 0}tasks]";

            stage.OnInit(data =>
            {
                if (data is not AbilityDataPacket abilityData) return;

                // Track stage milestone on the data packet path
                abilityData.AppendPath(pathPart);

                abilityData.Callbacks?.AbilityStageActivated(
                    AbilityCallbackStatus.GenerateForStageEvent(abilityData, abilityStage, true));

                if (applyUsage && applyAtStart) ApplyUsageEffects(abilityData);
            });

            stage.OnTerminate((ctx, success) =>
            {
                if (ctx.Data is not AbilityDataPacket abilityData) return;

                // Apply usage effects if configured and successful
                if (applyUsage && !applyAtStart && success) ApplyUsageEffects(abilityData);

                abilityData.Callbacks?.AbilityStageEnded(
                    AbilityCallbackStatus.GenerateForStageEvent(abilityData, abilityStage, success));
            });
        }

        private static void ApplyUsageEffects(AbilityDataPacket abilityData)
        {
            if (abilityData.UsageEffectsApplied || abilityData.EffectOrigin is not AbilitySpec spec) return;

            spec.ApplyUsageEffects(abilityData);
            abilityData.UsageEffectsApplied = true;
        }
    }
}
