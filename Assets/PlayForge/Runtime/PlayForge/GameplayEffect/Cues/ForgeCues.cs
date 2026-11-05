namespace FarEmerald.PlayForge
{
    /// <summary>The game's camera, which a camera shake cue asks to shake (FEAT-1).</summary>
    public interface ICameraShaker
    {
        /// <summary>Shakes the camera, as hard as the strength says, for the duration in seconds.</summary>
        void Shake(float strength, float duration);
    }

    /// <summary>What PlayForge's cues reach in the game: the camera shaker the game gives (FEAT-1).</summary>
    public static class ForgeCues
    {
        /// <summary>The shaker a camera shake cue asks, or null, which shakes nothing.</summary>
        public static ICameraShaker Shaker { get; set; }
    }
}
