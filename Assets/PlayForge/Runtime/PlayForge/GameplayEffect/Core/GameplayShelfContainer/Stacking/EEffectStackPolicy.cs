namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How the stacks of an effect whose Re-application is Stack Existing keep time: Stacks Share One Duration, the
    /// default, runs one timer for all, which a stack added doesn't reset, each tick executing once for each stack;
    /// Stacks Have Independent Durations gives each stack its own timer and ticks, so they drop off one at a time;
    /// Duration Taken From One Stack runs one stack's timer at a time, a stack lost and the timer restarted each time
    /// it runs out.
    /// </summary>
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
