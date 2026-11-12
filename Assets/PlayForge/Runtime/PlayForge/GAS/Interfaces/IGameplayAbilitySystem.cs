using System.Collections.Generic;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface IGameplayAbilitySystem : ISource, ILocalDataUser, IAnalysisUser, IProxyTaskBehaviourCaller
    {
        /// <summary>
        /// Brings a staying entity back, at <paramref name="at"/> or where it was: alive, its revive effects taken, its
        /// body spawned. False where it isn't dead, or doesn't stay (TW-4).
        /// </summary>
        public bool Revive(UnityEngine.Vector3? at = null) => false;

        /// <summary>The body a staying entity follows, while it has one (TW-4).</summary>
        public AbstractMonoProcess GetBody() => null;

        public AttributeSystemComponent GetAttributeSystem();
        public AbilitySystemComponent GetAbilitySystem();

        /// <summary>
        /// Sends a gameplay event to the entity: each ability it knows that the event triggers activates, with the event's
        /// data. How many activated (FEAT-4).
        /// </summary>
        public int SendGameplayEvent(Tag eventTag, ProcessDataPacket data = null) => GetAbilitySystem()?.SendGameplayEvent(eventTag, data) ?? 0;
        public ItemSystemComponent GetItemSystem();
        public EffectSystemComponent GetEffectSystem();
        
        /// <summary>
        /// The MonoBehaviour hub, for its GameObject or transform; null for a runtime GAS, which has none. What every GAS
        /// has, its name among it, ask of this interface instead.
        /// </summary>
        [CanBeNull] public GameplayAbilitySystem ToGASObject();

        public bool RemoveGameplayEffect(Tag identifier);
        
        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container);
        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers);

        public GameplayAbilitySystemCallbacks GetCallbacks();

        /// <summary>The modifiers it holds now, which change its own effects and those landing on it (TW-7).</summary>
        public EffectModifierSet GetEffectModifiers() => null;

        /// <summary>The auras it runs, which hold their effects on those near it (TW-10).</summary>
        public AuraSet GetAuras() => null;

        /// <summary>The crowd control rules its abilities follow: its own, or the project's (TW-16).</summary>
        public CrowdControlRules GetCrowdControl() => null;

        /// <summary>Sets its own sides, as the game runs, which its tags follow; effects that change its sides go over them (TW-18).</summary>
        public void SetAffiliation(List<Tag> sides) { }

        /// <summary>The side changes it's under, each from an effect that lasts (TW-18).</summary>
        public SideSet GetSides() => null;

        /// <summary>
        /// Grants a tag directly, with no effect, ability or item behind it, as Unreal's loose gameplay tags are. Each
        /// grant counts, and lasts until <see cref="RemoveLooseTag"/> takes it back. A tag added straight to the tag cache
        /// went at its next compile, when an ability starts or ends, an item changes or an effect lands (FEAT-16).
        /// </summary>
        public void AddLooseTag(Tag tag);

        /// <summary>Takes back one grant of a loose tag. False when the entity has none.</summary>
        public bool RemoveLooseTag(Tag tag);

        /// <summary>The entity's loose tags, a tag once for each grant.</summary>
        public System.Collections.Generic.IReadOnlyList<Tag> GetLooseTags();

        /// <summary>
        /// The entity's own time, which its effects, cooldowns and abilities' waits run by: the game's, scaled by its
        /// time scale (FEAT-7).
        /// </summary>
        public EntityClock GetClock();

        /// <summary>
        /// Record an impact this GAS DEALT (this GAS is ISource).
        /// </summary>
        public void RecordFrameImpactDealt(ImpactData impact);

        /// <summary>
        /// Record an impact this GAS RECEIVED (this GAS is ITarget).
        /// </summary>
        public void RecordFrameImpactReceived(ImpactData impact);
    }

}
