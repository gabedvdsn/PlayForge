using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Places a prefab at the cue's target: a burst as its moment comes, gone after its Lifetime, or, While Active, for
    /// as long as its cue lasts, following a target that has a place. In a Cues component's list of lasting cues, as an
    /// ability's While Active, it lasts whatever its While Active says.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Places a prefab at the target, for a moment or for as long as the cue lasts.")]
    public class PrefabCue : AbstractCueHandler
    {
        public GameObject Prefab;

        [Tooltip("For as long as the cue lasts, as an effect while it's on, in place of a burst as it lands")]
        public bool WhileActive;

        [Tooltip("How long a burst's prefab stays, in seconds; 0 keeps it")]
        [NumberMin(0f)] public ScalableFloat Lifetime = 2f;

        // What each lasting cue placed, by its key, as an effect's spec: one handler serves every entity it plays on
        [NonSerialized] private Dictionary<object, GameObject> placed;

        public override void OnApplied(CueContext cue)
        {
            if (WhileActive || Prefab == null) return;
            var made = Place(cue, follow: false);
            float lifetime = Lifetime?.Evaluate(NumberContext.Of(in cue)) ?? 0f;
            if (lifetime > 0f) Object.Destroy(made, lifetime);
        }

        public override void Begin(CueContext cue)
        {
            if (!(WhileActive || cue.Lasting) || Prefab == null || KeyOf(cue) is not { } key) return;
            (placed ??= new Dictionary<object, GameObject>())[key] = Place(cue, follow: true);
        }

        public override void End(CueContext cue)
        {
            if (placed == null || KeyOf(cue) is not { } key || !placed.Remove(key, out var made)) return;
            if (made != null) Object.Destroy(made);
        }

        // What pairs its beginning with its end: the cue's key, or its effect's spec for a cue raised with none
        private static object KeyOf(in CueContext cue) => cue.Key ?? cue.Spec;

        private GameObject Place(CueContext cue, bool follow)
        {
            var at = cue.Transform;
            if (at == null) return Object.Instantiate(Prefab);
            return follow ? Object.Instantiate(Prefab, at.position, at.rotation, at) : Object.Instantiate(Prefab, at.position, at.rotation);
        }
    }
}
