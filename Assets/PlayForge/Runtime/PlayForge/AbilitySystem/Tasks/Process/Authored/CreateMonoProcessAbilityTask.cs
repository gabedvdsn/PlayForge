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
            foreach (var process in ProcessesFor(data))
            {
                ProcessControl.Register(process).HandledBy(source).With(data).In(ProcessWorld.Of(source)).Run();
            }

            return UniTask.CompletedTask;
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
