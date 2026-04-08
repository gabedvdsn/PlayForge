using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class CreateMonoProcessAbilityTask : AbstractCreateProcessAbilityTask
    {
        public Tag DataTag = Tags.DATA;
        [Tooltip("When true, retrieve data as an enumerable.")]
        public bool RegisterMany = false;
        public EDataTarget DataTarget = EDataTarget.Primary;
        [Tooltip("When true, processes inside [InternalMonoProcesses] override data.")]
        public bool InternalIsOverride = false;
        [FormerlySerializedAs("MonoProcesses")] [SerializeReference] public List<AbstractMonoProcess> InternalMonoProcesses = new();

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
            if (!InternalIsOverride)
            {
                if (!RegisterMany && data.TryGet<AbstractMonoProcess>(DataTag, DataTarget, out var process))
                {
                    ProcessControl.Register(process, data.Spec.GetOwner(), data, out _);
                }
                else if (RegisterMany && data.TryGet<IEnumerable<AbstractMonoProcess>>(DataTag, DataTarget, out var processes))
                {
                    foreach (var p in processes)
                    {
                        ProcessControl.Register(p, data.Spec.GetOwner(), data, out _);
                    }
                }
            }
            else
            {
                foreach (var process in InternalMonoProcesses)
                {
                    ProcessControl.Register(process, data.Spec.GetOwner(), data, out _);
                }
            }
            
            return UniTask.CompletedTask;
        }
        
        public override bool IsCriticalSection => false;
    }
}
