using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The training dummy: it stands and takes what it's dealt, and counts it. It never dies, since no death watcher is
    /// in its worker group, and after a few seconds left alone it mends to full, by the roster's mend effect. The panel
    /// shows its damage per second and the last blow, colored by its damage type.
    /// </summary>
    public class DemoDummy : MonoBehaviour
    {
        public static DemoDummy Current { get; private set; }

        [Tooltip("Seconds left alone before it mends")]
        public float RestSeconds = 3f;

        [Tooltip("The seconds its damage per second is counted over")]
        public float Window = 5f;

        public float DamagePerSecond { get; private set; }
        public float LastBlow { get; private set; }
        public Tag LastKind { get; private set; }

        private DemoUnit unit;
        private readonly Queue<(float at, float amount)> blows = new();
        private float lastBlowAt = -99f;
        private bool listening;

        private void Start()
        {
            unit = GetComponent<DemoUnit>();
            Current = this;
            if (unit != null && unit.Gas != null)
            {
                unit.Gas.GetCallbacks().OnImpactReceived += Took;
                listening = true;
            }
        }

        private void OnDestroy()
        {
            if (listening && unit != null && unit.Gas != null) unit.Gas.GetCallbacks().OnImpactReceived -= Took;
            if (Current == this) Current = null;
        }

        private void Took(ImpactData impact)
        {
            var roster = unit.Roster;
            if (roster == null || impact.Attribute == null || !impact.Attribute.Equals(roster.Health)) return;
            float amount = -impact.RealImpact.CurrentValue;
            if (amount <= 0f) return;
            blows.Enqueue((Time.time, amount));
            LastBlow = amount;
            LastKind = DemoHud.KindOf(roster, impact);
            lastBlowAt = Time.time;
        }

        private void Update()
        {
            if (unit == null || !unit.Alive) return;
            while (blows.Count > 0 && Time.time - blows.Peek().at > Window) blows.Dequeue();
            float sum = 0f;
            foreach (var blow in blows) sum += blow.amount;
            DamagePerSecond = sum / Window;

            var roster = unit.Roster;
            if (Time.time - lastBlowAt < RestSeconds || roster == null || roster.DummyMend == null) return;
            if (unit.Value(roster.Health) >= unit.Base(roster.Health)) return;
            unit.Gas.ApplyGameplayEffect(unit.Gas.GenerateEffectSpec(IEffectOrigin.GenerateSourceDerivation(unit.Gas), roster.DummyMend));
            lastBlowAt = Time.time;
        }
    }
}
