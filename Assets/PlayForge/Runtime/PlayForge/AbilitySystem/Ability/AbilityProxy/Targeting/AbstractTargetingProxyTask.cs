using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractTargetingProxyTask : AbstractProxyTask
    {
        public override void Prepare(AbilityDataPacket data)
        {
            // Hook into input handler here
            if (ConnectInputHandler(data)) return;
            
            WhenTargetingInvalid();
            
            if (data.Spec.GetOwner().AsData().AbilitySystem && data.Spec is AbilitySpec spec)
            {
                spec.Owner.AsData().AbilitySystem.Inject(spec.Base, new InterruptInjection());
            }
        }
        
        public override void Clean(AbilityDataPacket data)
        {
            // Unhook from input handler here
            DisconnectInputHandler(data);
        }

        public virtual void WhenTargetingInvalid()
        {
            // Play some audio cue
        }

        public virtual void CommunicateToTarget(AbilityDataPacket data)
        {
            if (!data.TryGetFirstTarget(out var target)) return;
            
            // target.CommunicateTargetedIntent();
        }

        public override bool IsCriticalSection => true;

        /// <summary>
        /// Input handler can use data to derive visualization and validity
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected abstract bool ConnectInputHandler(AbilityDataPacket data);

        protected abstract void DisconnectInputHandler(AbilityDataPacket data);
    }
}
