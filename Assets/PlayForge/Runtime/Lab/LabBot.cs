using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// A bot's mind (LAB-10), the Demo's AI and the Battle sample's fighters made general: it takes the nearest foe it can
    /// target, casts what's ready in its order when its moment fits, handing its target in the activation's data, which
    /// click targeting takes with no click, and otherwise attacks. Low, it backs off a moment once in a while.
    /// <para>
    /// Its order is its entity's Local Data under the rules' cast order key, as "W Q E", the letters its casts in its
    /// entity's order; with none, each in turn. An ability's moment is its Local Data under the rules' cast-when key:
    /// InRange, the default, within its Local Data reach, 0 for anywhere; Hurt, below 60% health; Low, below 35%, cast away
    /// from its foe, as an escape; Stunned, while something stops it; FoeLow, with its foe below half.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(LabUnit))]
    public class LabBot : MonoBehaviour
    {
        [Tooltip("Seconds between its decisions")]
        public float ThinkEvery = 0.25f;

        [Tooltip("The share of its health below which it backs off a moment")]
        public float BackOffBelow = 0.2f;

        [Tooltip("How long it backs off for, in seconds")]
        public float BackOffSeconds = 1.5f;

        [Tooltip("The fewest seconds between two backings off")]
        public float BackOffEvery = 8f;

        [Tooltip("How far it backs off")]
        public float BackOffDistance = 5f;

        /// <summary>Whether it thinks; off, it stands with no orders of its own.</summary>
        public bool Fighting = true;

        /// <summary>Who it fights; with none given, the nearest foe it can target.</summary>
        public Func<LabUnit, LabUnit> Foe;

        private LabUnit unit;
        private float now;
        private float nextThought;
        private float backingOffUntil;
        private float backedOffAt = -99f;

        public LabUnit Unit => unit ? unit : unit = GetComponent<LabUnit>();

        protected virtual void Update()
        {
            if (Unit != null && !unit.SteppedByLab) Step(ForgeTime.DeltaTime);
        }

        /// <summary>A step by the time given: a decision, once its think time has come.</summary>
        public void Step(float deltaTime)
        {
            now += Mathf.Max(0f, deltaTime);
            if (!Fighting || Unit == null || !unit.Alive || now < nextThought) return;
            nextThought = now + Mathf.Max(0.02f, ThinkEvery);
            if (now < backingOffUntil) return;

            var foe = Foe != null ? Foe(unit) : Nearest(unit);
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
            if (health < BackOffBelow && now - backedOffAt > BackOffEvery)
            {
                var from = unit.transform.position - foe.transform.position;
                from.y = 0f;
                unit.MoveTo(unit.transform.position + (from.sqrMagnitude > 0.01f ? from.normalized : Vector3.back) * BackOffDistance);
                backedOffAt = now;
                backingOffUntil = now + BackOffSeconds;
                return;
            }

            if (unit.Target != foe) unit.AttackUnit(foe);
        }

        /// <summary>The nearest unit not of its side that it can target, alive, or null.</summary>
        public static LabUnit Nearest(LabUnit from)
        {
            LabUnit best = null;
            float bestDistance = float.MaxValue;
            foreach (var other in LabUnit.All)
            {
                if (!other || other == from || !other.Alive || LabSides.AreAllies(from.Gas, other.Gas) || !from.CanTarget(other)) continue;
                if (!other.Side.IsValid || other.Side.Tag.Equals(LabSides.Neutral.Tag)) continue;
                float distance = from.DistanceTo(other);
                if (distance >= bestDistance) continue;
                best = other;
                bestDistance = distance;
            }
            return best;
        }

        // Its casts in the order its entity's Local Data gives, as "W Q E"; with none, each in turn
        private IEnumerable<Ability> CastOrder()
        {
            var casts = new List<Ability>(unit.Casts());
            var key = unit.Rules?.CastOrderKey ?? default;
            string order = key.IsValid && unit.Gas.TryGetLocalData(key, out var data) ? data.stringValue : null;
            if (string.IsNullOrWhiteSpace(order))
            {
                foreach (var ability in casts) yield return ability;
                yield break;
            }
            foreach (var token in order.Split(' ', ','))
            {
                int index = token.Trim().ToUpperInvariant() switch { "Q" => 0, "W" => 1, "E" => 2, "R" => 3, _ => -1 };
                if (index >= 0 && index < casts.Count) yield return casts[index];
            }
        }

        // Whether now is when an ability's Local Data says to cast it
        private bool Fits(Ability ability, LabUnit foe, float health, out bool away)
        {
            var rules = unit.Rules;
            string when = rules != null && rules.CastWhenKey.IsValid && ability.TryGetLocalData(rules.CastWhenKey, out var whenData)
                ? whenData.stringValue : "InRange";
            float reach = rules != null && rules.ReachKey.IsValid && ability.TryGetLocalData(rules.ReachKey, out var reachData)
                ? reachData.floatValue : 0f;
            bool inReach = reach <= 0f || unit.DistanceTo(foe) <= reach;
            away = when == "Low";
            return when switch
            {
                "Hurt" => health < 0.6f,
                "Low" => health < 0.35f,
                "Stunned" => rules != null && unit.HasAny(rules.Stops),
                "FoeLow" => inReach && foe.HealthShare < 0.5f,
                _ => inReach,
            };
        }
    }
}
