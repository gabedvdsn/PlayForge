using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// An impact worker on the hitter's side: each blow it deals that passes the worker's filters applies an effect to
    /// what it hit, from the hitter. Ember Brand's attacks set their target burning this way.
    /// </summary>
    [Serializable]
    public sealed class ApplyEffectOnHitWorker : AbstractContextImpactWorker
    {
        [Tooltip("The effect a blow applies to what it hit")]
        public GameplayEffect Effect;

        public override EWorkerExecution Execution => EWorkerExecution.Deferred;

        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            var hit = ctx.ImpactData.Target;
            var hitter = ctx.ImpactData.SourcedModifier.Source;
            if (Effect == null || hit == null || hitter == null || hit.IsDead) return Array.Empty<IRootAction>();
            return new IRootAction[] { new ApplyEffectAction(hit, Effect, IEffectOrigin.GenerateSourceDerivation(hitter)) };
        }
    }

    /// <summary>
    /// An impact worker on the hitter's side: a share of each blow it deals that passes the worker's filters comes back to
    /// it, to its Work Attribute. Drain Life's own worker group heals the Oracle by half of what the drain deals this way,
    /// filtered to the drain's context tag, so its other blows heal nothing. Lifesteal reads a share from an attribute;
    /// this one is the ability's own.
    /// </summary>
    [Serializable]
    public sealed class ShareBackWorker : AbstractContextImpactWorker
    {
        [Tooltip("The share of each blow that comes back"), Range(0f, 1f)]
        public float Share = 0.5f;

        public override EWorkerExecution Execution => EWorkerExecution.Deferred;

        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            var hitter = ctx.ImpactData.SourcedModifier.Source;
            float back = Mathf.Abs(ctx.ImpactData.RealImpact.CurrentValue) * Share;
            if (hitter == null || hitter.IsDead || WorkAttribute == null || back <= 0f) return Array.Empty<IRootAction>();
            return new IRootAction[]
            {
                new ModifyAttributeAction(hitter, WorkAttribute, SourcedModifiedAttributeValue.GenerateSimple(hitter, WorkAttribute, back, 0f)),
            };
        }
    }

    /// <summary>
    /// A tag worker: while the entity's tags meet the worker's requirements, it has the worker's effect, which goes as
    /// they stop meeting them. The Shade regenerates while it's invisible this way.
    /// </summary>
    [Serializable]
    public sealed class WhileTaggedEffectWorker : AbstractTagWorker
    {
        [Tooltip("The effect the entity has while its tags meet the requirements")]
        public GameplayEffect Effect;

        public override void Activate(IGameplayAbilitySystem system)
        {
            if (Effect == null || system == null) return;
            system.ApplyGameplayEffect(system.GenerateEffectSpec(IEffectOrigin.GenerateSourceDerivation(system), Effect));
        }

        public override void Resolve(IGameplayAbilitySystem system)
        {
            if (Effect != null) system?.RemoveGameplayEffect(Effect);
        }
    }

    /// <summary>
    /// An effect worker: a blow of its effect that leaves its target below a share of its most Health finishes it, its
    /// Health to 0, and the death watcher does the rest. Eviscerate executes this way.
    /// </summary>
    [Serializable]
    public sealed class ExecuteEffectWorker : AbstractEffectWorker
    {
        [Tooltip("The attribute a blow leaves low")]
        public Attribute Health;

        [Tooltip("The share of its most Health below which a target is finished"), Range(0f, 1f)]
        public float Below = 0.1f;

        public override IEnumerable<IRootAction> OnEffectImpact(EffectWorkerContext ctx)
        {
            var target = ctx.Carrier?.GetTarget() ?? ctx.System;
            var finisher = ctx.Carrier?.GetOrigin()?.GetSource();
            if (Health == null || target == null || finisher == null || target.IsDead) yield break;
            if (!target.TryGetAttributeValue(Health, out var health) || health.BaseValue <= 0f) yield break;
            if (health.CurrentValue <= 0f || health.CurrentValue / health.BaseValue > Below) yield break;

            yield return new ModifyAttributeAction(target, Health,
                SourcedModifiedAttributeValue.GenerateSimple(finisher, Health, -health.CurrentValue, 0f));
        }
    }
}
