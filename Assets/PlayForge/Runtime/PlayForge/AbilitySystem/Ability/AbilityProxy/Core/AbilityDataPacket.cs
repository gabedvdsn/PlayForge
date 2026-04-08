using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Data packet for ability execution. Extends SequenceDataPacket so that
    /// abilities run through the TaskSequence system natively — the same packet
    /// flows through targeting, execution stages, callbacks, and ProcessControl.
    ///
    /// IMPORTANT: Ability tasks must be stateless. All per-activation data
    /// flows through this packet, NOT as fields on the task itself.
    /// </summary>
    public class AbilityDataPacket : SequenceDataPacket
    {
        public readonly IEffectOrigin Spec;
        public readonly AbilitySystemComponent.AbilityActivationRequest Request;
        public bool UsageEffectsApplied { get; set; }

        /// <summary>
        /// Set by targeting tasks when targeting fails (e.g. invalid target selected with BreakRuntimeOnInvalid).
        /// Checked by AbilitySpecContainer to cancel the ability before execution proceeds.
        /// </summary>
        public bool TargetingFailed { get; set; }

        public AbilitySystemComponent System => Request.System;
        public AbilitySystemCallbacks Callbacks => System.Callbacks;

        /// <summary>Resolves the AbilitySpec (convenience cast from IEffectOrigin).</summary>
        public AbilitySpec AbilitySpec => Spec as AbilitySpec;

        private AbilityDataPacket(IEffectOrigin spec, AbilitySystemComponent.AbilityActivationRequest request)
        {
            Spec = spec;
            Request = request;

            AddPayload(
                Tags.SOURCE,
                spec.GetOwner()
            );
        }

        public static AbilityDataPacket GenerateFrom(AbilitySpecContainer container, AbilitySystemComponent asc)
        {
            var req = asc.CreateActivationRequest(container.Index);
            return GenerateFrom(container.Spec, req, false);
        }

        public static AbilityDataPacket GenerateFrom(IEffectOrigin spec, AbilitySystemComponent.AbilityActivationRequest req, bool useImplicitTargeting)
        {
            var data = new AbilityDataPacket(spec, req);

            if (useImplicitTargeting)
            {
                data.AddPayload(Tags.TARGET_REAL, spec.GetOwner());
            }

            return data;
        }

        #region Readable Definition

        public override string GetName()
        {
            return $"AbilityDataPacket.{Spec.GetReadableDefinition().GetName()}";
        }
        public override string GetDescription()
        {
            return $"Active data packet for an active ability runtime: {Spec.GetReadableDefinition().GetName()}: {Spec.GetReadableDefinition().GetDescription()}";
        }
        public override Texture2D GetPrimaryIcon()
        {
            return Spec.GetReadableDefinition().GetPrimaryIcon();
        }

        #endregion

        #region Common

        public bool TryGetTarget(EDataTarget policy, out ITarget target) => TryGet(Tags.TARGET_REAL, policy, out target);
        public bool TryGetFirstTarget(out ITarget target) => TryGetFirst(Tags.TARGET_REAL, out target);

        #endregion
    }
}
