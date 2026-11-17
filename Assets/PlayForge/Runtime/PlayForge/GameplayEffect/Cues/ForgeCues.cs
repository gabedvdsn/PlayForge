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

        public delegate void CuePlayedDelegate(ECueEvent cueEvent, AbstractCueHandler handler, in CueContext cue);

        /// <summary>Heard as each cue plays, after its handler: for a debugger, a recorder or a test.</summary>
        public static event CuePlayedDelegate Played;

        /// <summary>Plays a burst.</summary>
        public static void Burst(AbstractCueHandler handler, in CueContext cue) => Play(ECueEvent.Burst, handler, cue);

        /// <summary>Begins a lasting cue.</summary>
        public static void Begin(AbstractCueHandler handler, in CueContext cue) => Play(ECueEvent.Begin, handler, cue);

        /// <summary>Ends a lasting cue.</summary>
        public static void End(AbstractCueHandler handler, in CueContext cue) => Play(ECueEvent.End, handler, cue);

        /// <summary>Plays each handler's burst, in order.</summary>
        public static void Burst(IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue) => Play(ECueEvent.Burst, handlers, cue);

        /// <summary>Begins each handler's lasting cue, in order.</summary>
        public static void Begin(IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue) => Play(ECueEvent.Begin, handlers, cue);

        /// <summary>Ends each handler's lasting cue, in order.</summary>
        public static void End(IReadOnlyList<AbstractCueHandler> handlers, in CueContext cue) => Play(ECueEvent.End, handlers, cue);

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
