namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Defines how a worker executes during the modification pipeline.
    /// </summary>
    public enum EWorkerExecution
    {
        /// <summary>
        /// Runs synchronously during the modification pipeline.
        /// Can modify ChangeValue directly. MUST NOT call system methods
        /// that trigger other workers (e.g., ModifyAttribute).
        /// </summary>
        Inline,
        
        /// <summary>
        /// Returns IRootAction(s) to queue for end-of-frame execution.
        /// Cannot modify ChangeValue. Safe to call any system method
        /// since execution is deferred.
        /// </summary>
        Deferred
    }
}
