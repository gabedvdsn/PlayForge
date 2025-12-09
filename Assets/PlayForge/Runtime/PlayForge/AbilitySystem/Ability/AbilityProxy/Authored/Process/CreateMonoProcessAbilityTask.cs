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
        protected List<AbstractMonoProcess> MonoProcesses;
        
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
