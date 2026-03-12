using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SelectPositionTargetTask : AbstractTargetingAbilityTask
    {
        public override string Description => "Use raycast to find ground target position";
        public override void Prepare(AbilityDataPacket data)
        {
            base.Prepare(data);
        }
        public override void Clean(AbilityDataPacket data)
        {
            base.Clean(data);
        }
        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            while (true)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) BreakAbilityRuntime();
                if (Input.GetMouseButtonDown(0))
                {
                    var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity))
                    {
                        if (!TargetIsValid(hitInfo.point, out var position))
                        {
                            WhenTargetingInvalid();
                            break;
                        }
                        
                        data.AddPayload(Tags.POSITION, position);
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
