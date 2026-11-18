using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Places a prefab where its placement says: a burst as its moment comes, gone after its Lifetime, or, in a Cues
    /// component's list of lasting cues, as an effect's While Active, for as long as its cue lasts. Its placement puts it
    /// on the one the cue is on, its source, an ability's target or
    /// each of its targets, or at the cue's point, as a hit's: at a socket, moved and scattered, following them while it
    /// lasts or as it says. Pooled, it takes its objects from <see cref="ForgeCuePool"/> and gives them back, as Unreal
    /// recycles its cue actors.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Places a prefab where its placement says, for a moment or for as long as the cue lasts.")]
    public class PrefabCue : AbstractCueHandler
    {
        public GameObject Prefab;

        [Tooltip("Where it's placed, on whom, and which way it faces")]
        public CuePlacement Placement = new();

        [Tooltip("How long a burst's prefab stays, in seconds; 0 keeps it")]
        [NumberMin(0f)] public ScalableFloat Lifetime = 2f;

        [Tooltip("Takes its objects from a pool and gives them back as its cue ends, in place of making and destroying " +
                 "one each time")]
        public bool Pooled;

        [Tooltip("How many objects its pool makes ahead as it first plays, so a fight's first hit doesn't hitch")]
        [Min(0)] public int Preallocate;

        // What each lasting cue placed, by its key, as an effect's spec: one handler serves every entity it plays on
        [NonSerialized] private Dictionary<object, List<GameObject>> placed;

        // Whether its pool has made its Preallocate objects ahead, as it does the first time it plays
        [NonSerialized] private bool warmed;

        public override void OnApplied(CueContext cue)
        {
            if (Prefab == null) return;
            float lifetime = Lifetime?.Evaluate(NumberContext.Of(in cue)) ?? 0f;
            foreach (var place in PlacementOf().Resolve(cue, lasting: false))
            {
                var made = Make(place);
                if (lifetime > 0f) Let(made, lifetime);
            }
        }

        public override void Begin(CueContext cue)
        {
            if (Prefab == null || KeyOf(cue) is not { } key) return;
            var made = new List<GameObject>();
            foreach (var place in PlacementOf().Resolve(cue, lasting: true)) made.Add(Make(place));
            (placed ??= new Dictionary<object, List<GameObject>>())[key] = made;
        }

        public override void End(CueContext cue)
        {
            if (placed == null || KeyOf(cue) is not { } key || !placed.Remove(key, out var made)) return;
            foreach (var each in made) Let(each);
        }

        // What pairs its beginning with its end: the cue's key, or its effect's spec for a cue raised with none
        private static object KeyOf(in CueContext cue) => cue.Key ?? cue.Spec;

        // Its placement, a new one where a cue made before it had none
        private CuePlacement PlacementOf() => Placement ??= new CuePlacement();

        // One of its prefab at a place, taken from its pool where it pools, which makes its Preallocate ahead the first time
        private GameObject Make(CuePlace place)
        {
            if (Pooled && !warmed)
            {
                warmed = true;
                ForgeCuePool.Preallocate(Prefab, Preallocate);
            }
            if (Pooled) return ForgeCuePool.Take(Prefab, place.Position, place.Rotation, place.Parent);
            return place.Parent != null
                ? Object.Instantiate(Prefab, place.Position, place.Rotation, place.Parent)
                : Object.Instantiate(Prefab, place.Position, place.Rotation);
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
