using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A component of a PlayForge asset: a feature it carries, added in its Components section, a class with hooks into its
    /// asset's life, each a no-op a kind may override (FN-14, Decision 76). An effect's, an ability's, an entity's and an
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
    }

    /// <summary>What a holder's components keep for it: an object for each component, made the first time it's asked for (FN-14).</summary>
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
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Effects
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>A component of an effect (FN-14): it hears the effect land, tick, hit and end, at the moments its workers do.</summary>
    [Serializable]
    public abstract class AbstractEffectComponent : AbstractForgeComponent
    {
        /// <summary>As the effect lands on the entity it's on.</summary>
        public virtual void Land(EffectComponentContext ctx) { }

        /// <summary>As a lasting effect executes its period's tick.</summary>
        public virtual void Tick(EffectComponentContext ctx) { }

        /// <summary>As an impact the effect deals lands.</summary>
        public virtual void Hit(EffectComponentContext ctx) { }

        /// <summary>As the effect comes off: an instant one as it's done, a lasting one as it's removed.</summary>
        public virtual void End(EffectComponentContext ctx) { }
    }

    /// <summary>What an effect's component hears a moment of its effect with (FN-14).</summary>
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

    /// <summary>How an ability's activation ended (FN-14).</summary>
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
    /// A component of an ability (FN-14): it hears the ability granted and removed, may refuse an activation, and hears an
    /// activation start, commit its cost and cooldown, and end, by how it did.
    /// </summary>
    [Serializable]
    public abstract class AbstractAbilityComponent : AbstractForgeComponent
    {
        /// <summary>As an entity learns the ability.</summary>
        public virtual void Granted(AbilityComponentContext ctx) { }

        /// <summary>As an entity loses the ability.</summary>
        public virtual void Removed(AbilityComponentContext ctx) { }

        /// <summary>Why an activation may not start, or null where it may; the reason names the refusal.</summary>
        public virtual string CanStart(AbilityComponentContext ctx) => null;

        /// <summary>As an activation starts, its claim taken.</summary>
        public virtual void Start(AbilityComponentContext ctx) { }

        /// <summary>As an activation's cost and cooldown apply.</summary>
        public virtual void Commit(AbilityComponentContext ctx) { }

        /// <summary>As an activation ends, by how it did.</summary>
        public virtual void End(AbilityComponentContext ctx, EAbilityEnd how) { }
    }

    /// <summary>What an ability's component hears a moment of its ability with (FN-14).</summary>
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

    /// <summary>A component of an entity (FN-14): it hears its entity register, die, revive and end.</summary>
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
    }

    /// <summary>What an entity's component hears a moment of its entity with (FN-14).</summary>
    public readonly struct EntityComponentContext
    {
        /// <summary>The entity.</summary>
        public readonly IGameplayAbilitySystem System;

        private readonly ComponentStates states;

        public EntityComponentContext(IGameplayAbilitySystem system, ComponentStates states)
        {
            System = system;
            this.states = states;
        }

        /// <summary>What <paramref name="component"/> keeps for this entity.</summary>
        public T GetState<T>(AbstractEntityComponent component) where T : class, new() => states?.Of<T>(component);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Items
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>A component of an item (FN-14): it hears the item given, equipped, unequipped and removed.</summary>
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
    }

    /// <summary>What an item's component hears a moment of its item with (FN-14).</summary>
    public readonly struct ItemComponentContext
    {
        /// <summary>The item as its entity holds it.</summary>
        public readonly ItemSpecContainer Container;

        /// <summary>The entity that holds the item.</summary>
        public ISource Source => Container?.Spec?.Source;

        public ItemComponentContext(ItemSpecContainer container) => Container = container;

        /// <summary>What <paramref name="component"/> keeps for this entity's item.</summary>
        public T GetState<T>(AbstractItemComponent component) where T : class, new() => Container?.ComponentStates.Of<T>(component);
    }
}
