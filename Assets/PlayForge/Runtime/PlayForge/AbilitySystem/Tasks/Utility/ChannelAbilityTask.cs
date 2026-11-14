using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Channels: holds its stage for its duration, in its caster's time, read as it starts at its ability's level, a
    /// critical section, as a cast bar fills (FN-24). It kept a cast bar's calls commented out, which nothing drew.
    /// </summary>
    [ForgeCategory("Waits")]
    public class ChannelAbilityTask : AbstractAbilityTask
    {
        [Tooltip("How long it channels, in seconds of its caster's time")]
        [NumberMin(0f)] public ScalableFloat ChannelDuration = 0f;

        [Tooltip("The step of Unity's player loop it counts its time in")]
        public PlayerLoopTiming Timing = PlayerLoopTiming.Update;

        public override bool IsCriticalSection => true;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float duration = Read(ChannelDuration, data);
            float elapsed = 0f;
            await ProcessTaskUtil.DoWhileAsync(
                body: () =>
                {
                    elapsed += SequenceTaskLibrary.DeltaTime(token);
                    return UniTask.CompletedTask;
                },
                condition: () => elapsed < duration,
                token: token,
                timing: Timing
            );
        }
    }
}
