using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Waits: holds its stage for its delay, in its caster's time, read as it starts at its ability's level (FN-24).</summary>
    [ForgeCategory("Waits")]
    public class DelayTask : AbstractAbilityTask
    {
        [Tooltip("How long it waits, in milliseconds of its caster's time")]
        [NumberMin(0f)] public ScalableInt DelayMilliseconds = 0;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            await SequenceTaskLibrary.Delay(Read(DelayMilliseconds, data) / 1000f, token);
        }

        public override bool IsCriticalSection => false;
    }
}
