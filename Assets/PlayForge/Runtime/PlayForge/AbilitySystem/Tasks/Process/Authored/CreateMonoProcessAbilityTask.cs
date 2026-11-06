using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Spawns mono processes, as a projectile, as the ability runs. What it spawns takes the ability as its origin, so the
    /// effects it applies are the caster's at the ability's level, and its attributes can start as the caster's (GAS-65).
    /// It spawns the processes the activation's data holds under <see cref="DataTag"/>, or, where the data holds none or
    /// <see cref="InternalIsOverride"/> is on, its own.
    /// </summary>
    public class CreateMonoProcessAbilityTask : AbstractCreateProcessAbilityTask
    {
        public Tag DataTag = Tags.DATA;
        [Tooltip("When true, retrieve data as an enumerable.")]
        public bool RegisterMany = false;
        public EDataTarget DataTarget = EDataTarget.Primary;
        [Tooltip("When true, the processes below are spawned whatever the data holds. When false, they're spawned where the data holds none.")]
        public bool InternalIsOverride = false;
        [FormerlySerializedAs("MonoProcesses")] public List<AbstractMonoProcess> InternalMonoProcesses = new();
        [Tooltip("Where a spawned process appears: where the data's Position says, or else where its prefab stands; at the caster, as a projectile does; or at the ability's target, as a fire on the ground does")]
        public ESpawnPoint SpawnAt = ESpawnPoint.Data;

        /// <summary>Where a spawned process appears (GAS-67).</summary>
        public enum ESpawnPoint
        {
            [InspectorName("As Its Data Says")] Data,
            [InspectorName("At the Caster")] Caster,
            [InspectorName("At the Target")] Target,
        }

        public CreateMonoProcessAbilityTask()
        {
        }

        public override string Description => "Register & instantiate mono processes";

        public CreateMonoProcessAbilityTask(List<AbstractMonoProcess> internalMonoProcesses)
        {
            InternalMonoProcesses = internalMonoProcesses;
        }

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            // What it spawns takes the ability as its origin. Nothing set it, so a process fell back to GameRoot, and a
            // projectile took GameRoot's speed and applied GameRoot's effects (GAS-65)
            if (!data.Contains(Tags.DERIVATION)) data.SetPrimary(Tags.DERIVATION, data.EffectOrigin);

            var source = data.EffectOrigin.GetSource();

            // Placed where Spawn At says for its registration alone, as a process places itself at its data's Position as it
            // starts, and the data put back after, since the ability's other tasks read it too. A process appeared where
            // its prefab stood unless the data held a Position, so a projectile left the world's origin (GAS-67)
            bool placed = TrySpawnPoint(data, source, out var point);
            object before = null;
            bool held = placed && data.TryGet(Tags.POSITION, EDataTarget.Primary, out before);
            if (placed) data.SetPrimary(Tags.POSITION, point);
            try
            {
                foreach (var process in ProcessesFor(data))
                {
                    ProcessControl.Register(process).HandledBy(source).With(data).In(ProcessWorld.Of(source)).Run();
                }
            }
            finally
            {
                if (placed)
                {
                    if (held) data.SetPrimary(Tags.POSITION, before);
                    else data.Remove(Tags.POSITION);
                }
            }

            return UniTask.CompletedTask;
        }

        // The point Spawn At names: the caster's place, or the ability's target's. False where it's the data's, or the
        // place can't be found
        private bool TrySpawnPoint(AbilityDataPacket data, ISource source, out Vector3 point)
        {
            point = default;
            switch (SpawnAt)
            {
                case ESpawnPoint.Caster:
                    if (source?.GetTargetingPacket() is not { } caster) return false;
                    point = caster.position;
                    return true;
                case ESpawnPoint.Target:
                    if (!data.GetTargetingPacket(Tags.TARGET, out var target, autoInterrupt: false) || target == null) return false;
                    point = target.position;
                    return true;
                default:
                    return false;
            }
        }

        // The data's processes, or, where it holds none or the task's own override it, the task's own. The task's own
        // were spawned only with InternalIsOverride on, so a task given processes and nothing else spawned none (GAS-65)
        private IEnumerable<AbstractMonoProcess> ProcessesFor(AbilityDataPacket data)
        {
            if (!InternalIsOverride)
            {
                if (!RegisterMany && data.TryGet<AbstractMonoProcess>(DataTag, DataTarget, out var process) && process)
                {
                    yield return process;
                    yield break;
                }
                if (RegisterMany && data.TryGet<IEnumerable<AbstractMonoProcess>>(DataTag, DataTarget, out var processes) && processes != null)
                {
                    foreach (var each in processes)
                        if (each) yield return each;
                    yield break;
                }
            }

            if (InternalMonoProcesses == null) yield break;
            foreach (var each in InternalMonoProcesses)
                if (each) yield return each;
        }

        public override bool IsCriticalSection => false;
    }
}
