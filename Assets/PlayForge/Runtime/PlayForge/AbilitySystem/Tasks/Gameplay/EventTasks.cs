using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Whom a Send Event task sends to: its caster, or each of its activation's targets (FN-24).</summary>
    public enum EEventRecipient
    {
        Caster,
        Targets,
    }

    /// <summary>
    /// Sends a gameplay event to its caster, or to each of its activation's targets, as a combo's hit tells its next stage it
    /// connected, or as an ability sets off another's trigger (FN-24). The event's data holds its caster as Source and the
    /// one it's sent to as Event Subject, with the activation's caller's magnitudes; it's sent at once, and the abilities it
    /// triggers try to activate as a trigger's do.
    /// </summary>
    [Serializable]
    [ForgeCategory("Events and Tags")]
    public class SendEventTask : AbstractAbilityTask
    {
        [Tooltip("The event it sends, by its tag")]
        [ForgeTagContext(ForgeContext.Event)]
        public Tag Event;

        [Tooltip("Whom it sends to: its caster, or each of its activation's targets")]
        public EEventRecipient To = EEventRecipient.Caster;

        public override string Description => Event.IsValid ? $"Send {Event}" : null;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (!Event.IsValid) return UniTask.CompletedTask;
            var caster = data.EffectOrigin?.GetSource();
            if (To == EEventRecipient.Caster)
            {
                Send(caster?.ToGAS(), caster as ITarget, data);
                return UniTask.CompletedTask;
            }
            int targets = data.Count(Tags.TARGET_REAL);
            for (int i = 0; i < targets; i++)
                if (data.TryGetAt(Tags.TARGET_REAL, i, out ITarget target)) Send(target as IGameplayAbilitySystem, caster as ITarget, data);
            return UniTask.CompletedTask;
        }

        private void Send(IGameplayAbilitySystem to, ITarget source, AbilityDataPacket data)
        {
            if (to == null) return;
            var packet = ProcessDataPacket.Default();
            if (source != null) packet.SetPrimary(Tags.SOURCE, source);
            if (to is ITarget subject) packet.SetPrimary(Tags.EVENT_SUBJECT, subject);
            if (data.SetByCallerMagnitudes != null)
                foreach (var magnitude in data.SetByCallerMagnitudes) packet.SetByCaller(magnitude.Key, magnitude.Value);
            to.SendGameplayEvent(Event, packet);
        }
    }

    /// <summary>
    /// Grants tags to its caster or its target while its stage runs, as a charge's State.Charging: on as it starts, off as
    /// the stage ends, however it ends (FN-24). Each is a loose tag, counted as a grant is, so two runs at once each give
    /// and take back their own.
    /// </summary>
    [Serializable]
    [ForgeCategory("Events and Tags")]
    public class GrantTagsTask : AbstractAbilityTask
    {
        [Tooltip("The tags it grants while its stage runs")]
        [ForgeTagContext(ForgeContext.Granted)]
        public List<Tag> Granted = new();

        [Tooltip("Whom it grants them: its caster, or its activation's target")]
        public NumberSide On = NumberSide.Caster;

        public override string Description => Granted is { Count: > 0 } ? $"Grant {string.Join(", ", Granted)}" : null;

        public override bool IsCriticalSection => false;

        // The entity a run granted its tags to, to take them back from as its stage ends
        private sealed class Grant
        {
            public IGameplayAbilitySystem To;
        }

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var grant = data.GetTaskState<Grant>(this);
            if (grant.To != null || Granted == null) return UniTask.CompletedTask;
            grant.To = EntityOf(data, On);
            if (grant.To == null) return UniTask.CompletedTask;
            for (int i = 0; i < Granted.Count; i++)
                if (Granted[i].IsValid) grant.To.AddLooseTag(Granted[i]);
            return UniTask.CompletedTask;
        }

        public override void Clean(AbilityDataPacket data)
        {
            var grant = data.GetTaskState<Grant>(this);
            if (grant.To == null || Granted == null) return;
            for (int i = 0; i < Granted.Count; i++)
                if (Granted[i].IsValid) grant.To.RemoveLooseTag(Granted[i]);
            grant.To = null;
        }
    }

    /// <summary>
    /// Cancels its caster's running abilities by tag, as a dodge cancels a cast: each running ability tagged with one of
    /// its tags, or a child of one, by its asset tag or a context tag, is interrupted, but a run in a critical stage, which
    /// is committed (Decision 30), and never the ability it runs in (FN-24).
    /// </summary>
    [Serializable]
    [ForgeCategory("Events and Tags")]
    public class CancelAbilitiesTask : AbstractAbilityTask
    {
        [Tooltip("The abilities it cancels, by their asset tag or a context tag, or a child of one")]
        [ForgeTagContext(ForgeContext.AssetTag, ForgeContext.ContextTag)]
        public List<Tag> Cancels = new();

        public override string Description => Cancels is { Count: > 0 } ? $"Cancel {string.Join(", ", Cancels)}" : null;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var abilities = data.EffectOrigin?.GetSource()?.ToGAS()?.GetAbilitySystem();
            if (abilities == null || Cancels == null || Cancels.Count == 0) return UniTask.CompletedTask;
            var own = data.EffectOrigin as AbilitySpec;
            foreach (var container in abilities.GetAbilityContainers())
            {
                if (container?.Spec == null || !container.IsActive || ReferenceEquals(container.Spec, own)) continue;
                if (AbilityTags.Names(Cancels, container.Spec.Base)) container.Cancel();
            }
            return UniTask.CompletedTask;
        }
    }
}
