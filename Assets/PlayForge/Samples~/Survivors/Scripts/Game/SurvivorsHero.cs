using System.Collections.Generic;
using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// The hero's controls: WASD or the arrows walk it, Space casts its dash the way it last walked, and each ability of
    /// the roster's pool it holds casts on its own as its cooldown ends, where an enemy stands within its reach. A cast hands
    /// the point ahead of where the hero last walked, which a volley with no target aims at, so a fan of knives goes the way
    /// the hero faces. It reads PlayForge's input, ForgeInput, so a test or a game's own input stands in.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivorsHero : MonoBehaviour
    {
        [Tooltip("The unit it walks and casts for; with none, the one it's on")]
        public LabUnit Unit;

        public KeyCode Up = KeyCode.W;
        public KeyCode Down = KeyCode.S;
        public KeyCode Left = KeyCode.A;
        public KeyCode Right = KeyCode.D;

        [Tooltip("The key that casts the roster's dash")]
        public KeyCode DashKey = KeyCode.Space;

        [Tooltip("How far an ability with no reach of its own casts at, from the hero")]
        [Min(1f)] public float DefaultReach = 14f;

        /// <summary>The way the hero last walked, which a volley with no target aims.</summary>
        public Vector3 Facing { get; private set; } = Vector3.forward;

        private readonly List<Ability> known = new();

        private void Awake()
        {
            if (!Unit) Unit = GetComponent<LabUnit>();
        }

        private void Update()
        {
            if (!Unit || !Unit.Alive || Time.deltaTime <= 0f) return;
            Walk();
            if (ForgeInput.Current.KeyPressed(DashKey)) Dash();
            CastWhatsReady();
        }

        private void Walk()
        {
            var input = ForgeInput.Current;
            var way = Vector3.zero;
            if (input.KeyHeld(Up) || input.KeyHeld(KeyCode.UpArrow)) way.z += 1f;
            if (input.KeyHeld(Down) || input.KeyHeld(KeyCode.DownArrow)) way.z -= 1f;
            if (input.KeyHeld(Left) || input.KeyHeld(KeyCode.LeftArrow)) way.x -= 1f;
            if (input.KeyHeld(Right) || input.KeyHeld(KeyCode.RightArrow)) way.x += 1f;
            if (way.sqrMagnitude < 0.01f)
            {
                if (Unit.Destination.HasValue) Unit.Stop();
                return;
            }
            Facing = way.normalized;
            Unit.MoveTo(Unit.transform.position + Facing * 0.75f);
        }

        // The roster's dash, the way the hero last walked, as its cooldown allows. The point handed is far ahead, as only its
        // way counts: the dash goes its own distance, no farther
        private void Dash()
        {
            var game = SurvivorsGame.Current;
            var dash = game && game.Roster ? game.Roster.Dash : null;
            if (dash && Unit.Ready(dash)) Unit.Cast(dash, Unit.DataAt(Unit.transform.position + Facing * 100f));
        }

        // Each ability of the pool ready, while an enemy stands within its reach: its targeting finds whom it's for. The dash
        // and anything else outside the pool waits for its key
        private void CastWhatsReady()
        {
            var game = SurvivorsGame.Current;
            float nearest = game ? game.Horde.NearestDistance : 0f;
            known.Clear();
            known.AddRange(Unit.Known());
            var pool = game && game.Roster ? game.Roster.AbilityPool : null;
            for (int i = 0; i < known.Count; i++)
            {
                var ability = known[i];
                if (pool != null && !pool.Contains(ability)) continue;
                if (!Unit.Ready(ability)) continue;
                float reach = Unit.Reach(ability);
                if (nearest > (reach > 0f ? reach : DefaultReach)) continue;
                Unit.Cast(ability, Unit.DataAt(Unit.transform.position + Facing * 3f));
            }
        }
    }
}
