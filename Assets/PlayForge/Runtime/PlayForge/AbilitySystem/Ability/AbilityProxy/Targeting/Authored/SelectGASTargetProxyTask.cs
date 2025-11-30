using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SelectGASTargetProxyTask : AbstractTargetingProxyTask
    {
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            // wait for response from some cursor manager that receives mouse input and finds the selected gameobject that has a GASComponent
            // await CursorManager.Instance.SetSelectTargetObjectMode();
            // if (CursorManager.Instance.LastSelectTargetObject) data.Add(ESourceTarget.Target, CursorManager.Instance.LastSelectTargetObject);
            while (true)
            {
                // Important to have some break response -- OR inject interrupt into ASC via inut handler
                if (Input.GetKeyDown(KeyCode.Escape)) break;
                if (Input.GetMouseButtonDown(0))
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
                    {
                        data.AddPayload(Tags.PAYLOAD_TARGET, hitInfo.collider.gameObject);
                        
                        // Access hitInfo.point, hitInfo.normal, hitInfo.collider, etc.
                        return;
                    }
                }
                
                await UniTask.NextFrame(token);
            }
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
