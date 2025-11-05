using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class DelayProxyTask : AbstractProxyTask
    {
        public int DelayMilliseconds;
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            await UniTask.Delay(DelayMilliseconds, cancellationToken: token);
        }
        public override bool IsCriticalSection => false;
    }
}
