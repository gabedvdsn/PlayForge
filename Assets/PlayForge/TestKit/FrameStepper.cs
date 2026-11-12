using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Testing
{
    /// <summary>
    /// Steps the real player loop a frame at a time, so a test can say "this takes exactly N frames" (TW-26). A step ends
    /// at the frame's last player loop stage, once FixedUpdate, Update and LateUpdate have all run, so a test only ever
    /// looks at finished frames.
    /// <para>
    /// Time is captured: every frame lasts exactly <see cref="DeltaTime"/> and runs one FixedUpdate, however fast the
    /// machine is. Unscaled time doesn't follow, so anything timed on it stays on the wall clock. WaitForEndOfFrame never
    /// resumes under -nographics, which is why a step waits on a player loop stage instead.
    /// </para>
    /// </summary>
    public sealed class FrameStepper
    {
        /// <summary>A frame's length unless a test says otherwise: 1/60 of a second.</summary>
        public const float DefaultDeltaTime = 1f / 60f;

        private readonly FramePulse pulse;
        private readonly float savedCapture, savedFixed, savedTimeScale;

        /// <summary>How long each frame lasts.</summary>
        public float DeltaTime { get; private set; }

        /// <summary>Frames stepped since the test body started.</summary>
        public int Frame { get; private set; }

        /// <summary>The Updates, FixedUpdates and LateUpdates Unity ran, so a test can see a frame was a whole one.</summary>
        public int Updates => pulse.Updates;
        public int FixedUpdates => pulse.FixedUpdates;
        public int LateUpdates => pulse.LateUpdates;

        internal FrameStepper(float deltaTime)
        {
            var host = new GameObject("Frame Pulse");
            Object.DontDestroyOnLoad(host);
            pulse = host.AddComponent<FramePulse>();

            savedCapture = Time.captureDeltaTime;
            savedFixed = Time.fixedDeltaTime;
            savedTimeScale = Time.timeScale;

            SetDeltaTime(deltaTime);
            Time.timeScale = 1f;
        }

        /// <summary>Runs from here on at <paramref name="fps"/> frames a second, still fixed and still one FixedUpdate each.</summary>
        public void SetFrameRate(int fps) => SetDeltaTime(1f / fps);

        private void SetDeltaTime(float deltaTime)
        {
            DeltaTime = deltaTime;
            Time.captureDeltaTime = deltaTime;
            Time.fixedDeltaTime = deltaTime;
        }

        internal void Release()
        {
            Time.captureDeltaTime = savedCapture;
            Time.fixedDeltaTime = savedFixed;
            Time.timeScale = savedTimeScale;

            if (pulse) Object.Destroy(pulse.gameObject);
        }

        /// <summary>Moves to a frame's end and starts counting there, so frame 0 is a whole frame.</summary>
        public async UniTask Align()
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Frame = 0;
        }

        /// <summary>Runs whole frames.</summary>
        public async UniTask Step(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Frame++;
            }
        }

        /// <summary>Runs as many frames as <paramref name="seconds"/> of captured time, rounded to the nearest.</summary>
        public UniTask Seconds(float seconds) => Step(Mathf.RoundToInt(seconds / DeltaTime));

        /// <summary>
        /// Steps until <paramref name="condition"/> holds at a frame's end, and fails the test if it hasn't within
        /// <paramref name="within"/> frames. Returns the frames it took.
        /// </summary>
        public async UniTask<int> Until(Func<bool> condition, int within, string what)
        {
            int taken = 0;
            while (!condition())
            {
                if (taken >= within) throw new AssertionException($"{what}: not true within {within} frames");
                await Step();
                taken++;
            }
            return taken;
        }

        /// <summary>Fails the test unless <paramref name="condition"/> first holds exactly <paramref name="frames"/> frames from now.</summary>
        public async UniTask ExactlyIn(int frames, Func<bool> condition, string what)
        {
            int taken = await Until(condition, frames, what);
            if (taken != frames) throw new AssertionException($"{what}: expected in exactly {frames} frames, took {taken}");
        }

        /// <summary>Fails the test if <paramref name="condition"/> stops holding at any frame's end for the next <paramref name="frames"/> frames.</summary>
        public async UniTask Holds(int frames, Func<bool> condition, string what)
        {
            for (int i = 0; i < frames; i++)
            {
                await Step();
                if (!condition()) throw new AssertionException($"{what}: stopped holding after {i + 1} of {frames} frames");
            }
        }
    }
}
