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
                if (Input.GetKeyDown(KeyCode.Escape)) break;
                if (Input.GetMouseButtonDown(0))
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity))
                    {
                        Debug.Log("Hit: " + hitInfo.collider.name);
                        // Access hitInfo.point, hitInfo.normal, hitInfo.collider, etc.
                        return;
                    }
                }
                
                await UniTask.NextFrame(token);
            }
            UnityEngine.Debug.Log($"Out of targeting");
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
