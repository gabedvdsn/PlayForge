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

        public AbilityTaskAdapter(AbstractAbilityTask abilityTask)
        {
            AbilityTask = abilityTask;
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
                await AbilityTask.Activate(abilityData, token);
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
