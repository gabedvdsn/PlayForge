using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge.Examples;
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
        public override void Prepare(AbilityDataPacket data)
        {
            // Hook into input handler here
            if (ConnectInputHandler(data)) return;

            WhenTargetingInvalid(data);

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

        /// <summary>
        /// Called when targeting produces an invalid result. Marks the data packet
        /// as failed and optionally breaks the ability runtime.
        /// Prefer this overload over the parameterless version in Activate() —
        /// it ensures the TargetingFailed flag is set before any throw.
        /// </summary>
        protected virtual void WhenTargetingInvalid(AbilityDataPacket data)
        {
            data.TargetingFailed = true;
            WhenTargetingInvalid();
        }

        /// <summary>
        /// Parameterless fallback for subclass overrides that don't have data access.
        /// Prefer WhenTargetingInvalid(data) when the data packet is available.
        /// </summary>
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
        /// Input handler can use data to derive visualization and validity.
        /// Return true if input handler connected successfully.
        /// Return false if connection failed (targeting will be marked invalid).
        /// </summary>
        protected virtual bool ConnectInputHandler(AbilityDataPacket data)
        {
            DemoManager.Input.SetTargetingCursor();
            return true;
        }

        protected virtual void DisconnectInputHandler(AbilityDataPacket data)
        {
            DemoManager.Input.ResetTargetingCursor();
        }
    }
}
