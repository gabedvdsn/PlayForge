using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;

namespace FarEmerald.PlayForge.Testing
{
    /// <summary>The two GAS hubs, so a test can play its content on each.</summary>
    public enum Hub
    {
        /// <summary>A <see cref="RuntimeGameplayAbilitySystem"/>, a plain object.</summary>
        Runtime,

        /// <summary>A <see cref="GameplayAbilitySystem"/>, a component on a GameObject of its own.</summary>
        Mono,
    }

    /// <summary>
    /// One live PlayForge for one test (TW-26, Decision 66): a real ProcessControl and GameRoot brought up by the real
    /// Bootstrapper, stepped by Unity's player loop a frame at a time. It makes what a test needs, entities on either hub,
    /// from an identity asset or from attributes, and plays them as a game does: <see cref="Hit"/> applies an effect,
    /// <see cref="Cast"/> gives an ability and starts it, <see cref="Use"/> starts one the entity has, and
    /// <see cref="Hold"/> equips an item. Its <see cref="Clock"/> and <see cref="Rolls"/> make time and chance the test's.
    /// <para>
    /// It ends as a test asks, or as it's disposed (<c>await using</c>): every process it began, every object and asset it
    /// made, and the framework's static state, so tests run in any order. <see cref="Audit"/> first says what broke of
    /// its <see cref="Invariants"/>. <see cref="ForgeTest"/> makes one for each test, audits it and ends it. Step frames,
    /// and let ProcessControl step what runs: never call a process's update by hand.
    /// </para>
    /// </summary>
    public sealed class ForgeTestWorld : IAsyncDisposable
    {
        // Enough for async processes to unwind and for a frame's deferred work to land
        private const int SettleFrames = 3;

        /// <summary>The world a test is in, while one is.</summary>
        public static ForgeTestWorld Current { get; private set; }

        public ProcessControl Control { get; private set; }
        public GameRoot Root { get; private set; }

        /// <summary>Steps the world's frames.</summary>
        public FrameStepper Frames { get; }

        /// <summary>Builds assets in code, destroyed with the world.</summary>
        public TestAssets Make { get; } = new();

        /// <summary>What must hold after the test, which <see cref="Audit"/> checks.</summary>
        public Invariants Invariants { get; } = new();

        private readonly List<ProcessRelay> owned = new();
        private readonly List<GameObject> objects = new();
        private GameObject templates;
        private HashSet<int> baseline = new();

        private IForgeClock previousClock;
        private IForgeRandom previousRandom;
        private bool clockSet, randomSet, ended;

        internal ForgeTestWorld(float deltaTime = FrameStepper.DefaultDeltaTime)
        {
            Frames = new FrameStepper(deltaTime);
            Current = this;
        }

        /// <summary>
        /// Starts a world of one's own, as a test with a base class of its own makes one: live, at a frame's end. End it as
        /// the test ends, or dispose it.
        /// </summary>
        public static async UniTask<ForgeTestWorld> Start(float deltaTime = FrameStepper.DefaultDeltaTime)
        {
            var world = new ForgeTestWorld(deltaTime);
            try
            {
                world.Bootstrap();
            }
            catch
            {
                await world.End();
                throw;
            }
            await world.Frames.Align();
            return world;
        }

