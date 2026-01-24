using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Callbacks for ability system events.
    /// Used for monitoring ability lifecycle without modifying behavior.
    /// </summary>
    public class AbilitySystemCallbacks
    {
        /*
         * Callback categories:
         * - Ability lifecycle (activate, end)
         * - Ability injection
         * - Task lifecycle (activate, end)
         * - Stage lifecycle (activate, end)
         */
        
        public delegate void AbilitySystemCallbackDelegate(AbilityCallbackStatus status);
        
        // ═══════════════════════════════════════════════════════════════
        // ABILITY LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Ability Activate
        private AbilitySystemCallbackDelegate _onAbilityActivate;
        public event AbilitySystemCallbackDelegate OnAbilityActivate
        {
            add => _onAbilityActivate = AddUnique(_onAbilityActivate, value);
            remove => _onAbilityActivate -= value;
        }
        public void AbilityActivated(AbilityCallbackStatus status) => _onAbilityActivate?.Invoke(status);
        #endregion
        
        #region On Ability End
        private AbilitySystemCallbackDelegate _onAbilityEnd;
        public event AbilitySystemCallbackDelegate OnAbilityEnd
        {
            add => _onAbilityEnd = AddUnique(_onAbilityEnd, value);
            remove => _onAbilityEnd -= value;
        }
        public void AbilityEnded(AbilityCallbackStatus status) => _onAbilityEnd?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // ABILITY INJECTION
        // ═══════════════════════════════════════════════════════════════
        
        #region On Ability Injection
        private AbilitySystemCallbackDelegate _onAbilityInjection;
        public event AbilitySystemCallbackDelegate OnAbilityInjection
        {
            add => _onAbilityInjection = AddUnique(_onAbilityInjection, value);
            remove => _onAbilityInjection -= value;
        }
        public void AbilityInjected(AbilityCallbackStatus status) => _onAbilityInjection?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // TASK LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Task Activate
        private AbilitySystemCallbackDelegate _onAbilityTaskActivate;
        public event AbilitySystemCallbackDelegate OnAbilityTaskActivate
        {
            add => _onAbilityTaskActivate = AddUnique(_onAbilityTaskActivate, value);
            remove => _onAbilityTaskActivate -= value;
        }
        public void AbilityTaskActivated(AbilityCallbackStatus status) => _onAbilityTaskActivate?.Invoke(status);
        #endregion
        
        #region On Task End
        private AbilitySystemCallbackDelegate _onAbilityTaskEnd;
        public event AbilitySystemCallbackDelegate OnAbilityTaskEnd
        {
            add => _onAbilityTaskEnd = AddUnique(_onAbilityTaskEnd, value);
            remove => _onAbilityTaskEnd -= value;
        }
        public void AbilityTaskEnded(AbilityCallbackStatus status) => _onAbilityTaskEnd?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // STAGE LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Stage Start
        private AbilitySystemCallbackDelegate _onAbilityStageStart;
        public event AbilitySystemCallbackDelegate OnAbilityStageActivate
        {
            add => _onAbilityStageStart = AddUnique(_onAbilityStageStart, value);
            remove => _onAbilityStageStart -= value;
        }
        public void AbilityStageActivated(AbilityCallbackStatus status) => _onAbilityStageStart?.Invoke(status);
        #endregion
        
        #region On Stage End
        private AbilitySystemCallbackDelegate _onAbilityStageEnd;
        public event AbilitySystemCallbackDelegate OnAbilityStageEnd
        {
            add => _onAbilityStageEnd = AddUnique(_onAbilityStageEnd, value);
            remove => _onAbilityStageEnd -= value;
        }
        public void AbilityStageEnded(AbilityCallbackStatus status) => _onAbilityStageEnd?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // HELPER
        // ═══════════════════════════════════════════════════════════════
        
        private static AbilitySystemCallbackDelegate AddUnique(AbilitySystemCallbackDelegate existing, AbilitySystemCallbackDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
    }
}