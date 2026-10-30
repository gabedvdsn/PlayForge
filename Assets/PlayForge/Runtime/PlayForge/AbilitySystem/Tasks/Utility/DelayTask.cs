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
            await SequenceTaskLibrary.Delay(DelayMilliseconds / 1000f, token);
        }
        public override bool IsCriticalSection => false;
    }
}
