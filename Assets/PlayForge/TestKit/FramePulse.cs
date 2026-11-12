using UnityEngine;

namespace FarEmerald.PlayForge.Testing
{
    /// <summary>Counts the phases Unity ran, so <see cref="FrameStepper"/> can show a frame was a whole one (TW-26).</summary>
    [DefaultExecutionOrder(int.MinValue)]
    [AddComponentMenu("")]
    public sealed class FramePulse : MonoBehaviour
    {
        public int Updates { get; private set; }
        public int FixedUpdates { get; private set; }
        public int LateUpdates { get; private set; }

        private void Update() => Updates++;
        private void FixedUpdate() => FixedUpdates++;
        private void LateUpdate() => LateUpdates++;
    }
}
