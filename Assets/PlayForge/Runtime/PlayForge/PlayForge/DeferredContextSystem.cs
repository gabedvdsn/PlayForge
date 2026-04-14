namespace FarEmerald.PlayForge
{
    public abstract class DeferredContextSystem
    {
        protected ActionQueue _actionQueue;
        protected FrameSummary _frameSummary;
        
        public virtual void SetDeferredContext(IGameplayAbilitySystem self, ActionQueue actionQueue, FrameSummary frameSummary)
        {
            _actionQueue = actionQueue;
            _frameSummary = frameSummary;
        }
    }
}
