using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FarEmerald.PlayForge
{
    /// <summary>The game's camera, which a camera shake cue asks to shake.</summary>
    public interface ICameraShaker
    {
        /// <summary>Shakes the camera, as hard as the strength says, for the duration in seconds.</summary>
        void Shake(float strength, float duration);
    }

    /// <summary>
    /// Where every cue plays, whatever raised it: an effect's, an ability's, an entity's, an item's, or a game's own
    /// code, as sparks at a ray's hit. A handler that throws is reported and the rest play on: what something shows
    /// mustn't stop what it does. And what PlayForge's cues reach in the game: the camera shaker the game gives.
    /// </summary>
    public static class ForgeCues
    {
        /// <summary>The shaker a camera shake cue asks, or null, which shakes nothing.</summary>
        public static ICameraShaker Shaker { get; set; }

        /// <summary>
        /// The random numbers cues draw, as a placement's scatter: their own, from a seed, so a cue never moves the
        /// game's rolls, which a server that plays no cues and a client that plays them must draw alike. Put another in
        /// for your own.
        /// </summary>
        public static IForgeRandom Random { get; set; } = new SeededRandom(1);

        /// <summary>The camera a floating text faces, or null for the main camera.</summary>
        public static UnityEngine.Camera Camera { get; set; }

        /// <summary>
        /// Whether cues play. Off, nothing bursts or begins, as on a server or while a simulation runs ahead; a lasting
        /// cue that began still ends.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        // The entities muted, held weakly, so one that's gone isn't kept
        private static readonly ConditionalWeakTable<IGameplayAbilitySystem, object> muted = new();
        private static readonly object Muted = new();

        /// <summary>
        /// Mutes an entity's cues, as one off screen, or plays them again. A lasting cue that began still ends.
        /// </summary>
        public static void Mute(IGameplayAbilitySystem entity, bool mute)
        {
            if (entity == null) return;
            muted.Remove(entity);
            if (mute) muted.Add(entity, Muted);
        }

        /// <summary>Whether an entity's cues are muted.</summary>
        public static bool IsMuted(IGameplayAbilitySystem entity) => entity != null && muted.TryGetValue(entity, out _);

        // The sink each world asks, held weakly, so a world that's gone isn't kept
        private static readonly ConditionalWeakTable<ProcessWorld, ForgeCueSink> sinks = new();

        private static readonly ForgeCueSink Everything = new();

        /// <summary>
        /// The sink a world with none of its own asks, once for each raise, before any of its cues plays: by default one
        /// that plays everything.
        /// </summary>
        public static ForgeCueSink Sink { get; set; } = Everything;

        /// <summary>Gives a world a sink of its own, as a server's or a client's, or takes it back with null.</summary>
        public static void UseSink(ProcessWorld world, ForgeCueSink sink)
        {
            if (world == null) return;
            sinks.Remove(world);
            if (sink != null) sinks.Add(world, sink);
        }

        /// <summary>The sink asked for a cue on an entity: its world's own, or else <see cref="Sink"/>.</summary>
        public static ForgeCueSink SinkFor(IGameplayAbilitySystem entity)
        {
            var world = entity != null ? ProcessWorld.Of(entity) : null;
            if (world != null && sinks.TryGetValue(world, out var own)) return own;
            return Sink ?? Everything;
        }

        /// <summary>
        /// Whether a raise plays: its world's sink asked, once, whatever mutes it; a sink that throws is reported, and
        /// lets it play.
        /// </summary>
        internal static bool WillPlay(ECueEvent cueEvent, in CueContext cue, CueSource from)
        {
            try
            {
                return SinkFor(cue.Target).WillPlay(cueEvent, new CueRaise(cue, from));
            }
            catch (Exception exception)
            {
                ForgeLog.Exception(ForgeLog.ESystem.Effects, exception);
                return true;
            }
        }

        /// <summary>Plays handlers within a raise already asked, as a Cue Tag's route does.</summary>
        internal static void Dispatch(ECueEvent cueEvent, IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue) =>
            Play(cueEvent, handlers, cue);

        /// <summary>
        /// A listener of <c>ForgeCues.Played</c>, invoked as each cue plays, after its handler, handed whether it
        /// bursts, begins or ends, the handler, and the cue's context: what raised it, who's behind it and where it
        /// plays.
        /// </summary>
        public delegate void CuePlayedDelegate(ECueEvent cueEvent, AbstractCueHandler handler, in CueContext cue);

        /// <summary>Heard as each cue plays, after its handler: for a debugger, a recorder or a test.</summary>
        public static event CuePlayedDelegate Played;

        /// <summary>Plays a burst, raised by what <paramref name="from"/> says, a game's own code where it says nothing.</summary>
        public static void Burst(AbstractCueHandler handler, in CueContext cue, CueSource from = default)
        {
            if (handler != null && WillPlay(ECueEvent.Burst, cue, from)) Play(ECueEvent.Burst, handler, cue);
        }

        /// <summary>Begins a lasting cue.</summary>
        public static void Begin(AbstractCueHandler handler, in CueContext cue, CueSource from = default)
        {
            if (handler != null && WillPlay(ECueEvent.Begin, cue, from)) Play(ECueEvent.Begin, handler, cue);
        }

        /// <summary>Ends a lasting cue.</summary>
        public static void End(AbstractCueHandler handler, in CueContext cue, CueSource from = default)
        {
            if (handler != null && WillPlay(ECueEvent.End, cue, from)) Play(ECueEvent.End, handler, cue);
        }

        /// <summary>
        /// Plays each handler's burst, in order: one raise, its world's sink asked once before any plays, raised by what
        /// <paramref name="from"/> says, a game's own code where it says nothing.
        /// </summary>
        public static void Burst(IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue, CueSource from = default) =>
            Raise(ECueEvent.Burst, handlers, cue, from);

        /// <summary>Begins each handler's lasting cue, in order, one raise.</summary>
        public static void Begin(IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue, CueSource from = default) =>
            Raise(ECueEvent.Begin, handlers, cue, from);

        /// <summary>Ends each handler's lasting cue, in order, one raise.</summary>
        public static void End(IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue, CueSource from = default) =>
            Raise(ECueEvent.End, handlers, cue, from);

        // A list with nothing in it isn't a raise: no sink is asked of it
        private static void Raise(ECueEvent cueEvent, IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue, CueSource from)
        {
            if (handlers == null || handlers.Count == 0) return;
            if (WillPlay(cueEvent, cue, from)) Play(cueEvent, handlers, cue);
        }

        private static void Play(ECueEvent cueEvent, IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue)
        {
            if (handlers == null) return;
            for (int i = 0; i < handlers.Count; i++) Play(cueEvent, handlers[i], cue);
        }

        private static void Play(ECueEvent cueEvent, AbstractCueHandler handler, in CueContext cue)
        {
            if (handler == null) return;

            // Muted, nothing bursts or begins; a lasting cue that began still ends, so what it placed doesn't stay
            if (cueEvent != ECueEvent.End && (!Enabled || IsMuted(cue.Target))) return;

            try
            {
                switch (cueEvent)
                {
                    case ECueEvent.Burst: handler.Burst(cue); break;
                    case ECueEvent.Begin: handler.Begin(cue); break;
                    case ECueEvent.End: handler.End(cue); break;
                }
            }
            catch (Exception exception)
            {
                ForgeLog.Exception(ForgeLog.ESystem.Effects, exception);
            }

            if (Played == null) return;
            try
            {
                Played(cueEvent, handler, cue);
            }
            catch (Exception exception)
            {
                ForgeLog.Exception(ForgeLog.ESystem.Effects, exception);
            }
        }
    }
}
