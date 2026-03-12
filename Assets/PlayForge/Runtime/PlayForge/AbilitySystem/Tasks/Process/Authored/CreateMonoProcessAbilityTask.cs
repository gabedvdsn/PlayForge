using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class CreateMonoProcessAbilityTask : AbstractCreateProcessAbilityTask
    {
        [SerializeReference] public List<AbstractMonoProcess> MonoProcesses = new();

        public CreateMonoProcessAbilityTask()
        {
        }

        public override string Description => "Register & instantiate mono processes";

        public CreateMonoProcessAbilityTask(List<AbstractMonoProcess> monoProcesses)
        {
            MonoProcesses = monoProcesses;
        }
        
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            foreach (var process in MonoProcesses)
            {
                ProcessControl.Instance.Register(process, data, out _);
            }
            
            return UniTask.CompletedTask;
        }
        
        public override bool IsCriticalSection => false;
    }
}