        internal void Bootstrap()
        {
            // Destroyed singletons aren't null to the Bootstrapper's `is null` checks (PC-26)
            ProcessControl.Instance = null;
            GameRoot.Instance = null;

            var rootTemplate = Template<GameRoot>("Game Root");
            rootTemplate.EntityData = Make.Identity("Game Root", Make.UnusedSet());
            rootTemplate.ProcessLifecycle = EProcessLifecycle.Synchronous;
            rootTemplate.ProcessTiming = EProcessStepTiming.UpdateAndLate;

            // Awake bootstraps, so the component is filled in before the object wakes
            var host = new GameObject("Bootstrapper");
            objects.Add(host);
            host.SetActive(false);
            var bootstrapper = host.AddComponent<Bootstrapper>();
            bootstrapper.GameRootPrefab = rootTemplate;
            bootstrapper.RegisterSets = new List<AttributeSet>();
            host.SetActive(true);

            Control = ProcessControl.Instance;
            Root = GameRoot.Instance;
            if (!Control) throw new AssertionException("The Bootstrapper brought up no ProcessControl");
            baseline = new HashSet<int>(Control.FetchActiveProcesses().Keys);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Processes
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Registers a process the world ends if the test doesn't.</summary>
        public ProcessRelay Register(AbstractRuntimeProcess process, ProcessDataPacket data = null)
        {
            // By its type: a refused process has no relay to read its name through
            if (!ProcessControl.Register(process, data ?? ProcessDataPacket.Default(), out var relay))
                throw new AssertionException($"ProcessControl refused a {process.GetType().Name}");

            owned.Add(relay);
            return relay;
        }

        /// <summary>Registers a process the world ends if the test doesn't.</summary>
        public ProcessRelay Register(AbstractMonoProcess process, ProcessDataPacket data = null)
        {
            if (!ProcessControl.Register(process, data ?? ProcessDataPacket.Default(), out var relay))
                throw new AssertionException($"ProcessControl refused {process.name}");

            owned.Add(relay);
            return relay;
        }

        /// <summary>Makes the world end a process that was registered some other way.</summary>
        public void Own(ProcessRelay relay) => owned.Add(relay);

        /// <summary>
        /// A stand-in for a prefab: an object outside any scene and not active, as a prefab is, so ProcessControl clones it
        /// as it registers it, the clone an ordinary active object in the scene.
        /// </summary>
        public T Template<T>(string name) where T : AbstractMonoProcess
        {
            if (!templates)
            {
                templates = new GameObject("Templates") { hideFlags = HideFlags.HideAndDontSave };
                templates.SetActive(false);
                objects.Add(templates);
            }

            var host = new GameObject(name);
            host.transform.SetParent(templates.transform, false);
            return host.AddComponent<T>();
        }

        /// <summary>Registers a template, or a prefab, and returns the clone ProcessControl made of it.</summary>
        public T Spawn<T>(T template, ProcessDataPacket data = null) where T : AbstractMonoProcess
        {
            var relay = Register(template, data);
            if (!relay.TryGetProcess<T>(out var clone)) throw new AssertionException($"Registering {template.name} made no {typeof(T).Name}");

            objects.Add(clone.gameObject);
            return clone;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Entities
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A registered runtime GAS of these attributes. An <paramref name="observed"/> entity is in the invariants' checks,
        /// which listen for its frame summaries, so a test that measures allocation turns it off.
        /// </summary>
        public RuntimeGameplayAbilitySystem RuntimeEntity(AttributeSet set = null, string name = "Entity", bool observed = true)
        {
            var gas = new RuntimeGameplayAbilitySystem(Make.RuntimeIdentity(name, set ?? Make.UnusedSet()));
            Register(gas);
            if (observed) Invariants.Observe(gas);
            return gas;
        }

        /// <summary>A registered MonoBehaviour GAS on a GameObject of its own, as <see cref="RuntimeEntity"/> makes one.</summary>
        public GameplayAbilitySystem MonoEntity(AttributeSet set = null, string name = "Entity", bool observed = true)
        {
            var gas = NewMonoEntity(set, name);
            Register(gas);
            if (observed) Invariants.Observe(gas);
            return gas;
        }

        /// <summary>A MonoBehaviour GAS as <see cref="MonoEntity"/> makes it, not yet registered or observed.</summary>
        public GameplayAbilitySystem NewMonoEntity(AttributeSet set = null, string name = "Entity")
        {
            var host = new GameObject(name);
            objects.Add(host);

            var gas = GameplayAbilitySystem.AddToGameObject(host, Make.Identity(name, set ?? Make.UnusedSet()));
            gas.ProcessLifecycle = EProcessLifecycle.Synchronous;
            gas.ProcessTiming = EProcessStepTiming.UpdateAndLate;
            return gas;
        }

        /// <summary>A registered GAS of these attributes on either hub.</summary>
        public IGameplayAbilitySystem Entity(Hub hub, AttributeSet set = null, string name = "Entity", bool observed = true) =>
            hub == Hub.Runtime ? RuntimeEntity(set, name, observed) : MonoEntity(set, name, observed);

        /// <summary>
        /// An entity made from an identity asset, a game's own or one <see cref="Make"/> built, registered on either hub,
        /// with its attribute set, starting abilities and items, and sides.
        /// </summary>
        public IGameplayAbilitySystem FromIdentity(Hub hub, EntityIdentity identity, bool observed = true)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            IGameplayAbilitySystem gas;
            if (hub == Hub.Runtime)
            {
                var runtime = new RuntimeGameplayAbilitySystem(identity);
                Register(runtime);
                gas = runtime;
            }
            else
            {
                // A Mono entity of the world's, its host the world's to clean up, with the identity in place of its own
                var mono = NewMonoEntity(name: identity.name);
                mono.EntityData = identity;
                Register(mono);
                gas = mono;
            }
            if (observed) Invariants.Observe(gas);
            return gas;
        }

        /// <summary>Includes an entity made elsewhere, as one from a scene, in the invariants' checks.</summary>
        public void Observe(IGameplayAbilitySystem gas) => Invariants.Observe(gas);

        // ═══════════════════════════════════════════════════════════════════════════
        // Time and chance
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Each frame takes <paramref name="step"/> seconds of the game's time until the world ends.</summary>
        public SteppingClock Clock(float step)
        {
            var clock = new SteppingClock(step);
            var previous = ForgeTime.Use(clock);
            if (!clockSet) previousClock = previous;
            clockSet = true;
            return clock;
        }

        /// <summary>Rolls come out as these, in turn and round again, until the world ends; the result counts them.</summary>
        public ScriptedRandom Rolls(params float[] rolls)
        {
            var scripted = new ScriptedRandom(rolls);
            var previous = ForgeRandom.Use(scripted);
            if (!randomSet) previousRandom = previous;
            randomSet = true;
            return scripted;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Playing
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Applies an effect from a source to a target, as a hit; whether it landed.</summary>
        public bool Hit(IGameplayAbilitySystem source, IGameplayAbilitySystem target, GameplayEffect effect)
        {
            var origin = IEffectOrigin.GenerateSourceDerivation(source);
            return target.ApplyGameplayEffect(target.GenerateEffectSpec(origin, effect));
        }

        /// <summary>Gives the caster an ability at a level, and asks it to start at a target, if any; whether it started.</summary>
        public bool Cast(IGameplayAbilitySystem caster, Ability ability, IGameplayAbilitySystem target = null, int level = 1)
        {
            var abilities = caster.GetAbilitySystem();
            if (!abilities.GiveAbility(ability, level, out int index)) throw new AssertionException($"{ability.name} wasn't given");
            return Cast(caster, index, target);
        }

        /// <summary>Asks the ability in a slot to start at a target, if any; whether it started.</summary>
        public bool Cast(IGameplayAbilitySystem caster, int index, IGameplayAbilitySystem target = null)
        {
            var abilities = caster.GetAbilitySystem();
            var request = ProcessDataPacket.Default();
            if (target != null) request.SetPrimary(Tags.TARGET_REAL, (ITarget)target);
            return abilities.TryActivateAbility(abilities.CreateActivationRequest(index, request));
        }

        /// <summary>Asks an ability the entity has already, as an item's, to start at a target, if any; whether it started.</summary>
        public bool Use(IGameplayAbilitySystem caster, Ability ability, IGameplayAbilitySystem target = null)
        {
            var data = ProcessDataPacket.Default();
            if (target != null) data.SetPrimary(Tags.TARGET_REAL, (ITarget)target);
            return caster.GetAbilitySystem().TryActivateAbility(ability, data);
        }

        /// <summary>Gives and equips an item, with as many stacks as asked.</summary>
        public void Hold(IGameplayAbilitySystem holder, Item item, int stacks = 1)
        {
            var items = holder.GetItemSystem();
            if (!items.GiveAndEquipItem(item, out _)) throw new AssertionException($"{item.name} wasn't held");
            if (stacks > 1 && items.GiveItem(item, stacks - 1) != stacks - 1) throw new AssertionException($"{item.name} took fewer than {stacks} stacks");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Reading
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>An attribute's current value, or NaN where the entity hasn't the attribute.</summary>
        public float Value(IGameplayAbilitySystem gas, ForgeAttribute attribute) =>
            gas.TryGetAttributeValue(attribute, out var value) ? value.CurrentValue : float.NaN;

        /// <summary>An attribute's base value, or NaN where the entity hasn't the attribute.</summary>
        public float Base(IGameplayAbilitySystem gas, ForgeAttribute attribute) =>
            gas.TryGetAttributeValue(attribute, out var value) ? value.BaseValue : float.NaN;

        /// <summary>Whether an effect is on the entity.</summary>
        public bool Has(IGameplayAbilitySystem gas, GameplayEffect effect) => gas.TryGetEffectContainer(effect, out _);

        /// <summary>An effect's stacks on the entity: 1 for one that doesn't stack, 0 for one that isn't on it.</summary>
        public int Stacks(IGameplayAbilitySystem gas, GameplayEffect effect) =>
            gas.TryGetEffectContainer(effect, out var container) ? (container as AbstractStackingEffectContainer)?.Stacks ?? 1 : 0;

        /// <summary>
        /// Whether the entity carries a tag, or one under it. A list of tags is checked here rather than with NUnit's
        /// Does.Contain, which compares a tag as the text it converts to.
        /// </summary>
        public bool HasTag(IGameplayAbilitySystem gas, Tag tag) => gas.GetTagCache().GetWeight(tag) > 0;

        // ═══════════════════════════════════════════════════════════════════════════
        // Ending
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lets the frame's work finish, ends each process the world began and the test left running, and returns every
        /// invariant that broke: attribute parts that don't sum, dealt that isn't received, and processes still running
        /// that the world didn't begin.
        /// </summary>
        public async UniTask<List<string>> Audit()
        {
            await Frames.Step(SettleFrames);

            var violations = Invariants.CheckAttributes();

            if (Control)
            {
                foreach (var relay in owned)
                {
                    if (Control.IsRegistered(relay.CacheIndex)) Control.TerminateImmediate(relay.CacheIndex, false);
                }

                await Frames.Step(SettleFrames);
                violations.AddRange(Invariants.CheckBlocks(Control, baseline));
            }

            return violations;
        }

        /// <summary>
        /// Takes everything down, whatever state the test left it in: every process, object and asset the world made, and
        /// PlayForge's static state, and gives back the clock and the rolls. Forcing every process to end at once runs
        /// each way one ends, and what those report says nothing of the test, so it's let pass. Ending twice does nothing.
        /// </summary>
        public async UniTask End()
        {
            if (ended) return;
            ended = true;
            IgnoreFailingLogs(true);

            try
            {
                if (Control) Control.TerminateAllImmediately();

                // Async processes unwind over later frames and need ProcessControl while they do
                await Frames.Step(SettleFrames);

                foreach (var host in objects)
                {
                    if (host) Object.Destroy(host);
                }

                if (Root) Object.Destroy(Root.gameObject);
                if (Control) Object.Destroy(Control.gameObject);
                Make.DestroyAll();

                await Frames.Step();
            }
            finally
            {
                ProcessControl.Instance = null;
                GameRoot.Instance = null;
                TagRegistry.Reset();
                AttributeRegistry.Reset();
                if (clockSet) ForgeTime.Use(previousClock);
                if (randomSet) ForgeRandom.Use(previousRandom);
                clockSet = randomSet = false;

                Frames.Release();
                if (Current == this) Current = null;
                IgnoreFailingLogs(false);
            }
        }

        /// <summary>Ends the world, as <c>await using</c> does.</summary>
        public async ValueTask DisposeAsync() => await End();

        private static void IgnoreFailingLogs(bool ignore)
        {
            try
            {
                LogAssert.ignoreFailingMessages = ignore;
            }
            catch (InvalidOperationException)
            {
                // A test that failed has lost its log scope by its teardown, and with it anything to ignore
            }
        }
    }
}
