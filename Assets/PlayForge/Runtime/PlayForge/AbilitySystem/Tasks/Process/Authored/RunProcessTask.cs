using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A stage task that runs its <c>Processes</c> as its stage starts, each registered for the caster, and waits while
    /// any still runs, up to <c>MaxDuration</c> seconds read at the ability's level, counted by its run's clock or,
    /// with <c>UseUnscaledDeltaTime</c>, unscaled; as the stage ends, it ends those still running.
    /// </summary>
    [ForgeMenu("Process", Description = "Runs processes for the caster and waits for them, up to a time.")]
    public class RunProcessTask : AbstractAbilityTask
    {
        [Tooltip("The processes it runs, each registered for its caster as it starts")]
        [SerializeReference] public List<AbstractRuntimeProcess> Processes;

        [Tooltip("The step of Unity's player loop it counts its time in")]
        public PlayerLoopTiming UpdateTiming = PlayerLoopTiming.Update;

        [Tooltip("The longest it waits for its processes, in seconds, read as it starts at its ability's level")]
        [NumberMin(0f)] public ScalableFloat MaxDuration = 999f;

        // Scaled by default: unscaled, a process ran on apart from the game's time scale and its caster's
        [Tooltip("Counts its time unscaled, apart from the game's time scale and its caster's")]
        public bool UseUnscaledDeltaTime;
        
        public override bool IsCriticalSection => false;

        // The relays are the activation's, so they're kept on its packet. Kept here, they were replaced by the next
        // activation's, which left the first one's processes running for good
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
            float most = Read(MaxDuration, data);
            float elapsedDuration = 0f;
            await ProcessTaskUtil.DoWhileAsync(
                body: () =>
                {
                    if (SequenceTaskLibrary.IsPaused(token)) return UniTask.CompletedTask;

                    elapsedDuration += (UseUnscaledDeltaTime ? UnscaledClock.Instance : TaskSequenceRuntime.ClockOfRun(token, data)).DeltaTime;
                    return UniTask.CompletedTask;
                },
                condition: () => 
                    elapsedDuration < most 
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
                // Through the relay, to the control that holds the process
                if (relay.ProcessActive) relay.TerminateImmediate();
            }

            relays.Clear();
        }

    }
}
