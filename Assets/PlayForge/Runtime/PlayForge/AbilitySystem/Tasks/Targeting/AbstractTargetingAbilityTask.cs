using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractTargetingAbilityTask : AbstractAbilityRelatedTask
    {
        [Tooltip("If target validation fails, break out of Ability runtime")]
        public bool BreakRuntimeOnInvalid = true;
        
        public virtual string Description => null;
        
        /// <summary>
        /// Prepare targeting measures.
        /// When overriding, put calls to base at the end of the method.
        /// </summary>
        /// <param name="data"></param>
        public override void Prepare(AbilityDataPacket data)
        {
            // Hook into input handler here
            if (ConnectInputHandler(data)) return;
            
            WhenTargetingInvalid();

            var abilitySystem = data.Spec.GetOwner().AsGAS().GetAbilitySystem();
            if (abilitySystem is not null && data.Spec is AbilitySpec spec)
            {
                abilitySystem.Inject(spec.Base, new InterruptInjection());
            }
        }
        
        public override void Clean(AbilityDataPacket data)
        {
            // Unhook from input handler here
            DisconnectInputHandler(data);
        }

        protected virtual void WhenTargetingInvalid()
        {
            // Play some audio cue

            if (BreakRuntimeOnInvalid) BreakAbilityRuntime();
        }

        protected virtual void BreakAbilityRuntime()
        {
            throw new OperationCanceledException();
        }

        protected virtual bool TargetIsValid(ITarget target)
        {
            return target is not null && TargetIsValid(target.AsGAS().ToGASObject()?.transform, out _);
        }

        protected virtual bool TargetIsValid(GameObject go, out ITarget target)
        {
            var targets = go.GetComponents<ITarget>();
            if (targets.Length > 0 && targets.All(TargetIsValid))
            {
                target = targets.First();
                return TargetIsValid(target);
            }

            target = null;
            return false;
        }

        protected virtual bool TargetIsValid(Transform transform, out Vector3 target)
        {
            return TargetIsValid(transform.position, out target);
        }

        protected virtual bool TargetIsValid(Vector3 position, out Vector3 target)
        {
            target = position;
            return true;
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
