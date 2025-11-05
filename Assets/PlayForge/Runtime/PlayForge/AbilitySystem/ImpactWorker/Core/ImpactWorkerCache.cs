using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class ImpactWorkerCache
    {
        private Dictionary<Attribute, List<AbstractImpactWorker>> Cache;

        public ImpactWorkerCache()
        {
            Cache = new Dictionary<Attribute, List<AbstractImpactWorker>>();
        }

        public ImpactWorkerCache(List<AbstractImpactWorker> workers)
        {
            Cache = new Dictionary<Attribute, List<AbstractImpactWorker>>();
            foreach (var worker in workers) worker.SubscribeToCache(this);
        }

        public void AddWorker(AbstractImpactWorker worker)
        {
            worker.SubscribeToCache(this);
        }

        public void RemoveWorker(AbstractImpactWorker worker)
        {
            worker.UnsubscribeFromCache(this);
        }

        public void AddWorker(Attribute attribute, AbstractImpactWorker worker)
        {
            Cache.SafeAdd(attribute, worker);
        }

        public void RemoveWorker(Attribute attribute, AbstractImpactWorker worker)
        {
            if (!Cache.ContainsKey(attribute)) return;
            Cache[attribute].Remove(worker);
        }
        
        public void RunImpactData(AbilityImpactData impactData)
        {
            if (!Cache.ContainsKey(impactData.Attribute)) return;
            foreach (var worker in Cache[impactData.Attribute])
            {
                if (!impactData.SourcedModifier.Workable && !worker.AcceptUnworkableImpact) continue;
                if (!worker.ValidateWorkFor(impactData)) continue;
                worker.InterpretImpact(impactData);
            }
        }
    }
}
