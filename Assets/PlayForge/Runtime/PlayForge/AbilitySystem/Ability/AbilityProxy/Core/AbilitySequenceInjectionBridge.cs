/*
namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Translates IAbilityInjection calls into equivalent ISequenceInjection
    /// operations on the compiled ability sequence.
    ///
    /// The ability injection API (4-level: container/proxy/stage/task) remains
    /// the public-facing interface for ability users. This bridge performs the
    /// internal translation to sequence-level operations.
    /// </summary>
    public static class AbilitySequenceInjectionBridge
    {
        /// <summary>
        /// Translates an IAbilityInjection into the corresponding ISequenceInjection.
        /// Returns null if no direct sequence-level equivalent exists.
        /// </summary>
        public static ISequenceInjection Translate(IAbilityInjection abilityInjection)
        {
            return abilityInjection switch
            {
                InterruptInjection => InterruptSequenceInjection.Instance,
                SkipCurrentStageInjection => SkipStageInjection.Instance,
                SkipAndMaintainCurrentStageInjection => SkipAndMaintainInjection.Instance,
                StopMaintainLastInjection => StopMaintainedLastInjection.Instance,
                StopMaintainAllInjection => StopMaintainedAllInjection.Instance,
                _ => null
            };
        }

        /// <summary>
        /// Attempts to apply an ability injection by translating it to a sequence injection
        /// and applying it to the running sequence.
        /// </summary>
        /// <returns>True if the injection was translated and successfully applied.</returns>
        public static bool TryApply(IAbilityInjection abilityInjection, TaskSequence sequence)
        {
            if (sequence == null || !sequence.IsRunning) return false;

            var seqInjection = Translate(abilityInjection);
            if (seqInjection == null) return false;

            return sequence.Inject(seqInjection);
        }
    }
}
*/
