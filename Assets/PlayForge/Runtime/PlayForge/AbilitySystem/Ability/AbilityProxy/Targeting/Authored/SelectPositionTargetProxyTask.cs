using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SelectPositionTargetProxyTask : AbstractTargetingProxyTask
    {
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            while (true)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) break;
                if (Input.GetMouseButtonDown(0))
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
                    {
                        data.AddPayload(Tags.PAYLOAD_TARGET, hitInfo.point);
                        break;
                    }
                }
                
                await UniTask.NextFrame(token);
            }
            await UniTask.CompletedTask;
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
