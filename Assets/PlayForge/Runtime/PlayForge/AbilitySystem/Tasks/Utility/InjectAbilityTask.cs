using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A stage task that sends its <c>Injection</c>, an interrupt unless it's set to another, as a skip or a jump to a
    /// stage, into its ability's running activations, and ends at once.
    /// </summary>
    [ForgeMenu("Sequence", Description = "Sends a command into the ability's own run, as an interrupt.")]
    public class InjectAbilityTask : AbstractAbilityTask
    {
        // A new task interrupts by default. The drawer wrote this into an empty field as it drew, which no longer
        // happens; the type picker runs this initializer when it makes the task
        [SerializeReference] public ISequenceInjection Injection = new InterruptSequenceInjection();

        public override string Description => "Inject a command into the Ability's runtime";

        public override bool IsCriticalSection => false;
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.EffectOrigin.GetSource().FindAbilitySystem(out var asc)
                && data.EffectOrigin is AbilitySpec spec) asc.Inject(spec.Base, Injection);
            
            return UniTask.CompletedTask;
        }
    }
}
