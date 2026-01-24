using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class AnalysisWorkerCache
    {
        private List<AbstractAnalysisWorker> _workers;

        private ActionQueue _actionQueue;
        private FrameSummary _frameSummary;

        public AnalysisWorkerCache()
        {
            _workers = new List<AbstractAnalysisWorker>();
        }

        public AnalysisWorkerCache(List<AbstractAnalysisWorker> workers) : this()
        {
            foreach (var worker in workers) ProvideWorker(worker);
        }
        
        public void SetDeferredContext(ActionQueue actionQueue, FrameSummary frameSummary)
        {
            _actionQueue = actionQueue;
            _frameSummary = frameSummary;
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
