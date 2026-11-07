using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The AI hero: it fights the player's hero with the same ability assets the player casts. It casts its abilities in
    /// the order its hero's Local Data gives, each when the ability's own Local Data says (in reach, hurt, or low), handing
    /// each its target in the activation's data, which the abilities' click targeting takes with no click. Between casts
    /// it attacks, and backs off a moment when it's low.
    /// </summary>
    public class DemoAI : MonoBehaviour
    {
        [Tooltip("Seconds between its decisions")]
        public float ThinkEvery = 0.25f;

        [Tooltip("The share of its health below which it backs off a moment")]
        public float BackOffBelow = 0.2f;

        private DemoUnit unit;
        private float nextThought;
        private float backingOffUntil;
        private float backedOffAt = -99f;

        private void Awake() => unit = GetComponent<DemoUnit>();

        private void Update()
        {
            if (unit == null || !unit.Alive || Time.time < nextThought) return;
            nextThought = Time.time + ThinkEvery;
            if (Time.time < backingOffUntil) return;

            var game = DemoGame.Current;
            var foe = game != null ? game.FoeOf(unit) : null;
            if (foe == null || !unit.CanTarget(foe))
            {
                unit.Stop();
                return;
            }

            float health = unit.HealthShare;
            foreach (var ability in CastOrder())
            {
                if (!unit.Ready(ability) || !Fits(ability, foe, health, out bool away)) continue;
                if (unit.Cast(ability, away ? unit.DataAway(foe) : unit.DataAt(foe))) return;
            }

            // Low, it backs off once in a while, then turns back
            if (health < BackOffBelow && Time.time - backedOffAt > 8f)
            {
                var away = unit.transform.position - foe.transform.position;
                away.y = 0f;
                unit.MoveTo(unit.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * 5f);
                backedOffAt = Time.time;
                backingOffUntil = Time.time + 1.5f;
                return;
            }
            if (unit.Target != foe) unit.Attack(foe);
        }

        // Its Q, W and E in the order its hero's Local Data gives, as "W Q E"; with none, Q, W, E
        private IEnumerable<Ability> CastOrder()
        {
            var roster = unit.Roster;
            string order = roster != null && unit.Gas.TryGetLocalData(roster.CastOrder, out var data) ? data.stringValue : null;
            if (string.IsNullOrWhiteSpace(order)) order = "Q W E";
            foreach (var key in order.Split(' ', ','))
            {
                int slot = key.Trim().ToUpperInvariant() switch { "Q" => 1, "W" => 2, "E" => 3, _ => -1 };
                var ability = unit.AbilityAt(slot);
                if (ability != null) yield return ability;
            }
        }

        // Whether now is when an ability's Local Data says to cast it: in reach of the foe, hurt, low (and away from the
        // foe, as an escape), stunned (to break it), or with the foe low (to finish it)
        private bool Fits(Ability ability, DemoUnit foe, float health, out bool away)
        {
            var roster = unit.Roster;
            string when = ability.TryGetLocalData(roster.CastWhen, out var whenData) ? whenData.stringValue : "InRange";
            float reach = ability.TryGetLocalData(roster.Reach, out var reachData) ? reachData.floatValue : 0f;
            bool inReach = reach <= 0f || unit.DistanceTo(foe) <= reach;
            away = when == "Low";
            return when switch
            {
                "Hurt" => health < 0.6f,
                "Low" => health < 0.35f,
                "Stunned" => unit.Has(roster.Stunned),
                "FoeLow" => inReach && foe.HealthShare < 0.5f,
                _ => inReach,
            };
        }
    }
}
