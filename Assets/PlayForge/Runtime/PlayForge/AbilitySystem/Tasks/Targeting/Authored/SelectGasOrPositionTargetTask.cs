using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SelectGasOrPositionTargetTask : AbstractGasTargetingAbilityTask
    {

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            while (true)
            {
                // Important to have some break response -- OR inject interrupt into ASC via inut handler
                if (Input.GetKeyDown(KeyCode.Escape)) BreakAbilityRuntime();
                if (Input.GetMouseButtonDown(0))
                {
                    var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity))
                    {
                        // We found real target
                        if (TargetIsValid(hitInfo.collider.gameObject, out var target))
                        {
                            data.AddPayload(Tags.TARGET_REAL, target);
                            break;
                        }
                        
                        // We found real position
                        if (TargetIsValid(hitInfo.collider.transform, out var position))
                        {
                            data.AddPayload(Tags.POSITION, position);
                            break;
                        }
                        
                        WhenTargetingInvalid();
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
