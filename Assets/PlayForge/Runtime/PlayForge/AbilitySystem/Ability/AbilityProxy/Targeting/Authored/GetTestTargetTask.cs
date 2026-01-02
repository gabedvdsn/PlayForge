using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class GetTestTargetTask : AbstractTargetingAbilityTask
    {
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var comps = Object.FindObjectsByType<GameplayAbilitySystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (!data.TryGet(Tags.PAYLOAD_TARGET, EProxyDataValueTarget.Primary, out ISource source))
            {
                return UniTask.CompletedTask;
            }

            var gas = source.AsGAS();
            
            foreach (var comp in comps)
            {
                if (comp != (GameplayAbilitySystem)gas && comp != GameRoot.Instance)
                {
                    data.AddPayload(Tags.PAYLOAD_TARGET, comp);
                    break;
                }
            }
            
            return UniTask.CompletedTask;
        }
        
        protected override bool ConnectInputHandler(AbilityDataPacket data)
        {
            return true;
        }
        protected override void DisconnectInputHandler(AbilityDataPacket data)
        {
            
        }
    }
}
