using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A project's cue library: what each cue tag plays. A Cue Tag cue, listed on an effect or a Cues component, plays
    /// the library's route for its tag: the tag's own, or else its nearest parent's, and a route that doesn't override
    /// plays its parent's too, as Unreal's GameplayCueManager routes its GameplayCue tags. An entity's Cue Receiver
    /// plays a tag its own way first. The project names its library in its settings, or code gives one to
    /// <see cref="ForgeCueRoutes.Library"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Cue Library", fileName = "Cue Library", order = 46)]
    public class CueLibrary : ScriptableObject
    {
        [Tooltip("What each cue tag plays: a route for a tag plays for it and for the tags under it that have none")]
        public List<CueRoute> Routes = new();
    }

    /// <summary>
    /// A cue tag's route: the handlers that play for it, and whether they play in place of its parent's.
    /// </summary>
    [Serializable]
    public class CueRoute
    {
        [Tooltip("The cue tag it plays for, and for each tag under it that has no route of its own")]
        [ForgeTagContext(ForgeContext.Cue)]
        public Tag Tag;

        [Tooltip("Plays in place of its parent's route; off, its parent's plays after it too")]
        public bool Override = true;

        [Tooltip("What it plays, in order")]
        [SerializeReference]
        public List<AbstractCueHandler> Handlers = new();
    }
}
