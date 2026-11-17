using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Plays what a cue tag routes to, in place of naming what it shows: the entity's own Cue Receiver's way, or the
    /// project's cue library's, as Unreal's GameplayCue tags play their notifies. Each burst, beginning and end it
    /// hears plays the tag's.
    /// </summary>
    [Serializable]
    [ForgeMenu(Name = "Cue Tag", Description = "Plays what the project's cue library has for its tag, or the entity's own Cue Receiver's.")]
    public class TagCue : AbstractCueHandler
    {
        [Tooltip("The cue tag whose route plays")]
        [ForgeTagContext(ForgeContext.Cue)]
        public Tag Tag;

        public override void Burst(CueContext cue) => ForgeCueRoutes.Within(ECueEvent.Burst, cue.WithTag(Tag));

        public override void Begin(CueContext cue) => ForgeCueRoutes.Within(ECueEvent.Begin, cue.WithTag(Tag));

        public override void End(CueContext cue) => ForgeCueRoutes.Within(ECueEvent.End, cue.WithTag(Tag));
    }
}
