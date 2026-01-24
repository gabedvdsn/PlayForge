using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Clamps an attribute's current value with respect to its overflow policy.
    /// This is an INLINE worker because it directly modifies the attribute cache.
    /// </summary>
    public class ClampAttributeWorker : AbstractFocusedAttributeWorker
    {
        public override EWorkerExecution Execution => EWorkerExecution.Inline;
        
        // Default to post-change so clamping happens after modifications
        public ClampAttributeWorker()
        {
            TimingConfig = EChangeEventTiming.PostChange;
            AnyImpactType = true;
            AnyContextTag = true;
        }
        
        public override void Intercept(WorkerContext ctx)
        {
            var cache = ctx.GetMutableCache();
            var cachedValue = cache[TargetAttribute];
            var clampValue = cachedValue.Value;
            var baseAligned = clampValue.BaseAligned();
            
            switch (cachedValue.Blueprint.Overflow.Policy)
            {
                case EAttributeOverflowPolicy.ZeroToBase:
                    if (AttributeValue.WithinLimits(clampValue, default, baseAligned)) return;
                    cachedValue.Clamp(baseAligned);
                    break;
                    
                case EAttributeOverflowPolicy.FloorToBase:
                    if (AttributeValue.WithinLimits(clampValue, cachedValue.Blueprint.Overflow.Floor, baseAligned)) return;
                    cachedValue.Clamp(cachedValue.Blueprint.Overflow.Floor, baseAligned);
                    break;
                    
                case EAttributeOverflowPolicy.ZeroToCeil:
                    if (AttributeValue.WithinLimits(clampValue, default, cachedValue.Blueprint.Overflow.Ceil)) return;
                    cachedValue.Clamp(cachedValue.Blueprint.Overflow.Ceil);
                    break;
                    
                case EAttributeOverflowPolicy.FloorToCeil:
                    if (AttributeValue.WithinLimits(clampValue, cachedValue.Blueprint.Overflow.Floor, cachedValue.Blueprint.Overflow.Ceil)) return;
                    cachedValue.Clamp(cachedValue.Blueprint.Overflow.Floor, cachedValue.Blueprint.Overflow.Ceil);
                    break;
                    
                case EAttributeOverflowPolicy.Unlimited:
                    // No clamping needed
                    break;
                    
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        // No deferred actions for inline workers
        public override IEnumerable<IRootAction> DeferredIntercept(WorkerContext ctx)
        {
            yield break;
        }
    }
}