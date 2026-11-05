using System;
using System.Collections.Generic;
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
        /// Set once the targeting task gets to its end. Targeting cut short, by a throw or by anything ending its
        /// sequence, has no target to give, and the ability goes no further (TS-V).
        /// </summary>
        public bool TargetingFinished { get; internal set; }

        /// <summary>
        /// Invoked by ability tasks that host sub-sequences (e.g. RunSequenceTask) when a nested
        /// critical section exits. Allows the activation handle to release its claim early, while
        /// the hosting ability runtime stays alive and fully tracked by ProcessControl until its
        /// sub-sequence completes naturally. Wired by AbilitySpecContainer when activating a handle.
        /// Invocation is expected to be idempotent on the consumer side.
        /// </summary>
        public Action NotifyCriticalSectionExit { get; set; }

        // What tasks keep for this activation, by task
        private Dictionary<AbstractAbilityTask, object> _taskStates;

        /// <summary>
        /// The state a task keeps for this activation, made the first time it's asked for. A task is one object
        /// shared by every activation of its ability, so whatever it keeps for an activation belongs here (TS-U).
        /// </summary>
        public T GetTaskState<T>(AbstractAbilityTask task) where T : class, new()
        {
            _taskStates ??= new Dictionary<AbstractAbilityTask, object>();
            if (_taskStates.TryGetValue(task, out var state)) return (T)state;

            var created = new T();
            _taskStates[task] = created;
            return created;
        }

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
                // Lists of its own: these were the request's, so writing to one packet wrote to both (TS-X)
                foreach (var d in request.Data.Payload)
                {
                    _payload[d.Key] = new List<object>(d.Value);
                }

                // And the magnitudes the caller set, for the effects the ability applies (FEAT-2)
                TakeSetByCaller(request.Data.SetByCallerMagnitudes);
            }
            
            AddPayload(
                Tags.SOURCE,
                effectOrigin.GetSource()
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
                data.SetPrimary(implicitTargetingTag, spec.GetSource().GetTargetingPacket());
            }

            data.AppendPath($"GAS[{spec.GetSource().GetName()}]");
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

        /// <summary>
        /// Files the entity each targeting packet points at under Target Real, in the packets' order, where tasks, rules
        /// and spawned processes look for it, unless a target is there already. Targeting leaves only the packets, under
        /// Target or the implicit tag, and nothing filed the entity, so Apply Effects never found its target (GAS-46).
        /// Then only the first was filed, so an ability that targeted several reached one of them (GAS-62).
        /// </summary>
        public void ResolveTarget(Tag implicitTag)
        {
            if (TryGetFirst(Tags.TARGET_REAL, out ITarget _)) return;

            var key = TryGetFirst(Tags.TARGET, out AbstractTargetingPacket _) ? Tags.TARGET : implicitTag;
            int count = Count(key);
            for (int i = 0; i < count; i++)
            {
                if (!TryGetAt(key, i, out AbstractTargetingPacket packet) || packet.target is null) continue;
                if (!Contains(packet.target, Tags.TARGET_REAL)) AddPayload(Tags.TARGET_REAL, packet.target);
            }
        }

        #endregion
    }
}
