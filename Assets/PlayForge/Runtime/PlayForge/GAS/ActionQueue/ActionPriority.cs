namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Standard priority levels for IRootActions.
    /// Higher values execute first.
    /// </summary>
    public static class ActionPriority
    {
        /// <summary>Emergency stops, interrupts - must run first</summary>
        public const int Critical = 100;
        
        /// <summary>Important reactions that should happen early</summary>
        public const int High = 50;
        
        /// <summary>Default priority for most actions</summary>
        public const int Normal = 0;
        
        /// <summary>Lower priority actions that can wait</summary>
        public const int Low = -50;
        
        /// <summary>Tag worker activation/resolution</summary>
        public const int TagWorker = -100;
        
        /// <summary>Analysis workers and final cleanup</summary>
        public const int Analysis = -200;
        
        /// <summary>Very last - cleanup operations</summary>
        public const int Cleanup = -300;
    }
}
