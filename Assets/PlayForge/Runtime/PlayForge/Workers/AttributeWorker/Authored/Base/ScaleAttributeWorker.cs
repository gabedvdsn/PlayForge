using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Scales an attribute's current value based on modifications to its base value.
    /// This is a DEFERRED worker because it triggers a new ModifyAttribute call.
    /// </summary>
    public class  ScaleAttributeWorker : AbstractFocusedAttributeWorker
    {
        public override EWorkerExecution Execution => EWorkerExecution.Deferred;
        
        public override bool PreValidateWorkFor(ChangeValue change)
        {
            // Only process if there's a base value change
            return change.Value.BaseValue != 0 && base.PreValidateWorkFor(change);
        }
        
        public override bool ValidateWorkFor(WorkerContext ctx)
        {
            // Additional check: ensure we won't divide by zero
            if (!ctx.AttributeCache.TryGetValue(TargetAttribute, out var cached))
                return false;
            
            if (cached.Value.BaseValue == 0)
                return false;
            
            return base.ValidateWorkFor(ctx);
        }
        
        public override IEnumerable<IRootAction> DeferredIntercept(WorkerContext ctx)
        {
            var cached = ctx.AttributeCache[TargetAttribute];
            if (cached.Value.BaseValue == 0) yield break;
            
            float proportion = ctx.Change.Value.BaseValue / cached.Value.BaseValue;
            float delta = proportion * cached.Value.CurrentValue;
            
            var derivation = IAttributeImpactDerivation.GenerateSourceDerivation(
                ctx.Change.Value, Tags.NONE, Tags.IGNORE);
            var scaleAmount = new SourcedModifiedAttributeValue(derivation, delta, 0f, false);
            
            yield return new ModifyAttributeAction(
                ctx.System, 
                TargetAttribute, 
                scaleAmount, 
                runEvents: false);
        }
        
        // Deferred worker - do nothing inline
        public override void Intercept(WorkerContext ctx) { }
    }
}