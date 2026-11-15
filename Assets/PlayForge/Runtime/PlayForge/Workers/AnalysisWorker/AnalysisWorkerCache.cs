using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public class AnalysisWorkerCache : DeferredContextSystem
    {
        private List<AbstractAnalysisWorker> _workers;
        // How many sources give each worker: one stays until the last takes it back, and runs once (GAS-83)
        private readonly Dictionary<AbstractAnalysisWorker, int> _given = new();
        
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
            if (worker == null) return;
            // Given again, as by a second copy of an item, it's counted (GAS-83)
            _given.TryGetValue(worker, out int given);
            _given[worker] = given + 1;
            if (given == 0 && !_workers.Contains(worker)) _workers.Add(worker);
        }

        public void RemoveWorker(AbstractAnalysisWorker worker)
        {
            if (worker == null) return;
            // It stays while another source still gives it (GAS-83)
            if (_given.TryGetValue(worker, out int given) && given > 1)
            {
                _given[worker] = given - 1;
                return;
            }
            _given.Remove(worker);
            _workers.Remove(worker);
        }

        /// <summary>Read-only view of registered analysis workers (for debuggers / inspectors).</summary>
        public IReadOnlyList<AbstractAnalysisWorker> Workers => _workers;
    }
}
