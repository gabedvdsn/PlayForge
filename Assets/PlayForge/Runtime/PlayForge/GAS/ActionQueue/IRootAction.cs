namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Interface for deferred actions that execute at end-of-frame.
    /// Actions can be invalidated (e.g., effect removed) before execution.
    /// </summary>
    public interface IRootAction
    {
        /// <summary>
        /// Execution priority. Higher values execute first.
        /// Use ActionPriority constants for standard levels.
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// Check if action is still valid for execution.
        /// Invalid actions are skipped but still trigger OnActionInvalidated callback.
        /// </summary>
        bool IsValid { get; }
        
        /// <summary>
        /// Human-readable description for debugging and logging.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Execute the action. Only called if IsValid returns true.
        /// May queue additional actions during execution.
        /// </summary>
        /// <param name="system">The gameplay ability system context</param>
        void Execute(ISource system);
    }

    /// <summary>
    /// An action the system takes back once the queue has run it, to use again (GAS-P9). A listener handed one reads it
    /// during the call. One the queue invalidates isn't taken back: it stays with the frame summary that records it.
    /// </summary>
    internal interface IReusableAction
    {
        void Release();
    }
}
