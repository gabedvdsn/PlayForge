using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Several impact workers as one: given to an entity, it gives it each of its workers, and each runs for the
    /// changes it takes as it would on its own; taken back, it takes them back.
    /// </summary>
    [ForgeMenu(Description = "Several impact workers as one.")]
    public class ImpactWorkerGroup : AbstractImpactWorker
    {
        public List<AbstractImpactWorker> Workers;
        
        public override void ActivateInline(ImpactData impactData)
        {
            foreach (var worker in Workers.Where(worker => worker.PreValidateWorkFor(impactData)).Where(worker => worker.ValidateWorkFor(impactData)))
            {
                worker.ActivateInline(impactData);
            }
        }

        public override bool PreValidateWorkFor(ImpactData impactData)
        {
            return Workers.Count > 0;
        }

        public override bool ValidateWorkFor(ImpactData impactData)
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
