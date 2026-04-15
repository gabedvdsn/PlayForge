using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class EffectWorkerGroup : AbstractEffectWorker
    {
        public List<AbstractEffectWorker> EffectWorkers;
        
        public override IEnumerable<IRootAction> OnEffectApplication(EffectWorkerContext ctx)
        {
            var actions = new List<IRootAction>();
            foreach (var eWorker in EffectWorkers) actions.AddRange(eWorker.OnEffectApplication(ctx));
            return actions;
        }
        public override IEnumerable<IRootAction> OnEffectTick(EffectWorkerContext ctx)
        {
            var actions = new List<IRootAction>();
            foreach (var eWorker in EffectWorkers) actions.AddRange(eWorker.OnEffectTick(ctx));
            return actions;
        }
        public override IEnumerable<IRootAction> OnEffectRemoval(EffectWorkerContext ctx)
        {
            var actions = new List<IRootAction>();
            foreach (var eWorker in EffectWorkers) actions.AddRange(eWorker.OnEffectRemoval(ctx));
            return actions;
        }
        public override IEnumerable<IRootAction> OnEffectImpact(EffectWorkerContext ctx)
        {
            var actions = new List<IRootAction>();
            foreach (var eWorker in EffectWorkers) actions.AddRange(eWorker.OnEffectImpact(ctx));
            return actions;
        }
    }
}
