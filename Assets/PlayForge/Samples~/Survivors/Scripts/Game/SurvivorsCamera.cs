using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>A camera that looks down at the hero from behind and above, following it smoothly.</summary>
    [RequireComponent(typeof(Camera))]
    public class SurvivorsCamera : MonoBehaviour
    {
        [Tooltip("How far back and up it stands from the hero")]
        public Vector3 Offset = new(0f, 21f, -13f);

        [Tooltip("How quickly it catches up, higher sooner")]
        [Min(0.1f)] public float Follow = 6f;

        private void LateUpdate()
        {
            var game = SurvivorsGame.Current;
            if (!game || !game.Hero) return;
            var heroAt = game.Hero.transform.position;
            var wanted = heroAt + Offset;
            transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-Follow * Time.unscaledDeltaTime));
            // It looks along its offset, so it turns not at all as it follows
            transform.rotation = Quaternion.LookRotation(heroAt + Vector3.up * 0.5f - wanted);
        }
    }
}
