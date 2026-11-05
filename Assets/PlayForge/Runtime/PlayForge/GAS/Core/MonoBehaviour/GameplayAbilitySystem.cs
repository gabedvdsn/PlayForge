using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A GAS on a GameObject. What makes it a GAS lives in <see cref="GASCore"/>, which it builds as it initializes and
    /// forwards to. What stays here is the asset it is made from, its place in the scene, and how it goes away (GAS-S1).
    /// </summary>
    public partial class GameplayAbilitySystem : LazyMonoProcess, IGameplayAbilitySystem
    {
        public EntityIdentity EntityData;

        // Built as this initializes, so null before then
        private GASCore core;

        // What a save reads and writes past the hub's public face (FEAT-9)
        internal GASCore Core => core;

        // Subsystems
        public AttributeSystemComponent AttributeSystem => core?.AttributeSystem;
        public AbilitySystemComponent AbilitySystem => core?.AbilitySystem;
        public ItemSystemComponent ItemSystem => core?.ItemSystem;
        public EffectSystemComponent EffectSystem => core?.EffectSystem;

        public SystemLevelsComponent LevelSystem => core?.LevelSystem;

        // Tags
        protected TagCache TagCache => core?.TagCache;

        /// <summary>
        /// Callbacks for GAS-level events (frame completion, action queue, effects, etc.). Created with the
        /// component, so a caller can subscribe (to <c>OnSystemInitialized</c> above all) before the GAS is
        /// registered and initialized; initialization keeps this instance (PF-013).
        /// </summary>
        public GameplayAbilitySystemCallbacks Callbacks { get; private set; } = new();

        #region Process Parameters
        public override void WhenInitialize()
        {
            base.WhenInitialize();

            // The registration data's sides, on this entity alone (GAS-47)
            var registered = regData.TryGet(Tags.AFFILIATION, EDataTarget.Primary, out List<Tag> sides) ? sides : null;

            core = new GASCore(this, Callbacks);
            core.Initialize(EntityData ? new RuntimeEntityIdentity(EntityData) : null, registered);

            // An entity for the Mono Entities counter as it runs, GameRoot aside (FEAT-18)
            if (this is not IManagerial && !counted)
            {
                ForgeCounters.MonoEntities.Value++;
                counted = true;
            }
        }

        // Whether the Mono Entities counter has it
        private bool counted;

        public override void WhenUpdate() => core.Tick();

        public override void WhenLateUpdate() => EndOfFrame();

        // Nothing starts on it from now on, and the base ends what it handles, its abilities among them (GAS-52)
        public override void WhenTerminate()
        {
            if (counted)
            {
                ForgeCounters.MonoEntities.Value--;
                counted = false;
            }
            core?.WhenTerminate();
            base.WhenTerminate();
        }

        #endregion

        #region Source Targeting Parameters
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
        public AbstractTargetingPacket GetTargetingPacket()
        {
            return new TargetingPacket(transform, this);
        }

        #endregion

        #region Effect Handling

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

        #endregion

        public override string ToString()
        {
            if (EntityData is null) return "[GAS] No EntityData Assigned.";
            return $"{EntityData.GetName()}";
        }

        #region IAttributeDerivation
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

        // Its GameObject's name when it has no Entity Data, which threw
        public override string GetName() => EntityData ? EntityData.Name : name;

        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            // Once: a process that runs again after a pause says it again (GAS-26)
            if (!regData.Contains(entity, Tags.TARGETED_INTENT)) regData.AddPayload(Tags.TARGETED_INTENT, entity);
        }

        public void WithdrawTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            regData.Remove(Tags.TARGETED_INTENT, entity);
        }

        // Its asset's until it initializes
        public List<Tag> GetAffiliation() => core != null ? core.GetAffiliation() : EntityData?.Affiliation;
        public List<Tag> GetAppliedTags() => core.TagCache.GetAppliedTags();
        public int GetTagWeight(Tag _tag) => core.TagCache.GetWeight(_tag);
        public bool QueryTags(TagQuery query) => core.QueryTags(query);
        public void CompileGrantedTags() => core.CompileGrantedTags();
        public void AddLooseTag(Tag tag) => core.AddLooseTag(tag);
        public bool RemoveLooseTag(Tag tag) => core.RemoveLooseTag(tag);
        public System.Collections.Generic.IReadOnlyList<Tag> GetLooseTags() => core.LooseTags;
        public EntityClock GetClock() => core.Clock;
        #endregion

        #region IGameplaySystem

        public AttributeSystemComponent GetAttributeSystem() => AttributeSystem;
        public AbilitySystemComponent GetAbilitySystem() => AbilitySystem;
        public ItemSystemComponent GetItemSystem() => ItemSystem;
        public EffectSystemComponent GetEffectSystem() => EffectSystem;
        public GameplayAbilitySystem ToGASObject() => this;

        #endregion

        public static GameplayAbilitySystem AddToGameObject(GameObject obj, EntityIdentity entity)
        {
            var gas = obj.AddComponent<GameplayAbilitySystem>();
            gas.EntityData = entity;
            return gas;
        }
        public void InitLocalData(ILocalDataSource source) => core.LocalData.Init(source);
        public void SetLocalData(Tag key, DataWrapper data) => core.LocalData.Set(key, data);
        public bool TryGetLocalData(Tag key, out DataWrapper data) => core.LocalData.TryGet(key, out data);
        public LocalDataStructure GetLocalDataStructure() => core?.LocalData;
    }
}
