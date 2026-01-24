using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class InjectAbilityTask : AbstractAbilityTask
    {
        [SerializeReference] public IAbilityInjection Injection;

        public override string Description => "Inject a command into the Ability's runtime";

        public override bool IsCriticalSection => false;
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.Spec.GetOwner().FindAbilitySystem(out var asc)
                && data.Spec is AbilitySpec spec) asc.Inject(spec.Base, Injection);
            
            return UniTask.CompletedTask;
        }
    }
}
