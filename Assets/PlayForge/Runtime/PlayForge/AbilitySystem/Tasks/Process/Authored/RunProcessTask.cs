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
        public bool UseUnscaledDeltaTime = true;
        
        public override bool IsCriticalSection => false;

        private List<ProcessRelay> relays;
        
        public override void Prepare(AbilityDataPacket data)
        {
            relays = new List<ProcessRelay>();
            foreach (var p in Processes)
            {
                if (!ProcessControl.Register(p, data.EffectOrigin.GetOwner(), out var relay)) continue;
                relays.Add(relay);
            }
        }
        
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            float elapsedDuration = 0f;
            await ProcessTaskUtil.DoWhileAsync(
                body: async () =>
                {
                    elapsedDuration += UseUnscaledDeltaTime ? Time.unscaledDeltaTime : Time.deltaTime;
                },
                condition: () => 
                    elapsedDuration < MaxDuration 
                    && relays.TrueForAny(relay => ProcessControl.Instance.IsRegistered(relay.CacheIndex)),
                token: token,
                timing: UpdateTiming
            );
        }

        public override void Clean(AbilityDataPacket data)
        {
            if (relays is null || relays.Count == 0) return;

            foreach (var relay in relays)
            {
                if (!ProcessControl.Instance.IsRegistered(relay.CacheIndex)) continue;
                if (!ProcessControl.Instance.TerminateImmediate(relay.CacheIndex)) continue;
            }
        }

    }
}
