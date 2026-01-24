using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SelectGasTargetTask : AbstractGasTargetingAbilityTask
    {
        public override string Description => "Use raycast to find GAS target";
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            // wait for response from some cursor manager that receives mouse input and finds the selected gameobject that has a GASComponent
            // await CursorManager.Instance.SetSelectTargetObjectMode();
            // if (CursorManager.Instance.LastSelectTargetObject) data.Add(ESourceTarget.Target, CursorManager.Instance.LastSelectTargetObject);
            while (true)
            {
                // Important to have some break response -- OR inject interrupt into ASC via inut handler
                if (Input.GetKeyDown(KeyCode.Escape)) BreakAbilityRuntime();
                if (Input.GetMouseButtonDown(0))
                {
                    var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity))
                    {
                        if (!TargetIsValid(hitInfo.collider.gameObject, out var target))
                        {
                            WhenTargetingInvalid();
                            continue;
                        }
                        
                        data.AddPayload(Tags.TARGET_REAL, target);
                        
                        // Access hitInfo.point, hitInfo.normal, hitInfo.collider, etc.
                        break;
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
