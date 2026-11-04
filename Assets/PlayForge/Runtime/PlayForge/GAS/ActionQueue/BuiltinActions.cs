using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════
    // MODIFY ATTRIBUTE ACTION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Deferred action to modify an attribute. One the system makes for itself, such as a rescale or a lifesteal's heal,
    /// is used again once the queue has run it, so a listener handed one reads it during the call (GAS-P9).
    /// </summary>
    public class ModifyAttributeAction : IRootAction, IReusableAction
    {
        public int Priority { get; set; } = ActionPriority.Normal;
        public bool IsValid => _target != null;

        public string Description => $"ModifyAttribute({_attribute?.GetName() ?? "null"}, {_value.CurrentValue:F2}/{_value.BaseValue:F2})";

        // Set again each time the system rents one. They were readonly, as each change made a new action (GAS-P9)
        private ITarget _target;
        private IAttribute _attribute;
        private SourcedModifiedAttributeValue _value;
        private bool _runEvents;

        // The spares it goes back to once the queue has run it, when the system rented it from them
        private Stack<ModifyAttributeAction> _spares;

        public ModifyAttributeAction(
            ITarget target,
            IAttribute attribute,
            SourcedModifiedAttributeValue value,
            bool runEvents = true,
            int? priority = null)
        {
            _target = target;
            _attribute = attribute;
            _value = value;
            _runEvents = runEvents;
            if (priority.HasValue) Priority = priority.Value;
        }
        
        public void Execute(ISource system)
        {
            _target.TryModifyAttribute(_attribute, _value, _runEvents);
        }

        /// <summary>
        /// An action from <paramref name="spares"/>, or a new one when there are none, set to make this change at the
        /// default priority. It goes back to them once the queue has run it (GAS-P9).
        /// </summary>
        internal static ModifyAttributeAction Rent(Stack<ModifyAttributeAction> spares, ITarget target, IAttribute attribute,
            SourcedModifiedAttributeValue value, bool runEvents)
        {
            var action = spares.Count > 0 ? spares.Pop() : new ModifyAttributeAction(target, attribute, value, runEvents);
            action._target = target;
            action._attribute = attribute;
            action._value = value;
            action._runEvents = runEvents;
            action.Priority = ActionPriority.Normal;
            action._spares = spares;
            return action;
        }

        void IReusableAction.Release()
        {
            var spares = _spares;
            if (spares == null) return;

            // What it held is let go, so a spare keeps nothing alive
            _target = null;
            _attribute = null;
            _value = default;
            _spares = null;
            spares.Push(this);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // APPLY EFFECT ACTION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Deferred action to apply an effect.
    /// </summary>
    public class ApplyEffectAction : IRootAction
    {
        public int Priority { get; set; } = ActionPriority.Normal;
        public bool IsValid => _target != null && _effect != null;
        
        public string Description => $"ApplyEffect({_effect?.GetName() ?? "null"})";
        
        private readonly ITarget _target;
        private readonly GameplayEffect _effect;
        private readonly IEffectOrigin _origin;
        
        public ApplyEffectAction(
            ITarget target,
            GameplayEffect effect,
            IEffectOrigin origin,
            int? priority = null)
        {
            _target = target;
            _effect = effect;
            _origin = origin;
            if (priority.HasValue) Priority = priority.Value;
        }
        
        public void Execute(ISource system)
        {
            var spec = _target.GenerateEffectSpec(_origin, _effect);
            _target.ApplyCarrier(spec);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // REMOVE EFFECT ACTION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Deferred action to remove an effect.
    /// </summary>
    public class RemoveEffectAction : IRootAction
    {
        public int Priority { get; set; } = ActionPriority.Normal;
        public bool IsValid => _target != null && _effect != null;
        
        public string Description => $"RemoveEffect({_effect?.GetName() ?? "null"})";
        
        private readonly ITarget _target;
        private readonly GameplayEffect _effect;
        
        public RemoveEffectAction(
            ITarget target,
            GameplayEffect effect,
            int? priority = null)
        {
            _target = target;
            _effect = effect;
            if (priority.HasValue) Priority = priority.Value;
        }
        
        public void Execute(ISource system)
        {
            _target.RemoveGameplayEffect(_effect);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // EFFECT-SCOPED ACTION BASE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Base class for actions scoped to an effect's lifecycle.
    /// Automatically invalidates if the effect is removed.
    /// </summary>
    public abstract class EffectScopedAction : IRootAction
    {
        public int Priority { get; set; } = ActionPriority.Normal;
        public abstract string Description { get; }
        
        protected readonly IAttributeImpactCarrier carrier;
        private readonly int _effectInstanceId;
        
        /// <summary>
        /// Action is valid only if the source effect is still active.
        /// </summary>
        public virtual bool IsValid
        {
            get
            {
                if (carrier == null) return false;
                
                // Check if the effect derivation is still valid
                var effectOrigin = carrier.GetOrigin();
                return effectOrigin?.IsActive() ?? false;
            }
        }
        
        protected EffectScopedAction(IAttributeImpactCarrier carrier)
        {
            this.carrier = carrier;
        }
        
        public abstract void Execute(ISource system);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // SPECIAL ACTIONS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Simple action that executes a lambda. Use sparingly.
    /// </summary>
    public class LambdaAction : IRootAction
    {
        public int Priority { get; set; }
        public bool IsValid => _isValid?.Invoke() ?? true;
        public string Description { get; }
        
        private readonly Action<ISource> _action;
        private readonly Func<bool> _isValid;
        
        public LambdaAction(
            Action<ISource> action,
            string description = "LambdaAction",
            int priority = ActionPriority.Normal,
            Func<bool> isValid = null)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            Description = description;
            Priority = priority;
            _isValid = isValid;
        }
        
        public void Execute(ISource system)
        {
            if (!_isValid?.Invoke() ?? false) return;
            _action.Invoke(system);
        }
    }
    
    public class LogAction : IRootAction
    {
        public string Log { get; set; }
        public int Priority { get; set; }
        public bool IsValid => _isValid?.Invoke() ?? true;
        public string Description { get; }
        
        private readonly Func<bool> _isValid;
        
        public LogAction(
            string log,
            string description = "LogAction",
            int priority = ActionPriority.Cleanup,
            Func<bool> isValid = null)
        {
            Log = log;
            Description = description;
            Priority = priority;
            _isValid = isValid;
        }
        
        public void Execute(ISource system)
        {
            if (!_isValid?.Invoke() ?? false) return;
            UnityEngine.Debug.Log(Log);
        }
    }
    
    public class DeathAction : IRootAction
    {
        public int Priority => ActionPriority.Critical; // High priority
        public bool IsValid => !_system.IsDead; // Don't die twice
    
        public string Description => "Entity.Death";
    
        private readonly IGameplayAbilitySystem _system;
    
        public DeathAction(IGameplayAbilitySystem system)
        {
            _system = system;
        }
    
        public void Execute(ISource system)
        {
            // Mark as dead (fires callbacks, records in summary)
            _system.MarkDead();
        }
    }
}