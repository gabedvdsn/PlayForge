using System.Collections.Generic;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface IGameplayAbilitySystem : ISource, ILocalDataUser, IAnalysisUser, IProxyTaskBehaviourCaller
    {
        /// <summary>
        /// Brings a staying entity back, at <paramref name="at"/> or where it was: alive, its revive effects taken, its
        /// body spawned. False where it isn't dead, or doesn't stay.
        /// </summary>
        public bool Revive(UnityEngine.Vector3? at = null) => false;

        /// <summary>The body a staying entity follows, while it has one.</summary>
        public AbstractMonoProcess GetBody() => null;

        /// <summary>
        /// Gives the running entity a body of its own, one that isn't its identity's Body: a MonoBehaviour process,
        /// running already or registered for it here, which a runtime entity follows and which ends as the entity dies, as
        /// its identity's Body does; a revive spawns the identity's Body, where it has one. The body it had ends first, and
        /// null lets go of the one it has, ending it. False for a dead entity, which takes none.
        /// </summary>
        public bool UseBody(AbstractMonoProcess body) => false;

        public AttributeSystemComponent GetAttributeSystem();
        public AbilitySystemComponent GetAbilitySystem();

        /// <summary>
        /// Sends a gameplay event to the entity: each ability it knows that the event triggers activates, with the event's
        /// data. How many activated.
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

        /// <summary>The modifiers it holds now, which change its own effects and those landing on it.</summary>
        public EffectModifierSet GetEffectModifiers() => null;

        /// <summary>The auras it runs, which hold their effects on those near it.</summary>
        public AuraSet GetAuras() => null;

        /// <summary>The crowd control rules its abilities follow: its own, or the project's.</summary>
        public CrowdControlRules GetCrowdControl() => null;

        /// <summary>Sets its own sides, as the game runs, which its tags follow; effects that change its sides go over them.</summary>
        public void SetAffiliation(List<Tag> sides) { }

        /// <summary>The side changes it's under, each from an effect that lasts.</summary>
        public SideSet GetSides() => null;

        /// <summary>Its combat log: what the project subscribes to of the hits it takes and deals, its changes and its events.</summary>
        public CombatLog GetCombatLog() => null;

        /// <summary>
        /// Grants a tag directly, with no effect, ability or item behind it, as Unreal's loose gameplay tags are. Each
        /// grant counts, and lasts until <see cref="RemoveLooseTag"/> takes it back. A tag added straight to the tag cache
        /// went at its next compile, when an ability starts or ends, an item changes or an effect lands.
        /// </summary>
        public void AddLooseTag(Tag tag);

        /// <summary>Takes back one grant of a loose tag. False when the entity has none.</summary>
        public bool RemoveLooseTag(Tag tag);

        /// <summary>The entity's loose tags, a tag once for each grant.</summary>
        public System.Collections.Generic.IReadOnlyList<Tag> GetLooseTags();

        /// <summary>
        /// The entity's own time, which its effects, cooldowns and abilities' waits run by: the game's, scaled by its
        /// time scale.
        /// </summary>
        public EntityClock GetClock();

        /// <summary>
        /// Turns the entity to face a target, which it follows as it moves, flat, at
        /// <paramref name="degreesPerSecond"/> of its own time, 0 at once, until it faces it within
        /// <paramref name="within"/> degrees. Another order replaces the turn; its death and <see cref="StopTurning"/>
        /// end it. A turner of the game's own on its object, an <see cref="IForgeTurner"/>, takes each step where it
        /// has one; otherwise its facing is set. The entity itself, or a target with no place, turns nothing.
        /// </summary>
        public void Face(ITarget target, float degreesPerSecond, float within = 2f);

        /// <summary>
        /// Turns the entity to face a point, as <see cref="Face(ITarget, float, float)"/> turns it to a target.
        /// </summary>
        public void Face(UnityEngine.Vector3 point, float degreesPerSecond, float within = 2f);

        /// <summary>
        /// Ends the entity's turn, where it's turning, as a move of the game's takes over; an ability still waiting on
        /// the turn to start gives way, cancelled.
        /// </summary>
        public void StopTurning();

        /// <summary>
        /// How far, in degrees, flat, the entity has yet to turn to face what its turn faces; 0 when it isn't turning.
        /// </summary>
        public float AngleToFace { get; }

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
