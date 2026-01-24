using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Performs math operations on attribute changes relative to another attribute.
    /// This is an INLINE worker because it modifies the ChangeValue directly.
    /// </summary>
    public class MathAttributeWorker : AbstractRelativeAttributeWorker
    {
        [Header("Math Operation")]
        public ECalculationOperation Operation = ECalculationOperation.Multiply;
        public EEffectImpactTarget OperationTarget;
        public EMathApplicationPolicy OperationPolicy;
        
        public override EWorkerExecution Execution => EWorkerExecution.Inline;
        
        public override void Intercept(WorkerContext ctx)
        {
            var relative = GetRelative(ctx);
            var result = ForgeHelper.AttributeMathEvent(
                ctx.Change.Value.ToAttributeValue(),
                relative,
                Operation,
                OperationTarget,
                OperationPolicy);
            
            ctx.Change.Override(result);
        }
        
        // No deferred actions for inline workers
        public override IEnumerable<IRootAction> DeferredIntercept(WorkerContext ctx)
        {
            yield break;
        }
    }
}