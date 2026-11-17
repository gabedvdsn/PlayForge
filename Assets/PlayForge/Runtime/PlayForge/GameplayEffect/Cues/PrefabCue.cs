using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Places a prefab at the cue's target: a burst as its moment comes, gone after its Lifetime, or, While Active, for
    /// as long as its cue lasts, following a target that has a place. In a Cues component's list of lasting cues, as an
    /// ability's While Active, it lasts whatever its While Active says. A cue raised at a point, as a hit's, places it
    /// there. Pooled, it takes its objects from <see cref="ForgeCuePool"/> and gives them back, as Unreal recycles its
    /// cue actors.
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

        [Tooltip("Takes its objects from a pool and gives them back as its cue ends, in place of making and destroying " +
                 "one each time")]
        public bool Pooled;

        [Tooltip("How many objects its pool makes ahead as it first plays, so a fight's first hit doesn't hitch")]
        [Min(0)] public int Preallocate;

        // What each lasting cue placed, by its key, as an effect's spec: one handler serves every entity it plays on
        [NonSerialized] private Dictionary<object, GameObject> placed;

        // Whether its pool has made its Preallocate objects ahead, as it does the first time it plays
        [NonSerialized] private bool warmed;

        public override void OnApplied(CueContext cue)
        {
            if (WhileActive || Prefab == null) return;
            var made = Place(cue, follow: false);
            float lifetime = Lifetime?.Evaluate(NumberContext.Of(in cue)) ?? 0f;
            if (lifetime > 0f) Let(made, lifetime);
        }

        public override void Begin(CueContext cue)
        {
            if (!(WhileActive || cue.Lasting) || Prefab == null || KeyOf(cue) is not { } key) return;
            (placed ??= new Dictionary<object, GameObject>())[key] = Place(cue, follow: true);
        }

        public override void End(CueContext cue)
        {
            if (placed == null || KeyOf(cue) is not { } key || !placed.Remove(key, out var made)) return;
            Let(made);
        }

        // What pairs its beginning with its end: the cue's key, or its effect's spec for a cue raised with none
        private static object KeyOf(in CueContext cue) => cue.Key ?? cue.Spec;

        // At the point its raiser gave, as a hit's, facing its normal, or, for a target that has no place, at the cue's
        // position; else at its target, following it while it lasts
        private GameObject Place(CueContext cue, bool follow)
        {
            if (Pooled && !warmed)
            {
                warmed = true;
                ForgeCuePool.Preallocate(Prefab, Preallocate);
            }
            var at = cue.Transform;
            if (cue.Point.HasValue || at == null)
            {
                var facing = cue.Normal is { } normal && normal != Vector3.zero ? Quaternion.LookRotation(normal) : Quaternion.identity;
                return Make(cue.Position, facing, null);
            }
            return Make(at.position, at.rotation, follow ? at : null);
        }

        private GameObject Make(Vector3 position, Quaternion rotation, Transform parent)
        {
            if (Pooled) return ForgeCuePool.Take(Prefab, position, rotation, parent);
            return parent != null ? Object.Instantiate(Prefab, position, rotation, parent) : Object.Instantiate(Prefab, position, rotation);
        }

        // Gone, now or after some seconds: back to its pool where it pools
        private void Let(GameObject made, float after = 0f)
        {
            if (made == null) return;
            if (Pooled && after > 0f) ForgeCuePool.GiveAfter(made, after);
            else if (Pooled) ForgeCuePool.Give(made);
            else if (after > 0f) Object.Destroy(made, after);
            else Object.Destroy(made);
        }
    }
}
