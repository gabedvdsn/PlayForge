using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ChannelProxyTask : AbstractProxyTask
    {
        public float ChannelDuration;
        public PlayerLoopTiming Timing = PlayerLoopTiming.Update;
        
        public override void Prepare(AbilityDataPacket data)
        {
            SliderManager.Instance.ToggleSlider(true);
        }

        public override void Clean(AbilityDataPacket data)
        {
            SliderManager.Instance.ToggleSlider(false);
        }
        public override bool IsCriticalSection => false;

        private void OnValidate()
        {
            ReadOnlyDescription = "Holds a channeling state for the assigned duration, and updates the Slider value while doing so";
        }
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float elapsedDuration = 0f;

            await TaskUtil.DoWhileAsync(
                body: async () =>
                {
                    elapsedDuration += Time.deltaTime;
                    SliderManager.Instance.SetValue(elapsedDuration / ChannelDuration);
                    await UniTask.Yield(Timing, token);
                },
                condition: () => elapsedDuration < ChannelDuration,
                token: token
            );
            /*while (elapsedDuration < ChannelDuration)
            {
                elapsedDuration += Time.deltaTime;
                SliderManager.Instance.SetValue(elapsedDuration / ChannelDuration);

                await UniTask.NextFrame(token);
            }*/
        }
    }
}
