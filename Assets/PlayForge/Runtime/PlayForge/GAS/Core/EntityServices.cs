using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A per-entity object its components share, as the auras an entity runs, <see cref="AuraSet"/>, or the wait a Respawn
    /// counts down, <see cref="RespawnTimer"/>: made the first time it's asked for, with
    /// <see cref="IGameplayAbilitySystem.GetService{T}"/>; stepped each frame by its entity's own time; told as its entity
    /// revives; and ended with its entity. Each hook is a no-op a service overrides as it needs, so a heat meter or a combo
    /// counter of your own lives beside PlayForge's.
    /// </summary>
    public abstract class AbstractEntityService
    {
        /// <summary>The entity it's of.</summary>
        public IGameplayAbilitySystem System { get; private set; }

        // Made for an entity, by its entity's services
        internal void MadeFor(IGameplayAbilitySystem system) => System = system;

        /// <summary>
        /// Each frame, as its entity steps, by the entity's own time, its time scale times the game's: after the entity's
        /// effects, before its turn and its barriers.
        /// </summary>
        public virtual void Step(float deltaTime) { }

        /// <summary>As a staying entity revives, before anything else of the revive, as a wait for one ends.</summary>
        public virtual void Revive() { }

        /// <summary>As its entity ends: what it holds let go, as an aura's effects come off those it held them on.</summary>
        public virtual void End() { }
    }

    /// <summary>An entity's services, in the order they were made, which its core steps, tells and ends.</summary>
    internal sealed class EntityServices
    {
        private readonly IGameplayAbilitySystem owner;
        private readonly List<AbstractEntityService> services = new();

        public EntityServices(IGameplayAbilitySystem owner) => this.owner = owner;

        /// <summary>Its service of a kind, made the first time it's asked for.</summary>
        public T Get<T>() where T : AbstractEntityService, new()
        {
            if (Find<T>() is { } found) return found;
            var made = new T();
            made.MadeFor(owner);
            services.Add(made);
            return made;
        }

        /// <summary>Its service of a kind, or null where none was made.</summary>
        public T Find<T>() where T : AbstractEntityService
        {
            for (int i = 0; i < services.Count; i++)
                if (services[i] is T found) return found;
            return null;
        }

        // Each, by the entity's own time; one made as another steps steps this frame too
        public void Step(float deltaTime)
        {
            for (int i = 0; i < services.Count; i++) services[i].Step(deltaTime);
        }

        public void Revive()
        {
            for (int i = 0; i < services.Count; i++) services[i].Revive();
        }

        // Each ends, in the order they were made, and the entity keeps none
        public void End()
        {
            for (int i = 0; i < services.Count; i++) services[i].End();
            services.Clear();
        }
    }
}
