namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of the subsystems of an entity that queue work for its end of frame, its attribute, ability and item
    /// systems and its analysis worker cache: it holds the entity's action queue and frame summary, which the GAS hands
    /// it through <c>SetDeferredContext</c> as it initializes.
    /// </summary>
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
