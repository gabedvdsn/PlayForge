using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The fixed-rate tick. With ProcessControl's tick rate set, the game steps in ticks, each a fixed
    /// step of its time, a number of them a frame: every process steps once a tick, its end of frame with it, and a
    /// sequence waits for the next tick where it waited for the next frame. So a run comes out the same at any frame rate.
    /// Off, as it is by default, a wait is Unity's next frame, as it was. A world of its own has a tick of its own, in force
    /// while it steps or moves a process, so its sequences wait on its steps. The ticks run on the game's time and stand
    /// with it, so a sequence on a clock that runs while it stands, as <see cref="UnscaledClock"/> does, waits for Unity's
    /// frames.
    /// </summary>
    public static class ForgeTick
    {
        private static readonly ForgeTickSource scene = new();
        private static ForgeTickSource current = scene;

        // Statics outlive play mode when domain reload is off, so each session starts with no ticks and no waits
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTicks()
        {
            scene.Reset();
            current = scene;
        }

        /// <summary>The scene's tick, which ProcessControl's tick rate turns on.</summary>
        public static ForgeTickSource Scene => scene;

        /// <summary>The tick in force: a world of its own's while it steps or moves a process, and the scene's otherwise.</summary>
        public static ForgeTickSource Current => current;

        /// <summary>Whether the tick in force ticks now.</summary>
        public static bool Ticking
        {
            get => current.Ticking;
            internal set => current.Ticking = value;
        }

        /// <summary>The ticks since the tick in force began ticking, which a tick's clock reads as its frame.</summary>
        public static int Ticks => current.Ticks;

        /// <summary>Waits for the next tick, or, with ticks off, Unity's next frame, as a sequence's waits have.</summary>
        public static UniTask Next(CancellationToken token = default) => current.Next(token);

        /// <summary>
        /// Waits for the next tick for a wait at a frame's timing: the Update family waits for a tick, and FixedUpdate, the
        /// physics step's, for Unity's.
        /// </summary>
        public static UniTask Next(PlayerLoopTiming timing, CancellationToken token = default) => current.Next(timing, token);

        /// <summary>Waits for the next tick, or, with ticks off, Unity's next frame as UniTask.NextFrame does.</summary>
        public static UniTask NextFrame(CancellationToken token = default) => current.NextFrame(token);

        /// <summary>
        /// Waits while the condition holds, asking once a tick where the tick in force ticks, and once a frame, as
        /// UniTask.WaitWhile does, where it doesn't. A wait that asked Unity's loop never saw a world of its own change
        /// between its steps.
        /// </summary>
        public static UniTask WaitWhile(Func<bool> condition, CancellationToken token = default) =>
            current.WaitWhile(condition, PlayerLoopTiming.Update, token);

        /// <summary>Waits while the condition holds, at a frame's timing: the Update family asks once a tick.</summary>
        public static UniTask WaitWhile(Func<bool> condition, PlayerLoopTiming timing, CancellationToken token = default) =>
            current.WaitWhile(condition, timing, token);

        /// <summary>Waits until the condition holds, as <see cref="WaitWhile(Func{bool}, CancellationToken)"/> waits while it does.</summary>
        public static UniTask WaitUntil(Func<bool> condition, CancellationToken token = default) =>
            current.WaitUntil(condition, PlayerLoopTiming.Update, token);

        /// <summary>Waits until the condition holds, at a frame's timing: the Update family asks once a tick.</summary>
        public static UniTask WaitUntil(Func<bool> condition, PlayerLoopTiming timing, CancellationToken token = default) =>
            current.WaitUntil(condition, timing, token);

        /// <summary>
        /// Waits for the next step of a clock's time: the next tick, as <see cref="Next(CancellationToken)"/> does, or
        /// Unity's next frame for a clock that runs while the game's time stands (<see cref="IForgeClock.FollowsTicks"/>),
        /// where the scene's ticks, which run on the game's time, would hold it.
        /// </summary>
        public static UniTask Next(IForgeClock clock, CancellationToken token = default) =>
            ByFrames(clock) ? UniTask.Yield(PlayerLoopTiming.Update, token) : current.Next(token);

        // Waits while the condition holds, as WaitWhile does, asking once a step of the clock's time
        internal static UniTask WaitWhile(Func<bool> condition, IForgeClock clock, CancellationToken token = default) =>
            ByFrames(clock)
                ? UniTask.WaitWhile(condition, PlayerLoopTiming.Update, token)
                : current.WaitWhile(condition, PlayerLoopTiming.Update, token);

        // Waits until the condition holds, as WaitUntil does, asking once a step of the clock's time
        internal static UniTask WaitUntil(Func<bool> condition, IForgeClock clock, CancellationToken token = default) =>
            ByFrames(clock)
                ? UniTask.WaitUntil(condition, PlayerLoopTiming.Update, token)
                : current.WaitUntil(condition, PlayerLoopTiming.Update, token);

        // A clock that runs while the game's time stands has its waits go by Unity's frames where the scene ticks, as the
        // scene's ticks stand with the game's time. A world of its own's tick is all the time there is in it
        private static bool ByFrames(IForgeClock clock) => clock is { FollowsTicks: false } && current == scene && scene.Ticking;

        // Whether a wait under a token is one of a sequence's run whose clock runs while the game's time stands, which goes
        // by Unity's frames where the scene ticks. The task sequence runtime answers it for its runs' tokens
        internal static Func<CancellationToken, bool> WaitsByFrames;

        // One tick of the tick in force
        internal static void Advance() => current.Advance();

        // The tick in force stops ticking
        internal static void Stop() => current.Stop();

        // Puts a tick in force, as a world of its own does while it steps, and gives back the one before
        internal static ForgeTickSource Enter(ForgeTickSource source)
        {
            var previous = current;
            current = source ?? scene;
            return previous;
        }
    }

    /// <summary>
    /// A tick: the waits for its next one, which it lets go as it ticks, and how many it has run. The scene has one, which
    /// ProcessControl's tick rate turns on, and each world of its own has its own, which ticks as its owner steps it.
    /// </summary>
    public sealed class ForgeTickSource
    {
        // The waits for the next tick, and the ones a tick lets go, which are kept apart so a wait made as one is let go
        // waits for the tick after
        private List<(AutoResetUniTaskCompletionSource Source, CancellationToken Token)> waiting = new();
        private List<(AutoResetUniTaskCompletionSource Source, CancellationToken Token)> letGo = new();

        /// <summary>Whether it ticks now. A wait on one that doesn't is Unity's next frame.</summary>
        public bool Ticking { get; internal set; }

        /// <summary>The ticks since it began ticking.</summary>
        public int Ticks { get; private set; }

        /// <summary>
        /// Waits for the next tick, or, with ticks off, Unity's next frame. A wait of a sequence's run on a clock that runs
        /// while the game's time stands is Unity's next frame on the scene's tick too.
        /// </summary>
        public UniTask Next(CancellationToken token = default)
        {
            return TicksFor(token) ? Tick(token) : UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        /// <summary>
        /// Waits for the next tick for a wait at a frame's timing: the Update family waits for a tick, and FixedUpdate, the
        /// physics step's, for Unity's.
        /// </summary>
        public UniTask Next(PlayerLoopTiming timing, CancellationToken token = default)
        {
            return TicksAt(timing, token) ? Tick(token) : UniTask.Yield(timing, token);
        }

        /// <summary>Waits for the next tick, or, with ticks off, Unity's next frame as UniTask.NextFrame does.</summary>
        public UniTask NextFrame(CancellationToken token = default)
        {
            return TicksFor(token) ? Tick(token) : UniTask.NextFrame(token);
        }

        /// <summary>
        /// Waits while the condition holds, asking once a tick while this ticks, or as UniTask.WaitWhile does at the
        /// timing while it doesn't, and at the physics step's timings, which are Unity's.
        /// </summary>
        public UniTask WaitWhile(Func<bool> condition, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken token = default) =>
            TicksAt(timing, token) ? Asked(condition, whileSo: true, token) : UniTask.WaitWhile(condition, timing, token);

        /// <summary>Waits until the condition holds, as <see cref="WaitWhile"/> waits while it does.</summary>
        public UniTask WaitUntil(Func<bool> condition, PlayerLoopTiming timing = PlayerLoopTiming.Update, CancellationToken token = default) =>
            TicksAt(timing, token) ? Asked(condition, whileSo: false, token) : UniTask.WaitUntil(condition, timing, token);

        // The wait for the next tick
        private UniTask Tick(CancellationToken token)
        {
            if (token.IsCancellationRequested) return UniTask.FromCanceled(token);
            var source = AutoResetUniTaskCompletionSource.Create();
            waiting.Add((source, token));
            return source.Task;
        }

        // Whether a wait under the token at the timing waits on this tick: the Update family does while it ticks
        private bool TicksAt(PlayerLoopTiming timing, CancellationToken token) =>
            timing is not (PlayerLoopTiming.FixedUpdate or PlayerLoopTiming.LastFixedUpdate or PlayerLoopTiming.EarlyUpdate
                or PlayerLoopTiming.LastEarlyUpdate or PlayerLoopTiming.Initialization or PlayerLoopTiming.LastInitialization)
            && TicksFor(token);

        // Whether a wait under the token waits on this tick: while it ticks, but for a wait on the scene's of a sequence's
        // run whose clock runs while the game's time stands, which the scene's ticks stand with, and which goes by Unity's
        // frames. A token no source can cancel belongs to no run
        private bool TicksFor(CancellationToken token) =>
            Ticking && !(token.CanBeCanceled && ReferenceEquals(this, ForgeTick.Scene)
                         && ForgeTick.WaitsByFrames is { } byFrames && byFrames(token));

        // Asks once a tick, on this tick, until the condition is otherwise
        private async UniTask Asked(Func<bool> condition, bool whileSo, CancellationToken token)
        {
            while (condition() == whileSo) await Next(token);
        }

        // One tick: the waits made before it go on, and the ones they make wait for the next
        internal void Advance()
        {
            Ticks++;
            (letGo, waiting) = (waiting, letGo);
            try
            {
                for (int i = 0; i < letGo.Count; i++)
                {
                    var (source, token) = letGo[i];
                    if (token.IsCancellationRequested) source.TrySetCanceled(token);
                    else source.TrySetResult();
                }
            }
            finally
            {
                letGo.Clear();
            }
        }

        // Ticking stops: what waits goes on at once, a frame's wait at last
        internal void Stop()
        {
            Ticking = false;
            while (waiting.Count > 0) Advance();
        }

        internal void Reset()
        {
            Ticking = false;
            Ticks = 0;
            waiting.Clear();
            letGo.Clear();
        }
    }
}
