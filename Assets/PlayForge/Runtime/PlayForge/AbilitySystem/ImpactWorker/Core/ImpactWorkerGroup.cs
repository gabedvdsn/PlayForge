using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ImpactWorkerGroup : AbstractImpactWorker
    {
        public List<AbstractImpactWorker> Workers;
        
        public override void InterpretImpact(AbilityImpactData impactData)
        {
            foreach (AbstractImpactWorker worker in Workers)
            {
                if (!worker.ValidateWorkFor(impactData)) continue;
                worker.InterpretImpact(impactData);
            }
        }
        
        public override bool ValidateWorkFor(AbilityImpactData impactData)
        {
            return Workers.Any(worker => worker.ValidateWorkFor(impactData));
        }
        public override Attribute GetTargetedAttribute()
        {
            return default;
        }
        public override void SubscribeToCache(ImpactWorkerCache cache)
        {
            foreach (var worker in Workers) worker.SubscribeToCache(cache);
        }

        public override void UnsubscribeFromCache(ImpactWorkerCache cache)
        {
            foreach (var worker in Workers) worker.UnsubscribeFromCache(cache);
        }
    }
}
