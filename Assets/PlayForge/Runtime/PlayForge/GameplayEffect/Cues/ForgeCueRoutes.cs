using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Plays a cue by its tag, as a Cue Tag cue raises it: the entity's own Cue Receiver first, its own way; then,
    /// where the receiver plays nothing for it or passes it through, the library's route for the tag, the receiver's
    /// variant's where the library has one.
    /// </summary>
    public static class ForgeCueRoutes
    {
        private static CueLibrary library;

        /// <summary>The library cue tags route through: the one code gives, or else the running project's.</summary>
        public static CueLibrary Library
        {
            get
            {
                if (library != null) return library;
                var project = ForgeProject.Current;
                return project != null ? project.Settings?.Cues : null;
            }
            set => library = value;
        }

        /// <summary>Plays the bursts of the cue's tag.</summary>
        public static void Burst(in CueContext cue) => Route(ECueEvent.Burst, cue);

        /// <summary>Begins the lasting cues of the cue's tag.</summary>
        public static void Begin(in CueContext cue) => Route(ECueEvent.Begin, cue);

        /// <summary>Ends the lasting cues of the cue's tag.</summary>
        public static void End(in CueContext cue) => Route(ECueEvent.End, cue);

        private static void Route(ECueEvent cueEvent, in CueContext cue)
        {
            var path = cue.Tag.Path;
            if (string.IsNullOrEmpty(path)) return;

            // The entity's own way first: where it plays the tag and doesn't pass it through, the library's doesn't
            // play
            var receiver = GASCore.Of(cue.Target)?.Identity?.Find<CueReceiverEntityComponent>();
            if (receiver != null && Play(cueEvent, receiver.Routes, path, null, cue) && !receiver.PassThrough) return;

            var routes = Library?.Routes;
            if (routes == null) return;
            Play(cueEvent, routes, path, receiver != null ? VariantOf(receiver.Variant) : null, cue);
        }

        // The route for the tag, else its nearest parent's, at each step the variant's under the step's name before the
        // step's own; one that doesn't override lets the next one play too. Whether any played
        private static bool Play(ECueEvent cueEvent, List<CueRoute> routes, string path, string variant, in CueContext cue)
        {
            if (routes == null) return false;
            bool played = false;
            for (var at = path; at != null; at = ParentOf(at))
            {
                if (variant != null && Find(routes, at + "." + variant) is { } varied)
                {
                    Play(cueEvent, varied, cue);
                    played = true;
                    if (varied.Override) return true;
                }
                if (Find(routes, at) is { } own)
                {
                    Play(cueEvent, own, cue);
                    played = true;
                    if (own.Override) return true;
                }
            }
            return played;
        }

        private static void Play(ECueEvent cueEvent, CueRoute route, in CueContext cue)
        {
            switch (cueEvent)
            {
                case ECueEvent.Burst: ForgeCues.Burst(route.Handlers, cue); break;
                case ECueEvent.Begin: ForgeCues.Begin(route.Handlers, cue); break;
                case ECueEvent.End: ForgeCues.End(route.Handlers, cue); break;
            }
        }

        private static CueRoute Find(List<CueRoute> routes, string path)
        {
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                if (route != null && route.Tag.Path == path) return route;
            }
            return null;
        }

        private static string ParentOf(string path)
        {
            int dot = path.LastIndexOf('.');
            return dot < 0 ? null : path.Substring(0, dot);
        }

        // A variant tag's last name, as Golem for Cue.Variant.Golem, or null where it names none
        private static string VariantOf(Tag variant)
        {
            var leaf = variant.GetLeafName();
            return string.IsNullOrEmpty(leaf) ? null : leaf;
        }
    }
}
