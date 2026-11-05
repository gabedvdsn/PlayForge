using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ChannelAbilityTask : AbstractAbilityTask
    {
        public float ChannelDuration;
        public PlayerLoopTiming Timing = PlayerLoopTiming.Update;
        
        public override void Prepare(AbilityDataPacket data)
        {
            //SliderManager.Instance.ToggleSlider(true);
        }

        public override void Clean(AbilityDataPacket data)
        {
            //SliderManager.Instance.ToggleSlider(false);
        }
        public override bool IsCriticalSection => true;
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float elapsedDuration = 0f;

            await ProcessTaskUtil.DoWhileAsync(
                body: () =>
                {
                    elapsedDuration += SequenceTaskLibrary.DeltaTime(token);
                    //SliderManager.Instance.SetValue(elapsedDuration / ChannelDuration);
                    return UniTask.CompletedTask;
                },
                condition: () => elapsedDuration < ChannelDuration,
                token: token,
                timing: Timing
            );
            /*while (elapsedDuration < ChannelDuration)
            {
                elapsedDuration += ForgeTime.DeltaTime;
                SliderManager.Instance.SetValue(elapsedDuration / ChannelDuration);

                await ForgeTick.NextFrame(token);
            }*/
        }
    }
}
