using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runtime (non-MonoBehaviour) version of GameplayAbilitySystem.
    /// Instantiated at runtime as a plain class. Derives from LazyRuntimeProcess
    /// and implements the same IGameplayAbilitySystem interface as the MonoBehaviour version.
    /// What makes it a GAS lives in <see cref="GASCore"/>, which it builds in its constructor and forwards to (GAS-S1).
    ///
    /// Usage:
    ///   var gas = new RuntimeGameplayAbilitySystem(entityData);
    ///   var relay = ProcessControl.Register(gas).With(dataPacket).Run().Relay;
    /// </summary>
    public partial class RuntimeGameplayAbilitySystem : LazyRuntimeProcess, IGameplayAbilitySystem
    {
        public RuntimeEntityIdentity EntityData;

        private readonly GASCore core;

        // What a save reads and writes past the hub's public face (FEAT-9)
        internal GASCore Core => core;

        // Subsystems
        public AttributeSystemComponent AttributeSystem => core.AttributeSystem;
        public AbilitySystemComponent AbilitySystem => core.AbilitySystem;
        public ItemSystemComponent ItemSystem => core.ItemSystem;
        public EffectSystemComponent EffectSystem => core.EffectSystem;

        public SystemLevelsComponent LevelSystem => core.LevelSystem;

        // Tags
        protected TagCache TagCache => core.TagCache;

        // Transform (runtime entities have no Transform component)
        private AbstractTargetingPacket _targetingPacket;

        /// <summary>
        /// Callbacks for GAS-level events (frame completion, action queue, effects, etc.). Created with the
        /// GAS and kept by initialization (PF-013). A runtime GAS initializes inside its constructor, so
        /// <c>OnSystemInitialized</c> has fired before anyone can subscribe to it.
        /// </summary>
        public GameplayAbilitySystemCallbacks Callbacks { get; private set; } = new();

        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a runtime GAS entity with optional transform data.
        /// </summary>
        /// <param name="data">The entity identity defining this entity's configuration.</param>
        /// <param name="targetingPacket">Optional transform data. Defaults to origin if null.</param>
        public RuntimeGameplayAbilitySystem(
            EntityIdentity data,
            AbstractTargetingPacket targetingPacket = null)
            : this(data ? new RuntimeEntityIdentity(data) : null, targetingPacket)
        {
        }

        /// <summary>
        /// Creates a runtime GAS entity with optional transform data.
        /// </summary>
        /// <param name="data">The entity identity defining this entity's configuration.</param>
        /// <param name="targetingPacket">Optional transform data. Defaults to origin if null.</param>
        public RuntimeGameplayAbilitySystem(
            RuntimeEntityIdentity data,
            AbstractTargetingPacket targetingPacket = null)
            : base(
                data?.Name ?? "RuntimeGAS",
                EProcessStepPriorityMethod.First,
                0,
                // Update ticks effects and tag workers; LateUpdate runs the end of frame (GAS-1)
                EProcessStepTiming.UpdateAndLate,
                EProcessLifecycle.SelfTerminating)
        {
            _targetingPacket = targetingPacket
                               ?? new StaticTargetingPacket(Vector3.zero, Quaternion.identity, Vector3.one);
            // The packet points back at its entity, as the Mono GAS's does, so what targeting finds can be resolved
            // to it (GAS-46)
            _targetingPacket.target ??= this;

            EntityData = data;
            core = new GASCore(this, Callbacks);
            core.Initialize(data);
        }

        /// <summary>
        /// Creates a runtime GAS entity with explicit position/rotation/scale.
        /// </summary>
        public RuntimeGameplayAbilitySystem(
            EntityIdentity data,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
            : this(data, new StaticTargetingPacket(position, rotation, scale))
        {
        }

        protected RuntimeGameplayAbilitySystem()
        {
            core = new GASCore(this, Callbacks);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PROCESS PARAMETERS
        // ═══════════════════════════════════════════════════════════════════════════

        public override void WhenInitialize()
        {
            base.WhenInitialize();

            // The registration data's sides, on this entity alone: two entities can share one runtime identity (GAS-47)
            if (regData.TryGet(Tags.AFFILIATION, EDataTarget.Primary, out List<Tag> registered)) core.SetAffiliation(registered);

            // An entity for the Runtime Entities counter as it runs (FEAT-18)
            if (!counted)
            {
                ForgeCounters.RuntimeEntities.Value++;
                counted = true;
            }
        }

        // Whether the Runtime Entities counter has it
        private bool counted;

        public override void WhenUpdate() => core.Tick();

        public override void WhenLateUpdate() => EndOfFrame();

        // Nothing starts on it from now on, and the base ends what it handles, its abilities among them. They ran on
        // after it ended (GAS-52)
        public override void WhenTerminate()
        {
            if (counted)
            {
                ForgeCounters.RuntimeEntities.Value--;
                counted = false;
            }
            core.WhenTerminate();
            base.WhenTerminate();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SOURCE / TARGET PARAMETERS
        // ═══════════════════════════════════════════════════════════════════════════

        public bool FindLevelSystem(out SystemLevelsComponent lvlSystem)
        {
            lvlSystem = LevelSystem;
            return lvlSystem is not null;
        }
        public bool FindItemSystem(out ItemSystemComponent itemSystem)
        {
            itemSystem = ItemSystem;
            return itemSystem is not null;
        }
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem)
        {
            attrSystem = AttributeSystem;
            return attrSystem is not null;
        }
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem)
        {
            abilSystem = AbilitySystem;
            return abilSystem is not null;
        }
        public bool TryGetAttributeValue(IAttribute attribute, out AttributeValue value)
            => core.AttributeSystem.TryGetValue(attribute, out value);
        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
            => core.TryModifyAttribute(attribute, sourcedModifiedValue, runEvents);

        /// <summary>
        /// Returns the placeholder transform packet for this runtime entity.
        /// Unlike the MonoBehaviour version, this is not backed by a Unity Transform: the entity's position is this
        /// packet's, which whoever drives the entity keeps current. Targeting and range checks read it (GAS-38).
        /// </summary>
        public AbstractTargetingPacket GetTargetingPacket()
        {
            return _targetingPacket;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // EFFECT HANDLING
        // ═══════════════════════════════════════════════════════════════════════════

        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect effect) => core.GenerateEffectSpec(origin, effect);

        public bool ApplyGameplayEffect(GameplayEffectSpec spec) => core.EffectSystem.ApplyGameplayEffect(spec);

        public bool ApplyGameplayEffect(IEffectOrigin origin, GameplayEffect effect) => core.ApplyGameplayEffect(origin, effect);

        public bool RemoveGameplayEffect(GameplayEffect effect) => core.EffectSystem.RemoveGameplayEffect(effect);

        public bool RemoveGameplayEffect(GameplayEffect effect, IEffectOrigin origin) => core.EffectSystem.RemoveGameplayEffect(effect, origin);

        public bool RemoveGameplayEffect(Tag identifier) => core.EffectSystem.RemoveGameplayEffect(identifier);

        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container)
            => core.EffectSystem.TryGetEffectContainer(effect, out container);

        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers)
            => core.EffectSystem.TryGetEffectContainers(effect, out containers);

        public EffectDurationRemaining GetLongestDurationFor(Tag lookForTag)
            => core.EffectSystem.GetLongestDurationFor(lookForTag);

        public EffectDurationRemaining GetLongestDurationFor(List<Tag> lookForTags)
            => core.EffectSystem.GetLongestDurationFor(lookForTags);

        public GameplayAbilitySystemCallbacks GetCallbacks() => Callbacks;

        // ═══════════════════════════════════════════════════════════════════════════
        // TOSTRING
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString()
        {
            if (EntityData is null) return "[GAS] No EntityData Assigned.";
            return $"{EntityData.GetName()}";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TAG HANDLER / SOURCE INTERFACE
        // ═══════════════════════════════════════════════════════════════════════════

        public ISource GetSource() => this;
        public IHasReadableDefinition GetReadableDefinition() => this;
        public List<Tag> GetContextTags() => EntityData.ContextTags;
        public TagCache GetTagCache() => TagCache;
        public Tag GetAssetTag() => EntityData.AssetTag;
        public IntValuePairClamped GetLevel() => core.GetLevel();
        public IntValuePairClamped GetLevel(Tag key) => core.LevelSystem.GetLevel(key);
        public float GetRelativeLevel() => core.GetRelativeLevel();
        public LevelCallbackStatus SetLevel(Tag key, IntValuePair level) => core.LevelSystem.TrySetLevel(key, level);
        public LevelCallbackStatus ModifyLevel(Tag key, IntValuePair delta) => core.LevelSystem.TryModifyLevel(key, delta);
        public bool IsActive() => !IsDead;

        // Its process's name when it has no Entity Data, which threw
        public override string GetName() => EntityData?.Name ?? ProcessName;

        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            // Once: a process that runs again after a pause says it again (GAS-26)
            if (!regData.Contains(entity, Tags.TARGETED_INTENT)) regData.AddPayload(Tags.TARGETED_INTENT, entity);
        }

        public void WithdrawTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            regData.Remove(Tags.TARGETED_INTENT, entity);
        }

        public List<Tag> GetAffiliation() => core.GetAffiliation();
        public List<Tag> GetAppliedTags() => core.TagCache.GetAppliedTags();
        public int GetTagWeight(Tag _tag) => core.TagCache.GetWeight(_tag);
        public bool QueryTags(TagQuery query) => core.QueryTags(query);
        public void CompileGrantedTags() => core.CompileGrantedTags();
        public void AddLooseTag(Tag tag) => core.AddLooseTag(tag);
        public bool RemoveLooseTag(Tag tag) => core.RemoveLooseTag(tag);
        public System.Collections.Generic.IReadOnlyList<Tag> GetLooseTags() => core.LooseTags;
        public EntityClock GetClock() => core.Clock;

        // ═══════════════════════════════════════════════════════════════════════════
        // IGAMEPLAYABILITYSYSTEM
        // ═══════════════════════════════════════════════════════════════════════════

        public AttributeSystemComponent GetAttributeSystem() => AttributeSystem;
        public AbilitySystemComponent GetAbilitySystem() => AbilitySystem;
        public ItemSystemComponent GetItemSystem() => ItemSystem;
        public EffectSystemComponent GetEffectSystem() => EffectSystem;

        /// <summary>
        /// Null: a runtime GAS has no MonoBehaviour. Ask the interface for what any GAS has, its name among it.
        /// </summary>
        public GameplayAbilitySystem ToGASObject() => null;

        public void InitLocalData(ILocalDataSource source) => core.LocalData.Init(source);
        public void SetLocalData(Tag key, DataWrapper data) => core.LocalData.Set(key, data);
        public bool TryGetLocalData(Tag key, out DataWrapper data) => core.LocalData.TryGet(key, out data);
        public LocalDataStructure GetLocalDataStructure() => core.LocalData;
    }

    public class RuntimeEntityIdentity : IHasReadableDefinition, ITagSource, ILocalDataSource, IWorkerGroupSource
    {
        public string Name;
        public string Description;
        public List<TextureItem> Textures;

        public List<Tag> Affiliation;

        public Tag AssetTag;
        public List<Tag> GrantedTags;
        public List<Tag> ContextTags;

        public int StartingLevel = 1;
        public int MaxLevel = 99;

        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.QueueActivationIfBusy;

        // Defaults as EntityIdentity's: unset, these were null, and giving an ability or an item threw (GAS-23)
        public ScalerIntegerMagnitudeOperation MaxAbilitiesOperation = new() { Magnitude = 99 };
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;

        public ScalerIntegerMagnitudeOperation MaxItemsOperation = new() { Magnitude = 99 };
        public ScalerIntegerMagnitudeOperation MaxEquippedItemsOperation = new() { Magnitude = 99 };
        public List<StartingItemContainer> StartingItems = new();
        public bool AllowDuplicateItems;
        public bool AllowDuplicateEquippedItems;

        public AttributeSet AttributeSet;

        public StandardWorkerGroup WorkerGroup;

        // An asset's Local Data. The copy left it out, so a runtime GAS made from an asset had none (GAS-S1)
        public List<DataWrapper> LocalData = new();

        public RuntimeEntityIdentity()
        {
        }

        public RuntimeEntityIdentity(EntityIdentity data)
        {
            Name = data.Name;
            Description = data.Description;
            Textures = data.Textures;
            Affiliation = data.Affiliation;
            AssetTag = data.AssetTag;
            GrantedTags = data.GrantedTags;
            ContextTags = data.ContextTags;
            StartingLevel = data.StartingLevel;
            MaxLevel = data.MaxLevel;
            ActivationPolicy = data.ActivationPolicy;
            MaxAbilitiesOperation = data.MaxAbilitiesOperation;
            StartingAbilities = data.StartingAbilities;
            AllowDuplicateAbilities = data.AllowDuplicateAbilities;
            MaxItemsOperation = data.MaxItemsOperation;
            MaxEquippedItemsOperation = data.MaxEquippedItemsOperation;
            StartingItems = data.StartingItems;
            AllowDuplicateItems = data.AllowDuplicateItems;
            AllowDuplicateEquippedItems = data.AllowDuplicateEquippedItems;
            AttributeSet = data.AttributeSet;
            WorkerGroup = data.WorkerGroup;
            LocalData = data.LocalData;
        }

        public string GetName()
        {
            return Name;
        }
        public string GetDescription()
        {
            return Description;
        }
        public Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
        }
        public int GetMaxLevel()
        {
            return MaxLevel;
        }
        public int GetStartingLevel()
        {
            return StartingLevel;
        }
        public string GetProviderName()
        {
            return GetName();
        }
        public Tag GetProviderTag()
        {
            return AssetTag;
        }

        // As EntityIdentity's: the affiliation is granted too, which this left out (GAS-S1)
        public IEnumerable<Tag> GetGrantedTags()
        {
            yield return AssetTag;
            if (Affiliation != null) foreach (var tag in Affiliation) yield return tag;
            if (GrantedTags != null) foreach (var tag in GrantedTags) yield return tag;
        }
        public List<DataWrapper> GetLocalData()
        {
            return LocalData;
        }
        public void InitWorkers(ISource system)
        {
            WorkerGroup?.ProvideWorkersTo(system);
        }
        public void RemoveWorkers(ISource system)
        {
            WorkerGroup?.RemoveWorkersFrom(system);
        }
    }
}
