using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of an ability's targeting task, the one task that picks what an activation acts on before its stages
    /// run, as a click picks an entity or a search takes the enemies in range. Its <c>Activate</c> files the targeting
    /// packet of what it picks under the activation's Target key, whose entity becomes the activation's target, or
    /// calls <c>WhenTargetingInvalid</c>, which ends the activation where <c>BreakRuntimeOnInvalid</c> is on. Targeting
    /// holds the activation's claim while it runs, and its slot's <c>IsTargeting</c> says while it chooses.
    ///
    /// <para>A task of your own overrides <c>Activate</c>; <c>Prompt</c>, the words a game shows while it waits for the
    /// player; <c>TargetIsValid</c>, which targets it takes; and <c>ConnectInputHandler</c> and
    /// <c>DisconnectInputHandler</c>, to hook an input handler of its own as it's prepared and unhook it as it's
    /// cleaned, an override of <c>Prepare</c> calling the base's last. <c>TryGetDataTarget</c> and
    /// <c>TryGetDataPoint</c> take a target or a point the activation's data already holds, as an AI's does, and
    /// <c>WaitForPointerPress</c> waits for a press of the game's input.</para>
    /// </summary>
    public abstract class AbstractTargetingAbilityTask : AbstractAbilityRelatedTask
    {
        [Tooltip("If target validation fails, break out of Ability runtime")]
        public bool BreakRuntimeOnInvalid = true;
        public virtual string Description => null;

        /// <summary>
        /// The words a game shows while the task waits for the player to choose, as "Click a point on the ground", beside
        /// the ability's name; null for one that doesn't wait for the player, as an automatic one. A slot's
        /// <see cref="AbilitySpecContainer.IsTargeting"/> says when it waits.
        /// </summary>
        public virtual string Prompt => null;

        /// <summary>The words for pressing a pointer's button: "Click", "Right-click" or "Middle-click".</summary>
        protected static string PressWords(int button) => button switch
        {
            1 => "Right-click",
            2 => "Middle-click",
            _ => "Click",
        };

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
            // none, threw
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
            // A missing or destroyed transform is no target. It threw
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
        /// clicks. They waited for a click whatever the data held, so an AI couldn't cast them.
        /// </summary>
        protected static bool TryGetDataTarget(AbilityDataPacket data, out ITarget target) =>
            data.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out target) && target != null;

        /// <summary>The point the activation's data already holds under POSITION, as for <see cref="TryGetDataTarget"/>.</summary>
        protected static bool TryGetDataPoint(AbilityDataPacket data, out Vector3 point) =>
            data.TryGet(Tags.POSITION, EDataTarget.Primary, out point);

        /// <summary>
        /// Waits for the targeting input to press the button, and gives the ray it aims along as it does: by default,
        /// from the camera through the pointer. Null is a press with nothing to aim through, as with no camera. The
        /// click-targeting tasks read the Input Manager's mouse and Camera.main themselves, and threw with no camera.
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
