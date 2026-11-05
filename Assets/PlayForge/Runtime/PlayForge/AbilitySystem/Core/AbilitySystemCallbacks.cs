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
        public void AbilityClaimed(AbilityCallbackStatus status) => _onAbilityActivate?.Invoke(status);
        #endregion
        
        #region On Ability End
        // When the activation is over. This fired when the claim was let go, which for an ability with no critical
        // stage is as it starts (TS-9)
        private AbilitySystemCallbackDelegate _onAbilityEnd;
        public event AbilitySystemCallbackDelegate OnAbilityEnd
        {
            add => _onAbilityEnd = AddUnique(_onAbilityEnd, value);
            remove => _onAbilityEnd -= value;
        }
        public void AbilityEnded(AbilityCallbackStatus status) => _onAbilityEnd?.Invoke(status);
        #endregion

        #region On Ability Claim Released
        // When the ability lets other activations in, which is as its last critical stage ends
        private AbilitySystemCallbackDelegate _onAbilityClaimReleased;
        public event AbilitySystemCallbackDelegate OnAbilityClaimReleased
        {
            add => _onAbilityClaimReleased = AddUnique(_onAbilityClaimReleased, value);
            remove => _onAbilityClaimReleased -= value;
        }
        public void AbilityClaimReleased(AbilityCallbackStatus status) => _onAbilityClaimReleased?.Invoke(status);
        #endregion

        #region On Gameplay Event
        /// <summary>A gameplay event the entity was sent, with its data (FEAT-4).</summary>
        public delegate void GameplayEventDelegate(Tag eventTag, ProcessDataPacket data);

        // Before the abilities it triggers try to activate
        private GameplayEventDelegate _onGameplayEvent;
        public event GameplayEventDelegate OnGameplayEvent
        {
            add
            {
                if (_onGameplayEvent == null || Array.IndexOf(_onGameplayEvent.GetInvocationList(), value) == -1) _onGameplayEvent += value;
            }
            remove => _onGameplayEvent -= value;
        }
        public void GameplayEventReceived(Tag eventTag, ProcessDataPacket data) => _onGameplayEvent?.Invoke(eventTag, data);
        #endregion

        #region On Ability Refused
        /// <summary>A refused activation, with why it was refused (FEAT-17).</summary>
        public delegate void AbilityRefusedDelegate(ActivationRefusal refusal);

        // A request refused as it was asked for. A request waiting in the input buffer isn't told again as it's retried
        private AbilityRefusedDelegate _onAbilityRefused;
        public event AbilityRefusedDelegate OnAbilityRefused
        {
            add
            {
                if (_onAbilityRefused == null || Array.IndexOf(_onAbilityRefused.GetInvocationList(), value) == -1) _onAbilityRefused += value;
            }
            remove => _onAbilityRefused -= value;
        }
        public void AbilityRefused(ActivationRefusal refusal) => _onAbilityRefused?.Invoke(refusal);
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