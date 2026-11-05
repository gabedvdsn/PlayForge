using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The fixed-rate tick (FEAT-7, Decision 30). With ProcessControl's tick rate set, the game steps in ticks, each a fixed
    /// step of its time, a number of them a frame: every process steps once a tick, its end of frame with it, and a
    /// sequence waits for the next tick where it waited for the next frame. So a run comes out the same at any frame rate.
    /// Off, as it is by default, a wait is Unity's next frame, as it was.
    /// </summary>
    public static class ForgeTick
    {
        // The waits for the next tick, and the ones a tick lets go, which are kept apart so a wait made as one is let go
        // waits for the tick after
        private static List<(AutoResetUniTaskCompletionSource Source, CancellationToken Token)> waiting = new();
        private static List<(AutoResetUniTaskCompletionSource Source, CancellationToken Token)> letGo = new();

        // Statics outlive play mode when domain reload is off, so each session starts with no ticks and no waits (PC-26)
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTicks()
        {
            Ticking = false;
            Ticks = 0;
            waiting.Clear();
            letGo.Clear();
        }

        /// <summary>Whether the game steps in ticks now.</summary>
        public static bool Ticking { get; internal set; }

        /// <summary>The ticks since ticking began, which a tick's clock reads as its frame.</summary>
        public static int Ticks { get; private set; }

        /// <summary>Waits for the next tick, or, with ticks off, Unity's next frame, as a sequence's waits have.</summary>
        public static UniTask Next(CancellationToken token = default)
        {
            if (!Ticking) return UniTask.Yield(PlayerLoopTiming.Update, token);
            if (token.IsCancellationRequested) return UniTask.FromCanceled(token);
            var source = AutoResetUniTaskCompletionSource.Create();
            waiting.Add((source, token));
            return source.Task;
        }

        /// <summary>
        /// Waits for the next tick for a wait at a frame's timing: the Update family waits for a tick, and FixedUpdate, the
        /// physics step's, for Unity's.
        /// </summary>
        public static UniTask Next(PlayerLoopTiming timing, CancellationToken token = default)
        {
            if (!Ticking || timing is PlayerLoopTiming.FixedUpdate or PlayerLoopTiming.LastFixedUpdate or PlayerLoopTiming.EarlyUpdate
                    or PlayerLoopTiming.LastEarlyUpdate or PlayerLoopTiming.Initialization or PlayerLoopTiming.LastInitialization)
                return UniTask.Yield(timing, token);
            return Next(token);
        }

        /// <summary>Waits for the next tick, or, with ticks off, Unity's next frame as UniTask.NextFrame does.</summary>
        public static UniTask NextFrame(CancellationToken token = default)
        {
            return Ticking ? Next(token) : UniTask.NextFrame(token);
        }

        // One tick: the waits made before it go on, and the ones they make wait for the next
        internal static void Advance()
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
        internal static void Stop()
        {
            Ticking = false;
            while (waiting.Count > 0) Advance();
        }
    }
}
