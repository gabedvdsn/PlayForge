namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Deferred action to activate a tag worker.
    /// </summary>
    public class TagWorkerActivateAction : IRootAction
    {
        public int Priority => ActionPriority.TagWorker;
        public bool IsValid => _worker != null && _system != null;
        
        public string Description => $"TagWorker.Activate({_worker?.GetType().Name ?? "null"})";
        
        private readonly AbstractTagWorker _worker;
        private readonly IGameplayAbilitySystem _system;
        private readonly FrameSummary _frameSummary;
        
        public TagWorkerActivateAction(
            AbstractTagWorker worker, 
            IGameplayAbilitySystem system, 
            FrameSummary frameSummary)
        {
            _worker = worker;
            _system = system;
            _frameSummary = frameSummary;
        }
        
        public void Execute(IGameplayAbilitySystem system)
        {
            // Mark as active in cache
            _system.GetTagCache()?.MarkWorkerActive(_worker);
            
            // Call worker's activate
            _worker.Activate(_system);
            
            // Record in frame summary
            _frameSummary?.RecordTagWorkerActivated(_worker);
        }
    }
    
    /// <summary>
    /// Deferred action to resolve (deactivate) a tag worker.
    /// </summary>
    public class TagWorkerResolveAction : IRootAction
    {
        public int Priority => ActionPriority.TagWorker;
        public bool IsValid => _worker != null && _system != null;
        
        public string Description => $"TagWorker.Resolve({_worker?.GetType().Name ?? "null"})";
        
        private readonly AbstractTagWorker _worker;
        private readonly IGameplayAbilitySystem _system;
        private readonly FrameSummary _frameSummary;
        
        public TagWorkerResolveAction(
            AbstractTagWorker worker,
            IGameplayAbilitySystem system,
            FrameSummary frameSummary)
        {
            _worker = worker;
            _system = system;
            _frameSummary = frameSummary;
        }
        
        public void Execute(IGameplayAbilitySystem system)
        {
            // Call worker's resolve
            _worker.Resolve(_system);
            
            // Mark as inactive in cache
            _system.GetTagCache()?.MarkWorkerInactive(_worker);
            
            // Record in frame summary
            _frameSummary?.RecordTagWorkerResolved(_worker);
        }
    }
}