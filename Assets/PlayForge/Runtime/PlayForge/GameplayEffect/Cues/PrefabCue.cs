using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Places a prefab at the effect's target: a burst as the effect lands, gone after its Lifetime, or, While Active, for
    /// as long as the effect lasts, following a target that has a place (FEAT-1).
    /// </summary>
    [Serializable]
    public class PrefabCue : AbstractCueHandler
    {
        public GameObject Prefab;

        [Tooltip("For as long as the effect lasts, in place of a burst as it lands")]
        public bool WhileActive;

        [Tooltip("How long a burst's prefab stays, in seconds; 0 keeps it")]
        [NumberMin(0f)] public ScalableFloat Lifetime = 2f;

        // The prefab each lasting effect placed, by its spec: one handler serves every entity the effect is on
        [NonSerialized] private Dictionary<GameplayEffectSpec, GameObject> placed;

        public override void OnApplied(CueContext cue)
        {
            if (WhileActive || Prefab == null) return;
            var made = Place(cue, follow: false);
            float lifetime = Lifetime?.Evaluate(NumberContext.Of(in cue)) ?? 0f;
            if (lifetime > 0f) Object.Destroy(made, lifetime);
        }

        public override void Begin(CueContext cue)
        {
            if (!WhileActive || Prefab == null) return;
            (placed ??= new Dictionary<GameplayEffectSpec, GameObject>())[cue.Spec] = Place(cue, follow: true);
        }

        public override void End(CueContext cue)
        {
            if (placed == null || !placed.Remove(cue.Spec, out var made)) return;
            if (made != null) Object.Destroy(made);
        }

        private GameObject Place(CueContext cue, bool follow)
        {
            var at = cue.Transform;
            if (at == null) return Object.Instantiate(Prefab);
            return follow ? Object.Instantiate(Prefab, at.position, at.rotation, at) : Object.Instantiate(Prefab, at.position, at.rotation);
        }
    }
}
