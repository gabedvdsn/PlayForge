namespace FarEmerald.PlayForge
{
    /// <summary>What came of a request to activate an ability.</summary>
    public enum EActivationStatus
    {
        /// <summary>
        /// It was refused. <see cref="ActivationResult.Refusal"/> says why, as
        /// <see cref="AbilitySystemCallbacks.OnAbilityRefused"/> tells it.
        /// </summary>
        Refused,

        /// <summary>
        /// It started: its claim, its targeting and its first stage ran inside the call until one of its tasks waited, so
        /// an ability whose tasks all finish at once has ended by now.
        /// </summary>
        Started,

        /// <summary>
        /// It waits in the input buffer, as its Queue policy lets it, until the claim or the block that holds it lifts. It
        /// starts then, or is let go as its window runs out.
        /// </summary>
        Buffered,

        /// <summary>
        /// It was asked for while its own activation call still ran, from its own callbacks or from another ability's that
        /// it started, so it starts in the next frame instead. If it's refused then, OnAbilityRefused tells it.
        /// </summary>
        NextFrame,
    }

    /// <summary>
    /// What came of a request to activate an ability: it started, waits in the input buffer, starts in the next frame, or
    /// was refused, and why. It reads as true where it wasn't refused.
    /// </summary>
    public readonly struct ActivationResult
    {
        public readonly EActivationStatus Status;

        /// <summary>
        /// Why it was refused, as <see cref="AbilitySystemCallbacks.OnAbilityRefused"/> tells it. Its Reason is None where
        /// it wasn't refused.
        /// </summary>
        public readonly ActivationRefusal Refusal;

        public ActivationResult(EActivationStatus status, ActivationRefusal refusal)
        {
            Status = status;
            Refusal = refusal;
        }

        /// <summary>Whether it started inside the call.</summary>
        public bool Started => Status == EActivationStatus.Started;

        /// <summary>Whether it waits in the input buffer.</summary>
        public bool Buffered => Status == EActivationStatus.Buffered;

        /// <summary>Whether it was refused.</summary>
        public bool Refused => Status == EActivationStatus.Refused;

        /// <summary>Whether it wasn't refused: it started, waits in the input buffer, or starts in the next frame.</summary>
        public static implicit operator bool(ActivationResult result) => !result.Refused;

        public override string ToString() => Refused ? $"Refused: {Refusal}" : Status.ToString();
    }
}
