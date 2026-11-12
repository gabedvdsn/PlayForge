using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;

namespace FarEmerald.PlayForge.Testing
{
    /// <summary>
    /// The base of a test that plays PlayForge content (TW-26, Decision 66): a live <see cref="ForgeTestWorld"/> before
    /// each test, and after it the world's invariants audited and the world ended. A test is a PlayMode
    /// <c>[UnityTest]</c> whose body runs through <see cref="Live"/>, from a frame's end:
    /// <code>
    /// [UnityTest]
    /// public IEnumerator Burn_ticks_twice() =&gt; Live(async () =&gt;
    /// {
    ///     var health = Make.Attribute("Health");
    ///     var target = World.Entity(Hub.Runtime, Make.Set(Make.Pool(health, 100f, 100f)));
    ///     var burn = Make.Effect("Burn", new EffectConfig { Policy = EEffectDurationPolicy.Durational, Target = health,
    ///         Magnitude = -10f, Duration = 1f, PeriodicTicks = true, Ticks = 2 });
    ///     World.Hit(World.Entity(Hub.Runtime), target, burn);
    ///     await Frames.Until(() =&gt; World.Value(target, health) &lt;= 80f, 90, "two ticks land");
    /// });
    /// </code>
    /// A test with a base class of its own makes a world with <see cref="ForgeTestWorld.Start"/> instead.
    /// </summary>
    public abstract class ForgeTest
    {
        /// <summary>The test's world, made before it and ended after it.</summary>
        protected ForgeTestWorld World { get; private set; }

        /// <summary>Steps the world's frames.</summary>
        protected FrameStepper Frames => World.Frames;

        /// <summary>Builds assets in code, destroyed with the world.</summary>
        protected TestAssets Make => World.Make;

        /// <summary>How long each of the test's frames lasts: 1/60 of a second unless a test class says otherwise.</summary>
        protected virtual float DeltaTime => FrameStepper.DefaultDeltaTime;

        /// <summary>
        /// Whether the world's invariants are audited after each test that passed: on, so a test about something else still
        /// catches a leak. A class whose tests leave processes running on purpose turns it off.
        /// </summary>
        protected virtual bool Audits => true;

        [SetUp]
        public void StartWorld()
        {
            // In two steps, so the teardown still has a world to end if bootstrapping is what failed
            World = new ForgeTestWorld(DeltaTime);
            World.Bootstrap();
        }

        /// <summary>Runs a test's body from a frame's end, so it starts from a whole frame.</summary>
        protected IEnumerator Live(Func<UniTask> body) => UniTask.ToCoroutine(async () =>
        {
            await Frames.Align();
            await body();
        });

        [UnityTearDown]
        public IEnumerator EndWorld() => UniTask.ToCoroutine(async () =>
        {
            var violations = new List<string>();
            try
            {
                // A test that already failed has said what's wrong; what it left would only add noise
                if (Audits && World != null && TestContext.CurrentContext.Result.Outcome.Status != TestStatus.Failed)
                    violations = await World.Audit();
            }
            finally
            {
                if (World != null) await World.End();
                World = null;
            }

            if (violations.Count > 0) throw new AssertionException($"Invariants broken:\n  {string.Join("\n  ", violations)}");
        });
    }
}
