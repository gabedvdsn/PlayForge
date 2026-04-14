namespace FarEmerald.PlayForge
{
    public enum EEffectStackPolicy
    {
        /// <summary>
        /// All stacks share the same duration and period
        /// Stacked effects acquire the existing duration and period
        /// Execute ticks = number of stacks
        /// </summary>
        StacksShareOneDuration,
        
        /// <summary>
        /// All stacks maintain their own duration and period
        /// Duration is taken from each stack
        /// </summary>
        StacksHaveIndependentDurations,
        
        /// <summary>
        /// Execute ticks = number of stacks
        /// Period durations are shared
        /// Duration is taken only from one effect stack at a time (FIFO)
        /// </summary>
        DurationTakenFromOneStack
    }
}
