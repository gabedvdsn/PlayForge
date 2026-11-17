using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Bridge between ability activation and sequence-based execution.
    ///
    /// AbilityProxy compiles the AbilityBehaviour into reusable TaskSequences
    /// at construction time — one for targeting and one for execution stages.
    /// Both phases register with ProcessControl for full visibility.
    ///
    /// Since AbilityDataPacket extends SequenceDataPacket, the same packet
    /// flows through targeting, execution, callbacks, and ProcessControl.
    ///
    /// Injection, callbacks, and usage effects are handled transparently:
    /// - Injections go to the compiled sequence, its callback heard either way
    /// - AbilitySystemCallbacks fire from sequence stage hooks
    /// - Usage effects apply at configured stage boundaries
    /// </summary>
    public class AbilityProxy
    {
        private readonly AbilityBehaviour Behaviour;

        /// <summary>The compiled execution TaskSequence (reusable across activations).</summary>
        private TaskSequence CompiledSequence { get; set; }

        /// <summary>The compiled targeting TaskSequence (reusable, null if no targeting task).</summary>
        private TaskSequence CompiledTargeting { get; set; }

        /// <summary>The ProcessRelay for the active execution sequence (null when not running).</summary>
        public ProcessRelay ActiveRelay { get; private set; }

        /// <summary>The ProcessRelay for the active targeting sequence (null when not targeting).</summary>
        public ProcessRelay TargetingRelay { get; private set; }

        public bool HasAnyCriticalStage { get; private set; }
        public Action OnCriticalSectionExited { get; set; }

        public AbilityProxy(AbilityBehaviour behaviour, string abilityName = null)
        {
            Behaviour = behaviour;

            HasAnyCriticalStage = false;
            if (Behaviour.Stages != null)
            {
                for (int i = 0; i < Behaviour.Stages.Count && !HasAnyCriticalStage; i++)
                    HasAnyCriticalStage = HasCriticalTask(Behaviour.Stages[i]);
            }

            CompileSequence(abilityName);
        }

        // Whether a stage holds a critical task, by index. It was asked through LINQ on every activation, whose
        // enumerators allocated
        private static bool HasCriticalTask(AbilityTaskBehaviourStage stage)
        {
            var tasks = stage?.Tasks;
            if (tasks == null) return false;

            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i] != null && tasks[i].IsCriticalSection) return true;
            }
            return false;
        }

        /// <summary>
        /// Compiles the behaviour into TaskSequences (targeting + execution).
        /// Called once, typically at grant time. Safe to call multiple times (recompiles).
        /// </summary>
        /// <param name="abilityName">Display name for ProcessControl visibility.</param>
        public void CompileSequence(string abilityName = null)
        {
            CompiledTargeting = AbilitySequenceCompiler.CompileTargeting(Behaviour, abilityName);
            CompiledSequence = AbilitySequenceCompiler.Compile(Behaviour, abilityName);
        }

        /// <summary>
        /// Returns true if the proxy has a compiled execution sequence ready.
        /// </summary>
        public bool IsCompiled => CompiledSequence != null;

        /// <summary>
        /// Returns true if the proxy has a compiled targeting sequence.
        /// </summary>
        public bool HasCompiledTargeting => CompiledTargeting != null;

        /// <summary>
        /// True when nothing of an activation is still running on this proxy, so it can run the next one. Its
        /// compiled sequences hold one run at a time.
        /// </summary>
        public bool IsIdle => ActiveRelay == null && TargetingRelay == null
                              && CompiledSequence?.Runtime == null && CompiledTargeting?.Runtime == null;

        public void Clean()
        {
            // Interrupt any running sequences
            CompiledTargeting?.Interrupt();
            CompiledSequence?.Interrupt();

            ActiveRelay = null;
            TargetingRelay = null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TARGETING (its compiled sequence, where it has a targeting task)
        // ═══════════════════════════════════════════════════════════════════════════

        public async UniTask ActivateTargetingTask(CancellationToken token, AbilityDataPacket implicitData)
        {
            if (Behaviour.Targeting != null)
            {
                implicitData.AppendPath($"Targeting[{Behaviour.Targeting.GetType().Name}]");
            }

            // An ability with no targeting task has no targeting sequence, and nothing to target
            if (HasCompiledTargeting)
            {
                await ActivateTargetingViaSequence(token, implicitData);
            }

            // A targeting task gives up by throwing, which only ends its own sequence, so the ability hears of
            // it here. A task set not to break lets the ability go on
            if (implicitData.TargetingFailed && Behaviour.Targeting is { BreakRuntimeOnInvalid: true })
            {
                throw new OperationCanceledException();
            }

            // Targeting that didn't get to its end has no target to give, whether it threw or something ended its
            // sequence first. The ability went on regardless
            if (HasCompiledTargeting && !implicitData.TargetingFinished)
            {
                throw new OperationCanceledException();
            }
        }

        /// <summary>
        /// Runs the compiled targeting sequence through ProcessControl.
        /// </summary>
        private async UniTask ActivateTargetingViaSequence(CancellationToken token, AbilityDataPacket implicitData)
        {
            var handler = implicitData.EffectOrigin.GetSource();
            // Refused, targeting ends here, and the ability with it. It used to run outside ProcessControl, where no
            // pause or terminate could reach it
            // In the caster's world, so its waits are the world's
            if (!ProcessControl.Register(CompiledTargeting).HandledBy(handler).With(implicitData).In(ProcessWorld.Of(handler)).Run(out var relay))
            {
                throw new OperationCanceledException();
            }

            TargetingRelay = relay;

            try
            {
                // Done in the frame targeting ends. Polling its relay found that a frame later
                await relay.WhenTerminated(token);
            }
            catch (OperationCanceledException)
            {
                CompiledTargeting?.Interrupt();
                throw;
            }
            finally
            {
                TargetingRelay = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SEQUENCE-BASED EXECUTION
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Activates the ability via the compiled sequence, registering with ProcessControl.
        /// </summary>
        public async UniTask Activate(CancellationToken token, AbilityDataPacket implicitData)
        {
            // Run asset loaders before any stages execute.
            // These are synchronous tasks that populate the data packet with
            // pre-configured assets (effects, entities, tags, etc.).
            RunAssetLoaders(implicitData, token);

            await ActivateViaSequence(token, implicitData);
        }

        /// <summary>
        /// Executes all configured asset loading tasks, populating the data packet
        /// with pre-configured assets before ability stages begin.
        /// </summary>
        private void RunAssetLoaders(AbilityDataPacket data, CancellationToken token)
        {
            if (Behaviour.AssetLoaders == null || Behaviour.AssetLoaders.Count == 0) return;

            data.AppendPath($"Assets[{Behaviour.AssetLoaders.Count}]");

            foreach (var loader in Behaviour.AssetLoaders)
            {
                if (loader == null) continue;
                
                loader.Prepare(data);
                loader.Activate(data, token);
                loader.Clean(data);
                
                data.AppendPath($"{loader.GetType().Name}");
            }
        }

        /// <summary>
        /// Runs the compiled execution TaskSequence through ProcessControl.
        /// </summary>
        private async UniTask ActivateViaSequence(CancellationToken token, AbilityDataPacket implicitData)
        {
            // Forward critical section exit from the compiled sequence up through the proxy.
            // AbilitySpecContainer wires OnCriticalSectionExited → handle.ReleaseClaimIfNeeded().
            CompiledSequence.OnCriticalSectionExited = () => OnCriticalSectionExited?.Invoke();

            // AbilityDataPacket extends SequenceDataPacket, so pass it directly.
            var handler = implicitData.EffectOrigin.GetSource();
            // Refused, the ability ends here. It used to run its stages outside ProcessControl instead, where no
            // pause or terminate could reach them
            // In the caster's world, so its waits are the world's
            var registration = ProcessControl.Register(CompiledSequence).HandledBy(handler).With(implicitData).In(ProcessWorld.Of(handler)).Run();
            var relay = registration.Relay;
            if (!registration)
            {
                ForgeLog.Warning(ForgeLog.ESystem.Abilities, $"ProcessControl refused an ability's sequence ({registration.Refusal}), so the ability " +
                                 "ends without running.");
                throw new OperationCanceledException();
            }

            ActiveRelay = relay;

            try
            {
                // Wait for the process to terminate (SelfTerminating lifecycle).
                // The relay state transitions: Created → Running → Terminated.
                // Done in the frame it does. Polling the relay found that a frame later
                await relay.WhenTerminated(token);
            }
            catch (OperationCanceledException)
            {
                // External cancellation (e.g., InterruptInjection from container).
                // Interrupt the running sequence so it cleans up properly.
                CompiledSequence?.Interrupt();
            }
            finally
            {
                implicitData.InUse = false;
                ActiveRelay = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // INJECTION (translates ability injections to sequence injections)
        // ═══════════════════════════════════════════════════════════════════════════

        public void Inject(ISequenceInjection injection, AbilityDataPacket activeData)
        {
            // Into the compiled sequence, which takes it where it's running; the ability's callback hears it either way,
            // with the stage it's in, or none. A second path, for no running sequence, gave the same callback
            var success = CompiledSequence.Inject(injection);
            FireInjectionCallback(injection, activeData, success);
        }

        private void FireInjectionCallback(
            ISequenceInjection injection,
            AbilityDataPacket activeData,
            bool success)
        {
            var owner = activeData.EffectOrigin.GetSource();
            if (!owner.FindAbilitySystem(out var asc)) return;

            var stageSafe = GetCurrentAbilityStage();

            var status = AbilityCallbackStatus.GenerateForInjection(
                activeData, stageSafe, injection, success);
            owner.GetFrameSummary().RecordAbilityInjection(status);

            asc.Callbacks.AbilityInjected(status);
        }

        /// <summary>
        /// Resolves the current AbilityTaskBehaviourStage from the running sequence's stage index.
        /// </summary>
        private AbilityTaskBehaviourStage GetCurrentAbilityStage()
        {
            if (!IsCompiled || CompiledSequence.Runtime == null)
                return new AbilityTaskBehaviourStage { Tasks = new List<AbstractAbilityTask>() };

            int idx = CompiledSequence.Runtime.StageIndex;
            if (idx >= 0 && idx < Behaviour.Stages.Count)
            {
                return Behaviour.Stages[idx];
            }

            return new AbilityTaskBehaviourStage { Tasks = new List<AbstractAbilityTask>() };
        }
    }
}
