using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What makes an entity a GAS, whichever hub carries it: its subsystems and tag cache, its level, Local Data and
    /// affiliation, its end of frame, and its death. <see cref="GameplayAbilitySystem"/>, a MonoBehaviour, and
    /// <see cref="RuntimeGameplayAbilitySystem"/>, a plain process, each own one and forward to it. What stays with a hub
    /// is how it is built, where its entity is, and how it goes away.
    ///
    /// Each hub kept its own copy of all this, about 400 lines, and the copies drifted: only the Mono hub applied the
    /// entity's affiliation as tags, and a runtime GAS made from an Entity asset lost the asset's Local Data (GAS-S1).
    /// </summary>
    internal sealed class GASCore
    {
        private readonly IGameplayAbilitySystem owner;

        /// <summary>The hub this core is, which a save reads it through (FEAT-20).</summary>
        internal IGameplayAbilitySystem Owner => owner;
        private readonly LocalDataStructure localData = new();
        private List<Tag> affiliation;
        private bool pendingDestruction;

        // A staying entity's death, run at its frame's end, and its body while it has one (TW-4)
        private bool pendingDeath;
        private ProcessRelay bodyRelay;
        private AbstractMonoProcess body;

        public AttributeSystemComponent AttributeSystem { get; }
        public AbilitySystemComponent AbilitySystem { get; }
        public ItemSystemComponent ItemSystem { get; }
        public EffectSystemComponent EffectSystem { get; }
        public SystemLevelsComponent LevelSystem { get; }
        public TagCache TagCache { get; }
        public GameplayAbilitySystemCallbacks Callbacks { get; }

        /// <summary>The modifiers the entity holds now: its own, its items', its abilities' and its effects' (TW-7).</summary>
        public EffectModifierSet EffectModifiers { get; } = new();

        /// <summary>The auras the entity runs, its items', abilities', effects' and its own (TW-10).</summary>
        public AuraSet Auras { get; } = new();

        /// <summary>The side changes it's under, which go over its own sides while they last (TW-18).</summary>
        public SideSet Sides { get; } = new();

        /// <summary>Its combat log, which writes what the project subscribes to (TW-22).</summary>
        public CombatLog CombatLog { get; } = new();

        /// <summary>The crowd control rules its abilities follow: its identity's, or else the project's (TW-16).</summary>
        public CrowdControlRules CrowdControl => Identity?.CrowdControl is { } own && own ? own : ProjectCrowdControl;

        // The current project's, where it names any (TW-29)
        private static CrowdControlRules ProjectCrowdControl =>
            ForgeProject.Current is { } project && project.Settings?.CrowdControl is { } rules && rules ? rules : null;

        /// <summary>The entity's settings as this GAS uses them, or null for a hub given none.</summary>
        public RuntimeEntityIdentity Identity { get; private set; }

        // What its identity's components keep for it (FN-14)
        private readonly ComponentStates componentStates = new();

        // Its identity's components, each hearing a moment of it (FN-14)
        private void HearComponents(System.Action<AbstractEntityComponent, EntityComponentContext> moment)
        {
            var components = Identity?.Components;
            if (components == null) return;
            var ctx = new EntityComponentContext(owner, componentStates);
            foreach (var component in components)
                if (component != null) moment(component, ctx);
        }

        /// <summary>The entity's own time, its time scale times the game's (FEAT-7).</summary>
        public EntityClock Clock { get; } = new();

        public ActionQueue ActionQueue { get; private set; }
        public FrameSummary FrameSummary { get; private set; }
        public AnalysisWorkerCache AnalysisCache { get; private set; }
        public LocalDataStructure LocalData => localData;
        public bool IsDead { get; private set; }

        /// <param name="owner">The hub, which the subsystems, workers and callbacks see as this GAS.</param>
        /// <param name="callbacks">The hub's, made with it so a caller can subscribe before it initializes (PF-013).</param>
        public GASCore(IGameplayAbilitySystem owner, GameplayAbilitySystemCallbacks callbacks)
        {
            this.owner = owner;
            Callbacks = callbacks;

            AttributeSystem = new AttributeSystemComponent(owner);
            AbilitySystem = new AbilitySystemComponent(owner);
            ItemSystem = new ItemSystemComponent(owner);
            EffectSystem = new EffectSystemComponent(owner);
            LevelSystem = new SystemLevelsComponent();
            TagCache = new TagCache(owner);

            // A running ability's While active group is checked again once the tags change (FEAT-5)
            TagCache.TagChanged += AbilitySystem.HearTagChange;

            // An effect's side change goes over its sides while it lasts (TW-18)
            Sides.Changed = SidesChanged;

            // An effect that reads a level reads it again as it changes (GAS-58)
            LevelSystem.Callbacks.OnLevelChanged += LevelChanged;
            LevelSystem.Callbacks.OnMaxLevelChanged += LevelChanged;
        }

        /// <summary>
        /// Builds the GAS from <paramref name="identity"/>: its rules, end of frame, workers and level, its starting
        /// abilities, attributes and items, then its tags, then the abilities that activate at once. A hub given no
        /// identity keeps its empty subsystems.
        /// </summary>
        /// <param name="registered">The sides the hub's registration data gives, which replace the identity's.</param>
        public void Initialize(RuntimeEntityIdentity identity, List<Tag> registered = null)
        {
            Identity = identity;
            localData.Init(identity);

            // Live, for an area search on either hub, until it ends (GAS-75)
            ForgeEntities.Add(owner);

            if (identity is null) return;

            // This entity's sides: the registration data's, or else a copy of the identity's. They were written onto the
            // identity, which every entity of this kind shares, so one turncoat moved them all (GAS-47)
            affiliation = registered ?? (identity.Affiliation != null ? new List<Tag>(identity.Affiliation) : null);

            AbilitySystem.Setup(identity.ActivationPolicy, identity.AllowDuplicateAbilities, identity.MaxAbilitiesOperation);
            AttributeSystem.Setup(identity.AttributeSet);
            ItemSystem.Setup(identity.AllowDuplicateItems, identity.AllowDuplicateEquippedItems, identity.MaxItemsOperation, identity.MaxEquippedItemsOperation);

            InitializeEndOfFrame();

            identity.InitWorkers(owner);
            if (identity.AttributeSet) identity.AttributeSet.InitWorkers(owner);

            LevelSystem.Register(identity.AssetTag, new IntValuePairClamped(identity.GetStartingLevel(), identity.GetMaxLevel()));

            AbilitySystem.Initialize(identity.StartingAbilities);
            AttributeSystem.Initialize();
            FollowTimeScale();
            ItemSystem.Initialize(identity.StartingItems);

            CompileGrantedTags();

            // Once attributes, items and tags exist, so the rules can check them (GAS-49)
            AbilitySystem.ActivateImmediateAbilities();

            // Its body, where it is (TW-4)
            SpawnBody();

            // Its components hear it register, its systems set up (FN-14)
            HearComponents((component, ctx) => component.Register(ctx));

            Callbacks?.SystemInitialized();
        }

        private void InitializeEndOfFrame()
        {
            ActionQueue = new ActionQueue();
            FrameSummary = new FrameSummary();
            AnalysisCache = new AnalysisWorkerCache();

            ActionQueue.OnActionQueued += action => Callbacks.ActionQueued(action);
            ActionQueue.OnActionExecuted += action => Callbacks.ActionExecuted(action);
            ActionQueue.OnActionInvalidated += action =>
            {
                FrameSummary.RecordInvalidatedAction(action);
                Callbacks.ActionInvalidated(action);
            };

            TagCache.SetDeferredContext(owner, ActionQueue, FrameSummary);
            AnalysisCache.SetDeferredContext(owner, ActionQueue, FrameSummary);

            AttributeSystem.SetDeferredContext(owner, ActionQueue, FrameSummary);
            AbilitySystem.SetDeferredContext(owner, ActionQueue, FrameSummary);
            ItemSystem.SetDeferredContext(owner, ActionQueue, FrameSummary);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Frame
        // ═══════════════════════════════════════════════════════════════════════════

        private void LevelChanged(LevelCallbackStatus status) => EffectSystem.RefreshEffectsLevelledBy(status.LevelKey);

        /// <summary>
        /// Whether the entity is part way through its step or its end of frame, whose loops a load would pull the effects
        /// and actions out from under. A load waits for the step to end (FEAT-9).
        /// </summary>
        internal bool Stepping { get; private set; }

        public void Tick()
        {
            using var sample = ForgeProfiler.GASStep.Auto();
            Stepping = true;
            try
            {
                FollowTimeScale();
                Clock.Step();
                EffectSystem.Tick();

                // Heard as the tags change; checked here too, after the effects, for a change heard while a check ran (FEAT-5)
                AbilitySystem.CheckWhileActive();

                // The input buffer's windows count down by the entity's own time (FEAT-6)
                AbilitySystem.StepBuffer(Clock.DeltaTime);

                // Its auras check who's near by its own time too (TW-10)
                Auras.Step(Clock.DeltaTime);

                // And its barriers decay by it (TW-12)
                AttributeSystem.StepBarriers(Clock.DeltaTime);

                // Its shelf, for the Effects Active counter (FEAT-18)
                CountEffects();

                // Tag workers tick once a frame, at its end, once their activations are settled. Ticked here as well,
                // they ran at twice their rate (GAS-19)
            }
            finally
            {
                Stepping = false;
            }
        }

        /// <summary>
        /// The frame's deferred work: the queued actions, then the tag workers' and the analysis workers', each with what
        /// it queued in turn, then the frame's summary. True once the entity has died, for its hub to end it.
        /// </summary>
        // The entity's time runs at its time-scale attribute's current value, read as each step begins, where its identity
        // names one, so an effect on that attribute slows or hastes it. Only its game's code could set it (FEAT-22)
        private void FollowTimeScale()
        {
            var attribute = Identity?.TimeScaleAttribute;
            if (attribute != null && AttributeSystem.TryGetValue(attribute, out var value)) Clock.TimeScale = value.CurrentValue;
        }

        public bool EndOfFrame()
        {
            if (ActionQueue == null) return false;

            using var sample = ForgeProfiler.EndOfFrame.Auto();
            Stepping = true;
            try
            {
                return RunEndOfFrame();
            }
            finally
            {
                Stepping = false;
            }
        }

        private bool RunEndOfFrame()
        {
            int totalExecuted = ActionQueue.ProcessAll(owner);

            TagCache.EvaluateTagWorkers();

            // Tag worker activate/resolve actions
            totalExecuted += ActionQueue.ProcessAll(owner);

            TagCache.TickTagWorkers();

            // Actions queued by ticks
            totalExecuted += ActionQueue.ProcessAll(owner);

            AnalysisCache.Analyze(owner);

            // The attribute playing the project's Health role, where its own workers watch for no death (TW-28, TW-29)
            WatchProjectHealth();

            // Its lasting effects' Ongoing and Removal conditions, which no tag change asks for (TW-19)
            EffectSystem.CheckConditions();

            // Actions queued by analysis workers
            totalExecuted += ActionQueue.ProcessAll(owner);

            // The snapshot copies ten lists, so it's built only for someone listening (PF-006), and into lists it keeps:
            // a new one each frame allocated them all again (GAS-P10)
            if (Callbacks != null && Callbacks.HasFrameCompleteListeners) Callbacks.FrameComplete(FrameSummary.TakeSnapshot(totalExecuted));

            FrameSummary.Clear();

            // A staying entity dies here, once the frame's work is done, and stays; one with a body follows it (TW-4)
            if (pendingDeath)
            {
                pendingDeath = false;
                Die();
            }
            FollowBody();

            if (!pendingDestruction) return false;

            Callbacks?.SystemDisabled();
            return true;
        }

        /// <summary>
        /// The entity's process is ending. Its ability system takes no more activations, so none starts as the abilities
        /// still running end with the process, nor after it (GAS-52).
        /// </summary>
        public void WhenTerminate()
        {
            // Its components hear it end, before what it holds goes (FN-14)
            HearComponents((component, ctx) => component.End(ctx));

            // Its auras' effects come off each they held them on (TW-10)
            Auras.Clear();

            // What its effects' lasting transfers hold, given back, as they go with it (TW-20)
            EffectSystem.GiveBackTransfers();
            Sides.Clear();
            AbilitySystem.ShutDown();
            ForgeEntities.Remove(owner);
            EndBody();

            // Its effects leave the Effects Active counter with it (FEAT-18)
            ForgeCounters.EffectsActive.Value -= countedEffects;
            countedEffects = 0;
        }

        // The effects on its shelf as the Effects Active counter last had them, which each step brings up to date
        private int countedEffects;

        private void CountEffects()
        {
            int now = EffectSystem.Shelf.Count;
            if (now == countedEffects) return;
            ForgeCounters.EffectsActive.Value += now - countedEffects;
            countedEffects = now;
        }

        public void QueueAction(IRootAction action) => ActionQueue?.Enqueue(action);

        public void QueueActions(IEnumerable<IRootAction> actions) => ActionQueue?.EnqueueRange(actions);

        // ═══════════════════════════════════════════════════════════════════════════
        // Death and impacts
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Marks the entity dead. Listeners hear at once, and the frame completes as usual; its end hands the death to
        /// the hub, which ends the entity as its kind ends.
        /// </summary>
        public void MarkDead()
        {
            if (IsDead) return;

            IsDead = true;
            Callbacks?.EntityDied(owner);
            FrameSummary?.RecordDeath(owner);

            // Died to itself, with its killer; Kill to the killer (TW-5)
            var killer = Killer();

            // The blow that killed it, in its combat log and its killer's (TW-22)
            if (killer != null)
            {
                CombatLog.MarkKilling(ECombatLogKind.Taken, killer);
                (killer as IGameplayAbilitySystem)?.GetCombatLog()?.MarkKilling(ECombatLogKind.Dealt, owner);
            }
            Send(owner, Tags.DIED, EventData(killer, owner));
            if (killer is IGameplayAbilitySystem slayer) Send(slayer, Tags.KILL, EventData(killer, owner));

            // Its components hear it die, then its effects' (FN-14)
            HearComponents((component, ctx) => component.Death(ctx));
            EffectSystem.HearDeath();

            // One that stays dies at its frame's end and stays; any other ends there, as it always did (TW-4)
            if (EntityDeath.Stays(Identity?.Death)) pendingDeath = true;
            else pendingDestruction = true;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Staying dead, reviving, and the body (TW-4)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The body it follows, while it has one: the MonoBehaviour process its identity's death names.</summary>
        public AbstractMonoProcess Body => body ? body : null;

        /// <summary>
        /// Brings a staying entity back: alive, its analysis workers ready to see its next death, its revive effects taken,
        /// its body spawned at <paramref name="at"/>, or where it was. False where it isn't dead, or doesn't stay.
        /// </summary>
        public bool Revive(UnityEngine.Vector3? at = null)
        {
            if (!IsDead || !EntityDeath.Stays(Identity?.Death)) return false;
            IsDead = false;
            pendingDeath = false;
            foreach (var worker in AnalysisCache.Workers) worker?.Rearm(owner);
            if (at.HasValue && owner.GetTargetingPacket() is { } packet) packet.position = at.Value;
            ApplyEach(Identity.Death.OnRevive);
            SpawnBody();
            Callbacks?.EntityRevived(owner);
            Send(owner, Tags.REVIVED, EventData(null, owner));

            // Its components hear it revive (FN-14)
            HearComponents((component, ctx) => component.Revive(ctx));
            return true;
        }

        // A staying entity's death: the effects it ends, its body gone, the effects it takes, as a respawn timer
        private void Die()
        {
            var death = Identity?.Death;
            if (death == null) return;
            if (death.EndsEffectsTagged is { Count: > 0 }) EffectSystem.RemoveEffectsTagged(death.EndsEffectsTagged);
            EndBody();
            ApplyEach(death.OnDeath);
        }

        // Each effect, from the entity to itself
        private void ApplyEach(List<GameplayEffect> effects)
        {
            if (effects == null || effects.Count == 0) return;
            var origin = IEffectOrigin.GenerateSourceDerivation(owner);
            foreach (var effect in effects)
                if (effect != null) owner.ApplyGameplayEffect(owner.GenerateEffectSpec(origin, effect));
        }

        // Its body, from its template, where the entity is
        private void SpawnBody()
        {
            var template = Identity?.Death?.Body;
            if (template == null || Body != null) return;
            var data = ProcessDataPacket.Default();
            if (owner.GetTargetingPacket() is { } packet) data.SetPrimary(Tags.POSITION, packet.position);
            var made = ProcessControl.Register(template).HandledBy(owner).With(data).In(ProcessWorld.Of(owner)).Run();
            bodyRelay = made.Relay;
            body = bodyRelay != null && bodyRelay.TryGetProcess<AbstractMonoProcess>(out var spawned) ? spawned : null;
        }

        private void EndBody()
        {
            if (bodyRelay != null && Body != null) bodyRelay.Terminate();
            bodyRelay = null;
            body = null;
        }

        // A runtime entity is where its body is. A MonoBehaviour entity has a place of its own
        private void FollowBody()
        {
            if (owner is UnityEngine.Object || Body == null) return;
            if (owner.GetTargetingPacket() is { } packet) packet.position = body.transform.position;
        }

        public void RecordImpactDealt(ImpactData impact)
        {
            FrameSummary?.RecordImpactDealt(impact);
            Callbacks?.ImpactDealt(impact);
            // In its combat log, where the project writes the hits it deals (TW-22)
            CombatLog.WriteDealt(impact, owner);
            SendImpactEvent(Tags.IMPACT_DEALT, impact, owner, impact.Target);
        }

        public void RecordImpactReceived(ImpactData impact)
        {
            ForgeCounters.Impacts.Value++;
            FrameSummary?.RecordImpactReceived(impact);
            Callbacks?.ImpactReceived(impact);
            // In its combat log, where the project writes the hits it takes or its other changes (TW-22)
            CombatLog.WriteReceived(impact, owner);
            SendImpactEvent(Tags.IMPACT_TAKEN, impact, impact.SourcedModifier.Source, owner);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The core's gameplay events (TW-5)
        // ═══════════════════════════════════════════════════════════════════════════

        // An impact between two entities, as an event to this one, where an ability listens: a system's own impact, and
        // one an entity deals itself, send none
        private void SendImpactEvent(Tag tag, ImpactData impact, ITarget dealer, ITarget subject)
        {
            if (!impact.SourcedModifier.ImpactIsWorkable || dealer == null || ReferenceEquals(dealer, subject)) return;
            if (!AbilitySystem.ListensFor(tag)) return;
            var data = ProcessDataPacket.Default();
            data.SetPrimary(Tags.SOURCE, dealer);
            if (subject != null) data.SetPrimary(Tags.EVENT_SUBJECT, subject);
            data.SetPrimary(Tags.EVENT_IMPACT, impact);
            Send(owner, tag, data);
        }

        // An event to an entity where an ability of its listens, sent as its frame's actions run: an ability set off
        // mid-commit would change what's committing
        private static void Send(IGameplayAbilitySystem to, Tag tag, ProcessDataPacket data)
        {
            var abilities = to?.GetAbilitySystem();
            if (abilities == null || !abilities.ListensFor(tag)) return;
            var queue = to.GetActionQueue();
            if (queue != null) queue.Enqueue(new LambdaAction(_ => abilities.SendGameplayEvent(tag, data), $"Send {tag}"));
            else abilities.SendGameplayEvent(tag, data);
        }

        private static ProcessDataPacket EventData(ITarget source, ITarget subject)
        {
            var data = ProcessDataPacket.Default();
            if (source != null) data.SetPrimary(Tags.SOURCE, source);
            if (subject != null) data.SetPrimary(Tags.EVENT_SUBJECT, subject);
            return data;
        }

        // Who killed it: the source of the last workable loss it took this frame from another; of the attribute playing
        // the Health role where the project has one, so a loss after the blow, to another attribute, doesn't take the kill
        // (TW-28, TW-29)
        private ISource Killer()
        {
            var received = FrameSummary?.ImpactsReceived;
            if (received == null) return null;
            var health = ForgeRoles.AttributeFor(Tags.ROLE_HEALTH);
            if (health != null && LastLoss(received, health) is { } slayer) return slayer;
            return LastLoss(received, null);
        }

        // The source of the last workable loss from another among the impacts, to the attribute, or to any for null
        private ISource LastLoss(List<ImpactData> received, IAttribute to)
        {
            for (int i = received.Count - 1; i >= 0; i--)
            {
                var impact = received[i];
                var source = impact.SourcedModifier.Source;
                if (!impact.SourcedModifier.ImpactIsWorkable || source == null || ReferenceEquals(source, owner)) continue;
                if (to != null && !to.Equals(impact.Attribute)) continue;
                if (impact.RealImpact.CurrentValue < 0f || impact.RealImpact.BaseValue < 0f) return source;
            }
            return null;
        }

        // The Health role's attribute (TW-28, TW-29): an entity whose analysis workers watch for no death of their own, and whose Death
        // doesn't say otherwise, dies as its Health reaches 0. One without the attribute doesn't
        private void WatchProjectHealth()
        {
            if (IsDead || pendingDestruction) return;
            var health = ForgeRoles.AttributeFor(Tags.ROLE_HEALTH);
            if (health == null) return;
            if (Identity?.Death is { DiesAtZeroHealth: false }) return;
            var workers = AnalysisCache.Workers;
            for (int i = 0; i < workers.Count; i++)
                if (workers[i] is DeathWatcherAnalysisWorker) return;
            if (!owner.TryGetAttributeValue(health, out var value) || value.CurrentValue > 0f) return;
            QueueAction(new DeathAction(owner));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tags and affiliation
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Its sides: those the side change it's under gives, or else its own (TW-18).</summary>
        public List<Tag> GetAffiliation() => Sides.Current ?? OwnAffiliation;

        /// <summary>Its own sides: those its registration or code set, or else its identity's; a side change goes over them.</summary>
        public List<Tag> OwnAffiliation => affiliation ?? Identity?.Affiliation;

        /// <summary>
        /// Sets its own sides, as its registration data does or the game as it runs (TW-18), in place of its identity's; its
        /// tags follow. A side change it's under still goes over them.
        /// </summary>
        public void SetAffiliation(List<Tag> sides)
        {
            affiliation = sides;
            if (Identity != null) CompileGrantedTags();
        }

        // Its sides where a side change came or went: its tags follow, and what runs is interrupted where the change says
        // so; where its sides are as they were, as under a change of higher priority, neither (TW-18)
        private void SidesChanged(List<Tag> before, List<Tag> after, bool interrupts)
        {
            if (SameSides(before ?? OwnAffiliation, after ?? OwnAffiliation)) return;
            if (Identity != null) CompileGrantedTags();
            if (interrupts) AbilitySystem.InjectAll(InterruptSequenceInjection.Instance);
        }

        private static bool SameSides(List<Tag> a, List<Tag> b)
        {
            if (ReferenceEquals(a, b)) return true;
            int countA = a?.Count ?? 0, countB = b?.Count ?? 0;
            if (countA != countB) return false;
            for (int i = 0; i < countA; i++)
                if (!b.Contains(a[i])) return false;
            return true;
        }

        // A rebuild of the tags from their sources, which tells, as it ends, only what it changed (FEAT-3)
        public void CompileGrantedTags()
        {
            using var sample = ForgeProfiler.TagCompile.Auto();
            ForgeCounters.TagCompiles.Value++;
            TagCache.BeginRebuild();
            try
            {
                GrantedTagCompiler.Compile(TagCache, Identity.AttributeSet, EffectSystem, AbilitySystem, ItemSystem);

                // The identity's own, as EntityIdentity.GetGrantedTags yields them. The runtime hub left out the
                // affiliation (GAS-S1)
                TagCache.AddTag(Identity.AssetTag);
                GrantedTagCompiler.AddAll(TagCache, GetAffiliation());
                GrantedTagCompiler.AddAll(TagCache, Identity.GrantedTags);
                GrantedTagCompiler.AddAll(TagCache, looseTags);
            }
            finally
            {
                TagCache.EndRebuild();
            }
        }

        // Tags granted to the entity directly, by its game's code or the Debugger, with no effect, ability or item
        // behind them, as Unreal's loose gameplay tags are. Each grant counts, and the compile includes them, so they last
        // until they're taken back; a tag added straight to the cache went at the next compile (FEAT-16)
        private readonly List<Tag> looseTags = new();

        public IReadOnlyList<Tag> LooseTags => looseTags;

        public void AddLooseTag(Tag tag)
        {
            looseTags.Add(tag);
            CompileGrantedTags();
        }

        public bool RemoveLooseTag(Tag tag)
        {
            if (!looseTags.Remove(tag)) return false;
            CompileGrantedTags();
            return true;
        }

        /// <summary>The loose tags a save left, in place of these, which the restore's one compile counts (FEAT-9).</summary>
        internal void RestoreLooseTags(IEnumerable<Tag> tags)
        {
            looseTags.Clear();
            looseTags.AddRange(tags);
        }

        public bool QueryTags(TagQuery query)
        {
            // The cache, which has the weights. The list counted each tag once, however often it was granted (GAS-16)
            return query.Validate(TagCache);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Attributes, effects and levels
        // ═══════════════════════════════════════════════════════════════════════════

        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents)
        {
            AttributeSystem.ModifyAttribute(attribute, sourcedModifiedValue, runEvents);
            return true;
        }

        /// <summary>Generates a gameplay effect spec with this GAS as the target.</summary>
        /// <param name="origin">The origination of the effect (typically entity, ability, or item)</param>
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect effect) => effect.Generate(origin, owner);

        /// <summary>Applies a gameplay effect with respect to an origin (typically entity, ability, or item).</summary>
        public bool ApplyGameplayEffect(IEffectOrigin origin, GameplayEffect effect)
            => EffectSystem.ApplyGameplayEffect(GenerateEffectSpec(origin, effect));

        public IntValuePairClamped GetLevel() => LevelSystem.GetLevel(Identity.AssetTag);

        public float GetRelativeLevel() => ForgeHelper.RelativeOffsetValue(GetLevel());
    }
}
