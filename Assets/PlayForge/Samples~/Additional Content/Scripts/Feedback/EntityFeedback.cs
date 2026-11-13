using UnityEngine;

namespace FarEmerald.PlayForge.AdditionalContent
{
    /// <summary>
    /// What the feedback components on an entity share (AC-2): the entity each shows, which is the hub on the same
    /// object or above it unless one is set, or given in code; and that entity's changes to one attribute, the project's
    /// Health role's where none is set. Each hooks to the entity's callbacks once it has them, so it can sit on an entity
    /// registered before it or after.
    /// </summary>
    public abstract class EntityFeedback : MonoBehaviour
    {
        [Tooltip("The entity it shows; with none, the hub on this object or above it")]
        public GameplayAbilitySystem Entity;

        [Tooltip("The attribute whose changes it shows; with none, the one playing the project's Health role")]
        public Attribute Attribute;

        private IGameplayAbilitySystem watched;
        private GameplayAbilitySystemCallbacks hooked;

        /// <summary>The entity it shows: the one given in code, or set, or found on this object or above it.</summary>
        public IGameplayAbilitySystem Watched
        {
            get
            {
                if (watched != null) return watched;
                if (Entity != null) return watched = Entity;
                var found = GetComponentInParent<GameplayAbilitySystem>();
                return found != null ? watched = found : null;
            }
        }

        /// <summary>The attribute whose changes it shows.</summary>
        public Attribute Counted => Attribute != null ? Attribute : ForgeRoles.AttributeFor(Tags.ROLE_HEALTH);

        /// <summary>Whether it hears the entity's callbacks yet.</summary>
        public bool Listening => hooked != null;

        /// <summary>Shows an entity given in code, as a runtime hub, which no component sits on.</summary>
        public void Watch(IGameplayAbilitySystem entity)
        {
            Unhook();
            watched = entity;
            Hook();
        }

        protected virtual void OnEnable() => Hook();

        protected virtual void OnDisable() => Unhook();

        protected virtual void Update()
        {
            if (hooked == null) Hook();
        }

        private void Hook()
        {
            if (hooked != null) return;
            var callbacks = Watched?.GetCallbacks();
            if (callbacks == null) return;
            callbacks.OnImpactReceived += Received;
            hooked = callbacks;
            Hooked(callbacks);
        }

        private void Unhook()
        {
            if (hooked == null) return;
            hooked.OnImpactReceived -= Received;
            Unhooked(hooked);
            hooked = null;
        }

        private void Received(ImpactData impact)
        {
            var counted = Counted;
            if (counted == null || impact.Attribute == null || !impact.Attribute.Equals(counted)) return;
            float change = impact.RealImpact.CurrentValue;
            if (Mathf.Abs(change) < 0.0001f) return;
            OnChange(impact, change);
        }

        /// <summary>A change to its attribute's current value on the entity: a loss below 0, a gain above.</summary>
        protected virtual void OnChange(ImpactData impact, float change) { }

        /// <summary>It hears the entity's callbacks now, as a status bar would add its own.</summary>
        protected virtual void Hooked(GameplayAbilitySystemCallbacks callbacks) { }

        /// <summary>It stops hearing them.</summary>
        protected virtual void Unhooked(GameplayAbilitySystemCallbacks callbacks) { }
    }
}
