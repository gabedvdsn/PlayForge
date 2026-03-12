using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace FarEmerald.PlayForge
{
    public class UnityEventAbilityTask : AbstractAbilityTask
    {
        public List<UnityEvent> Events;
        
        public override bool IsCriticalSection => false;
        
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            foreach (var e in Events) e?.Invoke();            
            return UniTask.CompletedTask;
        }
    }
}
