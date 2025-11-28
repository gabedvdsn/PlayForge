using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ImpactWorkerGroup : AbstractImpactWorker
    {
        public List<AbstractImpactWorker> Workers;
        
        public override void Activate(AbilityImpactData impactData)
        {
            foreach (var worker in Workers.Where(worker => worker.PreValidateWorkFor(impactData)).Where(worker => worker.ValidateWorkFor(impactData)))
            {
                worker.Activate(impactData);
            }
        }

        public override bool PreValidateWorkFor(AbilityImpactData impactData)
        {
            return Workers.Count > 0;
        }

        public override bool ValidateWorkFor(AbilityImpactData impactData)
        {
            return true;
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
