using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A component of a PlayForge asset: a feature it carries, added in its Components section, a class with hooks into its
    /// asset's life, each a no-op a kind may override. An effect's, an ability's, an entity's and an
    /// item's components each hear their own asset's moments, beside its workers. A component is its asset's, shared by
    /// every entity the asset reaches, so what it keeps for one of them it keeps through its context's state, never on itself.
    /// </summary>
    [Serializable]
    public abstract class AbstractForgeComponent
    {
        /// <summary>
        /// Whether an asset may hold more than one of this kind. One that makes no sense twice, as Charges, says no, and its
        /// Components section offers it only while the asset holds none.
        /// </summary>
        public virtual bool AllowsSeveral => true;

        /// <summary>
        /// What the component does, in one line with its numbers, as "Range 600" or "3 charges, each recharging on its own",
        /// which The Forge's summary and the breakdowns say beside its name; null to say its name alone. A
        /// component of your own overrides it.
        /// </summary>
        public virtual string Describe() => null;

        /// <summary>
        /// The first of a list's components that is a <typeparamref name="T"/>, its kind or a capability it implements, as
        /// an <see cref="IAbilityReach"/>; null where none is. Ask for an interface, not a class, and a component of your
        /// own that implements it is found as PlayForge's are. An asset's own <c>Find</c> asks its list.
        /// </summary>
        public static T Find<T>(IReadOnlyList<AbstractForgeComponent> components) where T : class
        {
            if (components == null) return null;
            for (int i = 0; i < components.Count; i++)
                if (components[i] is T found) return found;
            return null;
        }

        /// <summary>A component's name, as its picker and The Forge show it: its [ForgeMenu] name, or its class's.</summary>
        public static string NameOf(AbstractForgeComponent component) => component == null ? "" : NameOf(component.GetType());

        /// <summary>A component kind's name: its [ForgeMenu] name, or its class's without its family's suffix.</summary>
        public static string NameOf(System.Type type)
        {
            if (type == null) return "";
            if (ForgeMenuAttribute.NameOf(type) is { } named) return named;
            var name = type.Name;
            foreach (var suffix in new[] { "EffectComponent", "AbilityComponent", "EntityComponent", "ItemComponent", "Component" })
                if (name.EndsWith(suffix) && name.Length > suffix.Length) { name = name.Substring(0, name.Length - suffix.Length); break; }
            return System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");
        }

        /// <summary>An asset's name for a component's line: its PlayForge name, its file's, or "nothing".</summary>
        protected static string NameOf(UnityEngine.ScriptableObject asset) => asset switch
        {
            null => "nothing",
            BaseForgeAsset forge when !string.IsNullOrEmpty(forge.GetName()) => forge.GetName(),
            _ => asset.name,
        };

        /// <summary>Who an area or an aura takes, in words: "allies", "enemies", "everyone".</summary>
        protected static string Whom(EAffinityFilter takes) => System.Text.RegularExpressions.Regex.Replace(takes.ToString(), "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
    }

    /// <summary>What a holder's components keep for it: an object for each component, made the first time it's asked for.</summary>
    public sealed class ComponentStates
    {
        private Dictionary<AbstractForgeComponent, object> states;

        /// <summary>What <paramref name="component"/> keeps here, made the first time it's asked for.</summary>
        public T Of<T>(AbstractForgeComponent component) where T : class, new()
        {
            if (component == null) return null;
            states ??= new Dictionary<AbstractForgeComponent, object>();
            if (states.TryGetValue(component, out var state) && state is T kept) return kept;
            var made = new T();
            states[component] = made;
            return made;
        }

        /// <summary>
        /// What <paramref name="component"/> keeps here, where it keeps a <typeparamref name="T"/>; false where it keeps
        /// none yet. With <see cref="Keep"/>, a state made with what it needs, as a Charges component's charges need their
        /// ability.
        /// </summary>
        public bool TryGet<T>(AbstractForgeComponent component, out T state) where T : class
        {
            state = null;
            return component != null && states != null && states.TryGetValue(component, out var kept) && (state = kept as T) != null;
        }

        /// <summary>Keeps <paramref name="state"/> here for <paramref name="component"/>, in place of what it kept.</summary>
        public void Keep(AbstractForgeComponent component, object state)
        {
            if (component == null) return;
            states ??= new Dictionary<AbstractForgeComponent, object>();
            states[component] = state;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Effects
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A component of an effect: it hears the effect land, tick, hit and end, at the moments its workers do, and a lasting
    /// effect lend and take back what it gives while it lasts.
    /// </summary>
    [Serializable]
    public abstract class AbstractEffectComponent : AbstractForgeComponent
    {
        /// <summary>
        /// As a lasting effect starts to last on the one it's on, and lends what it gives while it lasts, as an aura: as it
        /// lands, before its Land; as it takes the place of another effect under its tag; and as a load or a move puts it
        /// back, where it doesn't land again. Each is matched by a <see cref="TakeBack"/>. An instant effect lends nothing.
        /// </summary>
        public virtual void Lend(EffectComponentContext ctx) { }

        /// <summary>
        /// As a lasting effect stops lasting on the one it's on, taking back what it lent: as it comes off, before its End;
        /// as another effect takes its place; and as a load clears it or a move takes it to another.
        /// </summary>
        public virtual void TakeBack(EffectComponentContext ctx) { }

        /// <summary>As the effect lands on the entity it's on.</summary>
        public virtual void Land(EffectComponentContext ctx) { }

        /// <summary>As a lasting effect executes its period's tick.</summary>
        public virtual void Tick(EffectComponentContext ctx) { }

        /// <summary>As an impact the effect deals lands.</summary>
        public virtual void Hit(EffectComponentContext ctx) { }

        /// <summary>As the effect comes off: an instant one as it's done, a lasting one as it's removed.</summary>
        public virtual void End(EffectComponentContext ctx) { }

        /// <summary>
        /// As the one it's on dies, while it's on them, before what the death ends comes off; heard whether the entity
        /// stays or ends.
        /// </summary>
        public virtual void Death(EffectComponentContext ctx) { }
    }

    /// <summary>What an effect's component hears a moment of its effect with.</summary>
    public readonly struct EffectComponentContext
    {
        /// <summary>The entity of the moment, as the effect's workers have it.</summary>
        public readonly IGameplayAbilitySystem System;

        /// <summary>The effect's application.</summary>
        public readonly GameplayEffectSpec Spec;

        /// <summary>For a tick, how many ticks it executes.</summary>
        public readonly int ExecuteTicks;

        /// <summary>For a hit, its impact.</summary>
        public readonly ImpactData? ImpactData;

        public EffectComponentContext(in EffectWorkerContext from)
        {
            System = from.System;
            Spec = from.Spec;
            ExecuteTicks = from.ExecuteTicks;
            ImpactData = from.ImpactData;
        }

        /// <summary>What <paramref name="component"/> keeps for this application of its effect.</summary>
        public T GetState<T>(AbstractEffectComponent component) where T : class, new() => Spec?.ComponentStates.Of<T>(component);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Abilities
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>How an ability's activation ended.</summary>
    public enum EAbilityEnd
    {
        /// <summary>It ran to its end.</summary>
        Done,

        /// <summary>It was cancelled, in its targeting or as it ran, or its target was refused.</summary>
        Cancelled,

        /// <summary>It threw, in its targeting or as it ran.</summary>
        Failed,
    }

    /// <summary>
    /// The checks of an ability's use a component may waive, <see cref="AbstractAbilityComponent.Waives"/>: its cost, as the
    /// Cost check asks, and its cooldown, as the Cooldown check asks.
    /// </summary>
    [Flags]
    public enum EUsageCheck
    {
        None = 0,

        /// <summary>Its cost: its Cost effect and its attribute costs.</summary>
        Cost = 1,

        /// <summary>Its cooldown: a use in hand, as its cooldown model says.</summary>
        Cooldown = 2,
    }

    /// <summary>
    /// A component of an ability: it hears the ability granted and removed, may refuse an activation, and hears an
    /// activation start, wind up, which the activation waits on, commit its cost and cooldown, and end, by how it did.
    /// </summary>
    [Serializable]
    public abstract class AbstractAbilityComponent : AbstractForgeComponent
    {
        /// <summary>As an entity learns the ability.</summary>
        public virtual void Granted(AbilityComponentContext ctx) { }

        /// <summary>As an entity loses the ability.</summary>
        public virtual void Removed(AbilityComponentContext ctx) { }

        /// <summary>
        /// Why an activation may not start, or null where it may; the reason names the refusal and says why, and
        /// <see cref="RefusedAs"/> is its kind.
        /// </summary>
        public virtual string CanStart(AbilityComponentContext ctx) => null;

        /// <summary>
        /// The kind of refusal its <see cref="CanStart"/> is told as, its reason naming it: Rule, as here; a Cast Range's
        /// is Range, so an AI can walk closer and ask again.
        /// </summary>
        public virtual EActivationRefusal RefusedAs => EActivationRefusal.Rule;

        /// <summary>
        /// Why the target or the point the activation's targeting picked is refused, which ends the activation there, as
        /// its targeting ends, before its wind-up, its cost and its stages; or null, as here, where it's taken. The pick
        /// is in its data under Target: a Cast Range refuses one beyond its range, whichever task picked it.
        /// </summary>
        public virtual string CanTakeTarget(AbilityComponentContext ctx) => null;

        /// <summary>
        /// What an activation waits on as it winds up, after its targeting and its target checks and before its cost
        /// and its first stage, as a turn to face its target; its entity's components' with it. An interrupt cancels
        /// the token, and the activation, its cost unpaid, as an <see cref="OperationCanceledException"/> thrown here
        /// does. Nothing to wait on, here.
        /// </summary>
        public virtual UniTask WindUp(AbilityComponentContext ctx, CancellationToken token) => UniTask.CompletedTask;

        /// <summary>As an activation starts, its claim taken.</summary>
        public virtual void Start(AbilityComponentContext ctx) { }

        /// <summary>
        /// Applies its ability's usage in the ability's place, and says it did, as a Toggle turns its effect on, paying the
        /// cost, or off, spending the cooldown; false, as here, leaves it to the ability, which spends its cooldown through
        /// its cooldown model and pays its costs. The first component that applies it does. A component composes its own
        /// from the spec's <see cref="AbilitySpec.PayCosts"/> and <see cref="AbilitySpec.SpendCooldown"/>; its commit is
        /// heard either way.
        /// </summary>
        public virtual bool ApplyUsage(AbilityComponentContext ctx) => false;

        /// <summary>
        /// The checks of its ability's use it waives for an activation, which pass while it does: a Toggle that's on waives
        /// its cost and its cooldown, so it can always turn off. None, as here.
        /// </summary>
        public virtual EUsageCheck Waives(AbilityComponentContext ctx) => EUsageCheck.None;

        /// <summary>As an activation's cost and cooldown apply.</summary>
        public virtual void Commit(AbilityComponentContext ctx) { }

        /// <summary>
        /// The categories an effect its ability put on is of, for a purge, beside those the ability gives it, as its
        /// Cooldown's: a Toggle's effect is a Toggle's. None, as here. Asked of the effect as a purge finds it, so a load
        /// that puts the effect back keeps its category.
        /// </summary>
        public virtual EEffectCategory CategoryOf(AbilityComponentContext ctx, GameplayEffectSpec effect) => EEffectCategory.None;

        /// <summary>As an activation ends, by how it did.</summary>
        public virtual void End(AbilityComponentContext ctx, EAbilityEnd how) { }

        /// <summary>
        /// What it adds to its ability's use, in a few words, which the ability's Activation line says after its cost and
        /// its cooldown, as Charges' "3 charges"; null, as here, for nothing.
        /// </summary>
        public virtual string DescribeUsage() => null;

        /// <summary>
        /// The check the component stands behind among its ability's Target Checks, shown there while the ability holds
        /// it, as Cast Range's Check Range; null, as it is here, for none. The component enforces what it says, as the
        /// check, an <see cref="AbstractComponentValidationRule{TComponent}"/>, passes.
        /// </summary>
        public virtual IAbilityValidationRule TargetCheck => null;

        /// <summary>
        /// The check the component stands behind among its ability's Caster Checks, as <see cref="TargetCheck"/> is
        /// among the Target Checks; null, as it is here, for none.
        /// </summary>
        public virtual IAbilityValidationRule CasterCheck => null;
    }

    /// <summary>What an ability's component hears a moment of its ability with.</summary>
    public readonly struct AbilityComponentContext
    {
        /// <summary>The ability as its entity holds it.</summary>
        public readonly AbilitySpec Spec;

        /// <summary>The activation's data, for an activation's moments; null as the ability's granted or removed.</summary>
        public readonly ProcessDataPacket Data;

        /// <summary>The entity that holds the ability.</summary>
        public ISource Source => Spec?.GetSource();

        public AbilityComponentContext(AbilitySpec spec, ProcessDataPacket data = null)
        {
            Spec = spec;
            Data = data;
        }

        /// <summary>What <paramref name="component"/> keeps for this entity's ability.</summary>
        public T GetState<T>(AbstractAbilityComponent component) where T : class, new() => Spec?.ComponentStates.Of<T>(component);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entities
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A component of an entity: it hears its entity register, die, revive and end, and an activation of one of its
    /// abilities wind up, which the activation waits on.
    /// </summary>
    [Serializable]
    public abstract class AbstractEntityComponent : AbstractForgeComponent
    {
        /// <summary>As the entity registers, its systems set up.</summary>
        public virtual void Register(EntityComponentContext ctx) { }

        /// <summary>As the entity dies, whether it stays or ends.</summary>
        public virtual void Death(EntityComponentContext ctx) { }

        /// <summary>As a staying entity revives.</summary>
        public virtual void Revive(EntityComponentContext ctx) { }

        /// <summary>As the entity ends.</summary>
        public virtual void End(EntityComponentContext ctx) { }

        /// <summary>
        /// What an activation of one of its abilities waits on as it winds up, before the ability's cost and its first
        /// stage, with the ability's own components' wind-ups: a turn to face its target, as Turns To Face gives.
        /// Nothing to wait on, here.
        /// </summary>
        public virtual UniTask WindUp(EntityComponentContext ctx, AbilityComponentContext ability,
            CancellationToken token) => UniTask.CompletedTask;
    }

    /// <summary>What an entity's component hears a moment of its entity with.</summary>
    public readonly struct EntityComponentContext
    {
        /// <summary>The entity.</summary>
        public readonly IGameplayAbilitySystem System;

        /// <summary>
        /// For its death, who killed it: the source of the last loss it took from another, as its Died event names; null at
        /// any other moment, or for a death no one dealt.
        /// </summary>
        public readonly ISource Killer;

        /// <summary>The entity's settings as its GAS uses them, its Death among them; null for a hub given none.</summary>
        public readonly RuntimeEntityIdentity Identity;

        private readonly ComponentStates states;

        public EntityComponentContext(IGameplayAbilitySystem system, ComponentStates states, ISource killer = null,
            RuntimeEntityIdentity identity = null)
        {
            System = system;
            this.states = states;
            Killer = killer;
            Identity = identity;
        }

        /// <summary>What <paramref name="component"/> keeps for this entity.</summary>
        public T GetState<T>(AbstractEntityComponent component) where T : class, new() => states?.Of<T>(component);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Items
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>A component of an item: it hears the item given, equipped, unequipped and removed.</summary>
    [Serializable]
    public abstract class AbstractItemComponent : AbstractForgeComponent
    {
        /// <summary>As an entity is given the item, in a slot of its own.</summary>
        public virtual void Given(ItemComponentContext ctx) { }

        /// <summary>As the item is equipped.</summary>
        public virtual void Equipped(ItemComponentContext ctx) { }

        /// <summary>As the item is unequipped.</summary>
        public virtual void Unequipped(ItemComponentContext ctx) { }

        /// <summary>As the item is taken away.</summary>
        public virtual void Removed(ItemComponentContext ctx) { }

        /// <summary>
        /// As its holder's use of the item's ability ends, having paid what it costs: one use of the item.
        /// </summary>
        public virtual void Used(ItemComponentContext ctx) { }
    }

    /// <summary>What an item's component hears a moment of its item with.</summary>
    public readonly struct ItemComponentContext
    {
        /// <summary>The item as its entity holds it.</summary>
        public readonly ItemSpecContainer Container;

        /// <summary>The entity that holds the item.</summary>
        public ISource Source => Container?.Spec?.Source;

        /// <summary>
        /// Whether the moment comes from a load, which puts back what the component's earlier moments did, as the effects
        /// it applied.
        /// </summary>
        public readonly bool Restoring;

        public ItemComponentContext(ItemSpecContainer container, bool restoring = false)
        {
            Container = container;
            Restoring = restoring;
        }

        /// <summary>What <paramref name="component"/> keeps for this entity's item.</summary>
        public T GetState<T>(AbstractItemComponent component) where T : class, new() => Container?.ComponentStates.Of<T>(component);
    }
}
