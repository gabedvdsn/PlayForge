using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class RunProcessTask : AbstractAbilityTask
    {
        [SerializeReference] public List<AbstractRuntimeProcess> Processes;
        public PlayerLoopTiming UpdateTiming = PlayerLoopTiming.Update;
        public float MaxDuration = 999f;
        // Scaled by default: unscaled, a process ran on apart from the game's time scale and its caster's (TS-6)
        public bool UseUnscaledDeltaTime;
        
        public override bool IsCriticalSection => false;

        // The relays are the activation's, so they're kept on its packet. Kept here, they were replaced by the next
        // activation's, which left the first one's processes running for good (TS-U)
        public override void Prepare(AbilityDataPacket data)
        {
            var relays = data.GetTaskState<List<ProcessRelay>>(this);
            relays.Clear();

            foreach (var p in Processes)
            {
                var caster = data.EffectOrigin.GetSource();
                if (!ProcessControl.Register(p).HandledBy(caster).In(ProcessWorld.Of(caster)).Run(out var relay)) continue;
                relays.Add(relay);
            }
        }
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var relays = data.GetTaskState<List<ProcessRelay>>(this);
            float elapsedDuration = 0f;
            await ProcessTaskUtil.DoWhileAsync(
                body: () =>
                {
                    if (SequenceTaskLibrary.IsPaused(token)) return UniTask.CompletedTask;

                    elapsedDuration += UseUnscaledDeltaTime ? ForgeTime.UnscaledDeltaTime : ForgeTime.DeltaTimeFor(data);
                    return UniTask.CompletedTask;
                },
                condition: () => 
                    elapsedDuration < MaxDuration 
                    && relays.TrueForAny(relay => relay.ProcessActive),
                token: token,
                timing: UpdateTiming
            );
        }

        public override void Clean(AbilityDataPacket data)
        {
            var relays = data.GetTaskState<List<ProcessRelay>>(this);

            foreach (var relay in relays)
            {
                // Through the relay, to the control that holds the process (FEAT-10)
                if (relay.ProcessActive) relay.TerminateImmediate();
            }

            relays.Clear();
        }

    }
}
