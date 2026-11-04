using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class InjectAbilityTask : AbstractAbilityTask
    {
        // A new task interrupts by default. The drawer wrote this into an empty field as it drew, which no longer
        // happens; the type picker runs this initializer when it makes the task (ED-12)
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
