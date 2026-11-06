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
        
        public override bool IsCriticalSection => true;

        /// <summary>
        /// Prepare targeting measures.
        /// When overriding, put calls to base at the end of the method.
        /// </summary>
        public override void Prepare(AbilityDataPacket data)
        {
            // Hook into input handler here
            if (ConnectInputHandler(data)) return;

            // If connection fails, interrupt ability
            WhenTargetingInvalid(data);

            var abilitySystem = data.EffectOrigin.GetSource().ToGAS().GetAbilitySystem();
            if (abilitySystem is not null && data.EffectOrigin is AbilitySpec spec)
            {
                abilitySystem.Inject(spec.Base, InterruptSequenceInjection.Instance);
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
            // Through the target's packet, which both hubs have. Through its GameObject, a runtime GAS, which has
            // none, threw (GAS-29)
            if (target?.GetTargetingPacket() is not { } packet) return false;
            return packet.HasTransform ? TargetIsValid(packet.transform, out _) : TargetIsValid(packet.position, out _);
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
            // A missing or destroyed transform is no target. It threw (GAS-29)
            target = default;
            return transform && TargetIsValid(transform.position, out target);
        }

        protected virtual bool TargetIsValid(Vector3 position, out Vector3 target)
        {
            target = position;
            return true;
        }

        /// <summary>
        /// The target the activation's data already holds under TARGET_REAL, as an AI's or a replay's activation does.
        /// A click-targeting task takes it rather than wait for a click, so the ability an AI casts is the one a player
        /// clicks. They waited for a click whatever the data held, so an AI couldn't cast them (GAS-66).
        /// </summary>
        protected static bool TryGetDataTarget(AbilityDataPacket data, out ITarget target) =>
            data.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out target) && target != null;

        /// <summary>The point the activation's data already holds under POSITION, as for <see cref="TryGetDataTarget"/> (GAS-66).</summary>
        protected static bool TryGetDataPoint(AbilityDataPacket data, out Vector3 point) =>
            data.TryGet(Tags.POSITION, EDataTarget.Primary, out point);

        /// <summary>
        /// Waits for the targeting input to press the button, and gives the ray it aims along as it does: by default,
        /// from the camera through the pointer. Null is a press with nothing to aim through, as with no camera. The
        /// click-targeting tasks read the Input Manager's mouse and Camera.main themselves, and threw with no camera
        /// (FEAT-11).
        /// </summary>
        protected static async UniTask<Ray?> WaitForPointerPress(int button, CancellationToken token)
        {
            await UniTask.WaitUntil(() => ForgeInput.Current.PointerPressed(button), cancellationToken: token);
            return ForgeInput.Current.TryGetPointerRay(out var ray) ? ray : null;
        }

        /// <summary>
        /// Input handler can use data to derive visualization and validity.
        /// Return true if input handler connected successfully.
        /// Return false if connection failed (targeting will be marked invalid).
        /// </summary>
        protected virtual bool ConnectInputHandler(AbilityDataPacket data)
        {
            return true;
        }

        protected virtual void DisconnectInputHandler(AbilityDataPacket data)
        {
        }
    }
}
