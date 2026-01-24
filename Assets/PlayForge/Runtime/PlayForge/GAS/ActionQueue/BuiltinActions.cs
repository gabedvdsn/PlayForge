using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════
    // MODIFY ATTRIBUTE ACTION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Deferred action to modify an attribute.
    /// </summary>
    public class ModifyAttributeAction : IRootAction
    {
        public int Priority { get; set; } = ActionPriority.Normal;
        public bool IsValid => _target != null;
        
        public string Description => $"ModifyAttribute({_attribute?.Name ?? "null"}, {_value.CurrentValue:F2}/{_value.BaseValue:F2})";
        
        private readonly ITarget _target;
        private readonly Attribute _attribute;
        private readonly SourcedModifiedAttributeValue _value;
        private readonly bool _runEvents;
        
        public ModifyAttributeAction(
            ITarget target,
            Attribute attribute,
            SourcedModifiedAttributeValue value,
            bool runEvents = true,
            int? priority = null)
        {
            Debug.Log($"Modify attribute action created : runEvents={runEvents} ({value})");
            _target = target;
            _attribute = attribute;
            _value = value;
            _runEvents = runEvents;
            if (priority.HasValue) Priority = priority.Value;
        }
        
        public void Execute(IGameplayAbilitySystem system)
        {
            _target.TryModifyAttribute(_attribute, _value, _runEvents);
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
        
        public void Execute(IGameplayAbilitySystem system)
        {
            var spec = _target.GenerateEffectSpec(_origin, _effect);
            _target.ApplyGameplayEffect(spec);
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
        
        public void Execute(IGameplayAbilitySystem system)
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
        
        protected readonly IAttributeImpactDerivation Derivation;
        private readonly int _effectInstanceId;
        
        /// <summary>
        /// Action is valid only if the source effect is still active.
        /// </summary>
        public virtual bool IsValid
        {
            get
            {
                if (Derivation == null) return false;
                
                // Check if the effect derivation is still valid
                var effectOrigin = Derivation.GetEffectDerivation();
                return effectOrigin?.IsActive() ?? false;
            }
        }
        
        protected EffectScopedAction(IAttributeImpactDerivation derivation)
        {
            Derivation = derivation;
        }
        
        public abstract void Execute(IGameplayAbilitySystem system);
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
        
        private readonly Action<IGameplayAbilitySystem> _action;
        private readonly Func<bool> _isValid;
        
        public LambdaAction(
            Action<IGameplayAbilitySystem> action,
            string description = "LambdaAction",
            int priority = ActionPriority.Normal,
            Func<bool> isValid = null)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            Description = description;
            Priority = priority;
            _isValid = isValid;
        }
        
        public void Execute(IGameplayAbilitySystem system)
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
        
        public void Execute(IGameplayAbilitySystem system)
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
        private readonly bool _destroyOnDeath;
    
        public DeathAction(IGameplayAbilitySystem system, bool destroyOnDeath = true)
        {
            _system = system;
            _destroyOnDeath = destroyOnDeath;
        }
    
        public void Execute(IGameplayAbilitySystem system)
        {
            
            // Mark as dead (fires callbacks, records in summary)
            _system.ToGASObject()?.MarkDead();
        
            // Optionally queue destruction
            if (_destroyOnDeath)
            {
                _system.ToGASObject()?.QueueDestruction();
            }
        }
    }
}