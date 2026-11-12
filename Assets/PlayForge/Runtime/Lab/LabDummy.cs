using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>A blow a dummy took: when, how much, of what types, and from which effect.</summary>
    public readonly struct LabBlow
    {
        public readonly float At;
        public readonly float Amount;
        public readonly List<Tag> Types;
        public readonly string From;

        public LabBlow(float at, float amount, List<Tag> types, string from)
        {
            At = at;
            Amount = amount;
            Types = types;
            From = from;
        }
    }

    /// <summary>
    /// A test dummy (LAB-4), and the Demo's training dummy: a unit that stands and counts what it takes. It shows its
    /// damage a second over a window, the last blow with its types, and a short history of blows. It doesn't die unless
    /// asked: falling, it's revived where it stands at once. Left alone a few seconds, it mends to full, by the mend effect
    /// given, or else one of the lab's own.
    /// </summary>
    [RequireComponent(typeof(LabUnit))]
    public class LabDummy : MonoBehaviour
    {
        [Tooltip("Seconds left alone before it mends")]
        public float RestSeconds = 3f;

        [Tooltip("The seconds its damage per second is counted over")]
        public float Window = 5f;

        [Tooltip("How many blows its history keeps")]
        public int HistoryLength = 8;

        [Tooltip("Whether it dies as its health runs out; off, it's revived where it stands at once")]
        public bool Dies;

        [Tooltip("Mends it to full as it rests; with none, the lab's own mend")]
        public GameplayEffect Mend;

        public float DamagePerSecond { get; private set; }
        public float LastBlow => history.Count > 0 ? history[history.Count - 1].Amount : 0f;
        public LabBlow? Last => history.Count > 0 ? history[history.Count - 1] : null;

        /// <summary>The blows it's kept, oldest first.</summary>
        public IReadOnlyList<LabBlow> History => history;

        /// <summary>Raised as it takes a blow.</summary>
        public event System.Action<LabDummy, LabBlow> Took;

        private LabUnit unit;
        private readonly Queue<(float at, float amount)> window = new();
        private readonly List<LabBlow> history = new();
        private float lastBlowAt = -99f;
        private float now;
        private bool listening;
        private GameplayEffect ownMend;

        public LabUnit Unit => unit ? unit : unit = GetComponent<LabUnit>();

        protected virtual void Start()
        {
            if (Unit == null || unit.Gas == null) return;
            unit.Gas.GetCallbacks().OnImpactReceived += Heard;
            listening = true;
        }

        protected virtual void OnDestroy()
        {
            if (listening && unit != null && unit.Gas != null) unit.Gas.GetCallbacks().OnImpactReceived -= Heard;
            if (ownMend) Destroy(ownMend);
        }

        private void Heard(ImpactData impact)
        {
            var health = unit.Rules?.Health;
            if (health == null || impact.Attribute == null || !impact.Attribute.Equals(health)) return;
            float amount = -impact.RealImpact.CurrentValue;
            if (amount <= 0f) return;
            var carrier = impact.SourcedModifier.Carrier;
            var types = carrier?.GetImpactTypes() is { } given ? new List<Tag>(given) : new List<Tag>();
            string from = carrier?.GetReadableDefinition() is { } definition ? definition.GetName() : "";
            var blow = new LabBlow(now, amount, types, from);
            window.Enqueue((now, amount));
            history.Add(blow);
            while (history.Count > Mathf.Max(1, HistoryLength)) history.RemoveAt(0);
            lastBlowAt = now;
            Took?.Invoke(this, blow);
        }

        /// <summary>Forgets what it took: its damage a second, its last blow and its history.</summary>
        public void Clear()
        {
            window.Clear();
            history.Clear();
            DamagePerSecond = 0f;
        }

        protected virtual void Update()
        {
            if (Unit != null && !unit.SteppedByLab) Step(ForgeTime.DeltaTime);
        }

        /// <summary>A step by the time given: its count over the window, a revive where it fell, and a mend as it rests.</summary>
        public void Step(float deltaTime)
        {
            now += Mathf.Max(0f, deltaTime);
            if (Unit == null || unit.Gas == null) return;

            if (!unit.Alive)
            {
                if (Dies) return;
                unit.Gas.Revive(unit.transform.position);
                MendNow();
                return;
            }

            while (window.Count > 0 && now - window.Peek().at > Window) window.Dequeue();
            float sum = 0f;
            foreach (var blow in window) sum += blow.amount;
            DamagePerSecond = Window > 0f ? sum / Window : 0f;

            if (now - lastBlowAt < RestSeconds) return;
            var health = unit.Rules?.Health;
            if (health == null || unit.Value(health) >= unit.Base(health)) return;
            MendNow();
        }

        /// <summary>Mends it to full now, by its mend effect.</summary>
        public void MendNow()
        {
            var health = unit != null ? unit.Rules?.Health : null;
            if (health == null || unit.Gas == null) return;
            var mend = Mend;
            if (!mend)
            {
                // As much as is missing, so an attribute with no ceiling of its own isn't overfilled
                mend = ownMend ? ownMend : ownMend = LabEffects.Restore(health);
                float missing = unit.Base(health) - unit.Value(health);
                if (missing <= 0f) return;
                LabEffects.SetAmount(mend, missing);
            }
            unit.Gas.ApplyGameplayEffect(unit.Gas.GenerateEffectSpec(IEffectOrigin.GenerateSourceDerivation(unit.Gas), mend));
            lastBlowAt = now;
        }
    }
}
