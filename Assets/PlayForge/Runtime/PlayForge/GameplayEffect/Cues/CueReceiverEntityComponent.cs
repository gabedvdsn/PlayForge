using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cue Receiver: plays cue tags its own way for its entity, as a golem's hits, before the project's cue library
    /// does, as Unreal's actors handle cues through their GameplayCue interface. Its own routes play first; where it
    /// plays nothing for a tag, or passes it through, the library's route plays, its Variant's where the library has
    /// one, as Cue.Hit.Golem for Cue.Hit. An entity holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Plays cue tags its own way for this entity, before the project's cue library.")]
    public sealed class CueReceiverEntityComponent : AbstractEntityComponent
    {
        [Tooltip("Its own routes, which play for its cues before the library's")]
        public List<CueRoute> Routes = new();

        [Tooltip("Plays the library's route too, after its own, for a tag it plays")]
        public bool PassThrough;

        [Tooltip("Its variant, by its last name: the library's route for a cue tag under that name plays in place of the " +
                 "tag's own, as Cue.Hit.Golem for Cue.Hit")]
        [ForgeTagContext(ForgeContext.Cue)]
        public Tag Variant;

        public override bool AllowsSeveral => false;

        public override string Describe()
        {
            int routes = 0;
            if (Routes != null)
                foreach (var route in Routes)
                    if (route != null) routes++;
            var parts = new List<string> { routes == 1 ? "1 cue tag its own way" : $"{routes} cue tags its own way" };
            if (routes > 0 && PassThrough) parts.Add("then the library's");
            var variant = Variant.GetLeafName();
            if (!string.IsNullOrEmpty(variant)) parts.Add($"the library's {variant} routes");
            return "Plays " + string.Join(", ", parts);
        }
    }
}
