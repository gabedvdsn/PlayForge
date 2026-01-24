using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class DelayTask : AbstractAbilityTask
    {
        public int DelayMilliseconds;
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            Debug.Log($"Start delay");
            await UniTask.Delay(DelayMilliseconds, cancellationToken: token);
            Debug.Log($"End delay");
        }
        public override bool IsCriticalSection => false;
    }
}
