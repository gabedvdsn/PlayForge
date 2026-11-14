using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Can't fall below a value (TW-15, Decision 53): a loss that would take its attribute below its Floor takes it to the
    /// floor, and no further, as Shallow Grave keeps its target at 1 Health while it lasts, in a buff's worker group.
    /// </summary>
    [Serializable]
    public class CantFallBelowAttributeWorker : AbstractFocusedAttributeWorker
    {
        [Tooltip("The least its attribute falls to, as 1")]
        public ScalableFloat Floor = 1f;

        public CantFallBelowAttributeWorker()
        {
            TimingConfig = EChangeEventTiming.PreChange;
            SignPolicy = ESignPolicyExtended.Negative;
            AllowSelfModification = true;
        }

        public override EWorkerExecution Execution => EWorkerExecution.Inline;

        public override void Intercept(WorkerContext ctx)
        {
            if (TargetAttribute == null || !ctx.AttributeCache.TryGetValue(TargetAttribute, out var cached)) return;
            var change = ctx.Change.Value;
            float current = cached.ActiveValue.CurrentValue;
            float floor = Floor?.Evaluate(NumberContext.Of(in ctx)) ?? 1f;
            if (change.CurrentValue >= 0f || current + change.CurrentValue >= floor) return;
            float allowed = Mathf.Min(0f, floor - current);
            ctx.Change.Value = new SourcedModifiedAttributeValue(change, allowed, change.BaseValue, change.ImpactIsWorkable);
        }
    }

    /// <summary>
    /// Damage return (TW-15, Decision 53): a share of each workable loss its attribute takes from another goes back to the
    /// one who dealt it, typed Returned, so no return returns it again and no lifesteal heals from it, as Blade Mail.
    /// </summary>
    [Serializable]
    public class ReturnDamageAttributeWorker : AbstractFocusedAttributeWorker
    {
        [Tooltip("The share of each loss it returns to its dealer, as 1 for all of it")]
        public ScalableFloat Share = 1f;

        public ReturnDamageAttributeWorker()
        {
            TimingConfig = EChangeEventTiming.PostChange;
            SignPolicy = ESignPolicyExtended.Negative;
        }

        public override EWorkerExecution Execution => EWorkerExecution.Deferred;

        // Returned damage isn't returned again
        public override bool ValidateWorkFor(WorkerContext ctx) =>
            base.ValidateWorkFor(ctx) && !ForgeLifesteal.Returned(ctx.Change.Value.Carrier);

        public override IEnumerable<IRootAction> DeferredIntercept(WorkerContext ctx)
        {
            var change = ctx.Change.Value;
            float loss = -change.CurrentValue;
            if (loss <= 0f || !change.ImpactIsWorkable || TargetAttribute == null) yield break;
            if (change.Carrier?.GetOrigin()?.GetSource() is not ITarget dealer || ReferenceEquals(dealer, ctx.System)) yield break;
            float amount = loss * Mathf.Max(0f, Share?.Evaluate(NumberContext.Of(ctx)) ?? 1f);
            if (amount <= 0f) yield break;

            var returner = ctx.System;
            var attribute = TargetAttribute;
            yield return new LambdaAction(_ =>
            {
                var carrier = IAttributeImpactCarrier.GenerateSourceDerivation(returner, attribute, Tags.IgnoreRetention,
                    new List<Tag> { Tags.RETURNED });
                dealer.TryModifyAttribute(attribute, new SourcedModifiedAttributeValue(carrier, -amount, 0f));
            }, $"Return {amount:0.##} {attribute.GetName()}");
        }
    }
}
