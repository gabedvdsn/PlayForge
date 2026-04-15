using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class AnalysisWorkerCache : DeferredContextSystem
    {
        private List<AbstractAnalysisWorker> _workers;
        
        public AnalysisWorkerCache()
        {
            _workers = new List<AbstractAnalysisWorker>();
        }

        public AnalysisWorkerCache(List<AbstractAnalysisWorker> workers) : this()
        {
            foreach (var worker in workers) ProvideWorker(worker);
        }

        public void Analyze(IGameplayAbilitySystem system)
        {
            foreach (var worker in _workers)
            {
                var actions = worker.Analyze(system, _frameSummary);
                _actionQueue.EnqueueRange(actions);
            }
        }

        public void ProvideWorker(AbstractAnalysisWorker worker)
        {
            if (worker != null && !_workers.Contains(worker))
                _workers.Add(worker);
        }
        
        public void RemoveWorker(AbstractAnalysisWorker worker)
        {
            if (worker == null) return;
            
            _workers.Remove(worker);
        }
    }
}
