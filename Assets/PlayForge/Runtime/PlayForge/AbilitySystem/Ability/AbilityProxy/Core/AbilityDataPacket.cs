using System;
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
        public readonly IEffectOrigin EffectOrigin;
        public readonly AbilitySystemComponent.AbilityActivationRequest Request;
        public bool UsageEffectsApplied { get; set; }

        /// <summary>
        /// Set by targeting tasks when targeting fails (e.g. invalid target selected with BreakRuntimeOnInvalid).
        /// Checked by AbilitySpecContainer to cancel the ability before execution proceeds.
        /// </summary>
        public bool TargetingFailed { get; set; }

        /// <summary>
        /// Invoked by ability tasks that host sub-sequences (e.g. RunSequenceTask) when a nested
        /// critical section exits. Allows the activation handle to release its claim early, while
        /// the hosting ability runtime stays alive and fully tracked by ProcessControl until its
        /// sub-sequence completes naturally. Wired by AbilitySpecContainer when activating a handle.
        /// Invocation is expected to be idempotent on the consumer side.
        /// </summary>
        public Action NotifyCriticalSectionExit { get; set; }

        public AbilitySystemComponent System => Request.System;
        public AbilitySystemCallbacks Callbacks => System.Callbacks;

        /// <summary>Resolves the AbilitySpec (convenience cast from IEffectOrigin).</summary>
        public AbilitySpec AbilitySpec => EffectOrigin as AbilitySpec;

        private AbilityDataPacket(IEffectOrigin effectOrigin, AbilitySystemComponent.AbilityActivationRequest request)
        {
            EffectOrigin = effectOrigin;
            Request = request;

            if (request.Data is not null)
            {
                foreach (var d in request.Data.Payload)
                {
                    _payload[d.Key] = d.Value;
                }
            }
            
            AddPayload(
                Tags.SOURCE,
                effectOrigin.GetOwner()
            );
        }

        public static AbilityDataPacket GenerateFrom(AbilitySpecContainer container, AbilitySystemComponent asc)
        {
            var req = asc.CreateActivationRequest(container.Index);
            var data = GenerateFrom(container.Spec, req, false, container.Spec.Base.Behaviour.ImplicitTag);
            return data;
        }

        public static AbilityDataPacket GenerateFrom(IEffectOrigin spec, AbilitySystemComponent.AbilityActivationRequest req, bool useImplicitTargeting, Tag implicitTargetingTag)
        {
            var data = new AbilityDataPacket(spec, req);

            if (useImplicitTargeting)
            {
                data.SetPrimary(implicitTargetingTag, spec.GetOwner().GetTargetingPacket());
            }

            data.AppendPath($"GAS[{spec.GetOwner().GetName()}]");
            data.AppendPath($"Ability[{spec.GetReadableDefinition().GetName()}]");
            return data;
        }

        #region Readable Definition

        public override string GetName()
        {
            return $"AbilityDataPacket.{EffectOrigin.GetReadableDefinition().GetName()}";
        }
        public override string GetDescription()
        {
            return $"Active data packet for an active ability runtime: {EffectOrigin.GetReadableDefinition().GetName()}: {EffectOrigin.GetReadableDefinition().GetDescription()}";
        }
        public override Texture2D GetDefaultIcon()
        {
            return EffectOrigin.GetReadableDefinition().GetDefaultIcon();
        }

        #endregion

        #region Common

        public bool TryGetTarget(EDataTarget policy, out ITarget target) => TryGet(Tags.TARGET_REAL, policy, out target);
        public bool TryGetFirstTarget(out ITarget target) => TryGetFirst(Tags.TARGET_REAL, out target);

        #endregion
    }
}
