using System;

namespace FarEmerald.PlayForge
{
    public class AbilitySystemCallbacks
    {
        /*
         * On ability activate (any/specific)
         * On ability end (any/specific)
         * On ability (any/all) injection (any/specific)
         * On ability (any/all) task activate (any/specific)
         * On ability (any/all) task deactivate (any/specific)
         *
         * Ability status packet
         * - Ability
         * - Task
         * - Stage
         * - Injection
         */
        
        public delegate void AbilitySystemCallbackDelegate(AbilityCallbackStatus status);

        #region On Ability Activate
        private AbilitySystemCallbackDelegate _onAbilityActivate;
        public event AbilitySystemCallbackDelegate OnAbilityActivate
        {
            add
            {
                if (Array.IndexOf(_onAbilityActivate.GetInvocationList(), value) == -1) _onAbilityActivate += value;
            }
            remove => _onAbilityActivate -= value;
        }
        public void AbilityActivated(AbilityCallbackStatus status) => _onAbilityActivate?.Invoke(status);
        #endregion
        
        #region On Ability End
        private AbilitySystemCallbackDelegate _onAbilityEnd;
        public event AbilitySystemCallbackDelegate OnAbilityEnd
        {
            add
            {
                if (Array.IndexOf(_onAbilityEnd.GetInvocationList(), value) == -1) _onAbilityEnd += value;
            }
            remove => _onAbilityEnd -= value;
        }
        public void AbilityEnded(AbilityCallbackStatus status) => _onAbilityEnd?.Invoke(status);
        #endregion
        
        #region On Ability Injection
        private AbilitySystemCallbackDelegate _onAbilityInjection;
        public event AbilitySystemCallbackDelegate OnAbilityInjection
        {
            add
            {
                if (Array.IndexOf(_onAbilityInjection.GetInvocationList(), value) == -1) _onAbilityInjection += value;
            }
            remove => _onAbilityInjection -= value;
        }
        public void AbilityInjected(AbilityCallbackStatus status) => _onAbilityInjection?.Invoke(status);
        #endregion
        
        #region On Task Activate
        private AbilitySystemCallbackDelegate _onAbilityTaskActivate;
        public event AbilitySystemCallbackDelegate OnAbilityTaskActivate
        {
            add
            {
                if (Array.IndexOf(_onAbilityTaskActivate.GetInvocationList(), value) == -1) _onAbilityTaskActivate += value;
            }
            remove => _onAbilityTaskActivate -= value;
        }
        public void AbilityTaskActivated(AbilityCallbackStatus status) => _onAbilityTaskActivate?.Invoke(status);
        #endregion
        
        #region On Task End
        private AbilitySystemCallbackDelegate _onAbilityTaskEnd;
        public event AbilitySystemCallbackDelegate OnAbilityTaskEnd
        {
            add
            {
                if (Array.IndexOf(_onAbilityTaskEnd.GetInvocationList(), value) == -1) _onAbilityTaskEnd += value;
            }
            remove => _onAbilityTaskEnd -= value;
        }
        public void AbilityTaskEnded(AbilityCallbackStatus status) => _onAbilityTaskEnd?.Invoke(status);
        #endregion
        
        #region On Stage Start
        private AbilitySystemCallbackDelegate _onAbilityStageStart;
        public event AbilitySystemCallbackDelegate OnAbilityStageActivate
        {
            add
            {
                if (Array.IndexOf(_onAbilityStageStart.GetInvocationList(), value) == -1) _onAbilityStageStart += value;
            }
            remove => _onAbilityStageStart -= value;
        }

        public void AbilityStageActivated(AbilityCallbackStatus status)
        {
            _onAbilityStageStart?.Invoke(status);
        }
        #endregion
        
        #region On Stage End
        private AbilitySystemCallbackDelegate _onAbilityStageEnd;
        public event AbilitySystemCallbackDelegate OnAbilityStageEnd
        {
            add
            {
                if (Array.IndexOf(_onAbilityStageEnd.GetInvocationList(), value) == -1) _onAbilityStageEnd += value;
            }
            remove => _onAbilityStageEnd -= value;
        }

        public void AbilityStageEnded(AbilityCallbackStatus status)
        {
            _onAbilityStageEnd?.Invoke(status);
        }
        #endregion
    }
}
