using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Adapts an AbstractAbilityTask to the ISequenceTask interface.
    ///
    /// Since AbilityDataPacket extends SequenceDataPacket directly,
    /// the adapter simply casts the incoming data to AbilityDataPacket
    /// and delegates to the underlying ability task.
    ///
    /// IMPORTANT: Ability tasks must be stateless — all per-activation state
    /// must flow through the AbilityDataPacket, not as fields on the task.
    /// Shared [SerializeReference] instances are reused across activations.
    /// </summary>
    public class AbilityTaskAdapter : ISequenceTask
    {
        /// <summary>The underlying ability task being adapted.</summary>
        public readonly AbstractAbilityTask AbilityTask;

        // The stage the task is in, for the task callbacks
        private readonly AbilityTaskBehaviourStage _stage;

        // The task's part of the packet's path, its type's name, read once: it was read on every run (TS-5)
        private readonly string _pathPart;

        public AbilityTaskAdapter(AbstractAbilityTask abilityTask, AbilityTaskBehaviourStage stage = null)
        {
            AbilityTask = abilityTask;
            _stage = stage;
            _pathPart = abilityTask?.GetType().Name;
        }

        public void Prepare(SequenceDataPacket data)
        {
            if (data is AbilityDataPacket abilityData)
            {
                AbilityTask.Prepare(abilityData);
            }
        }

        public async UniTask Execute(SequenceDataPacket data, CancellationToken token)
        {
            if (data is AbilityDataPacket abilityData)
            {
                abilityData.AppendPath(_pathPart);

                // The task callbacks fired only on the legacy path, which a compiled ability never takes (TS-9)
                abilityData.Callbacks?.AbilityTaskActivated(AbilityCallbackStatus.GenerateForTask(abilityData, AbilityTask, _stage, true));

                bool completed = false;
                try
                {
                    await AbilityTask.Activate(abilityData, token);
                    completed = true;
                }
                finally
                {
                    abilityData.Callbacks?.AbilityTaskEnded(AbilityCallbackStatus.GenerateForTask(abilityData, AbilityTask, _stage, completed));
                }
            }
        }

        public bool Step(SequenceDataPacket data, float deltaTime) => true; // Ability tasks are async-only

        public void Clean(SequenceDataPacket data)
        {
            if (data is AbilityDataPacket abilityData)
            {
                AbilityTask.Clean(abilityData);
            }
        }
    }
}
