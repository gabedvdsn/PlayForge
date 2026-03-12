using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Callbacks for level system events.
    /// Used for monitoring level changes without modifying behavior.
    /// </summary>
    public class LevelSystemCallbacks
    {
        /*
         * Callback categories:
         * - Registration lifecycle (registered, unregistered)
         * - Level changes (level changed, max level changed)
         */
        
        public delegate void LevelSystemCallbackDelegate(LevelCallbackStatus status);
        
        // ═══════════════════════════════════════════════════════════════
        // REGISTRATION LIFECYCLE
        // ═══════════════════════════════════════════════════════════════
        
        #region On Level Registered
        private LevelSystemCallbackDelegate _onLevelRegistered;
        public event LevelSystemCallbackDelegate OnLevelRegistered
        {
            add => _onLevelRegistered = AddUnique(_onLevelRegistered, value);
            remove => _onLevelRegistered -= value;
        }
        public void LevelRegistered(LevelCallbackStatus status) => _onLevelRegistered?.Invoke(status);
        #endregion
        
        #region On Level Unregistered
        private LevelSystemCallbackDelegate _onLevelUnregistered;
        public event LevelSystemCallbackDelegate OnLevelUnregistered
        {
            add => _onLevelUnregistered = AddUnique(_onLevelUnregistered, value);
            remove => _onLevelUnregistered -= value;
        }
        public void LevelUnregistered(LevelCallbackStatus status) => _onLevelUnregistered?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // LEVEL CHANGES
        // ═══════════════════════════════════════════════════════════════
        
        #region On Level Changed
        private LevelSystemCallbackDelegate _onLevelChanged;
        public event LevelSystemCallbackDelegate OnLevelChanged
        {
            add => _onLevelChanged = AddUnique(_onLevelChanged, value);
            remove => _onLevelChanged -= value;
        }
        public void LevelChanged(LevelCallbackStatus status) => _onLevelChanged?.Invoke(status);
        #endregion
        
        #region On Max Level Changed
        private LevelSystemCallbackDelegate _onMaxLevelChanged;
        public event LevelSystemCallbackDelegate OnMaxLevelChanged
        {
            add => _onMaxLevelChanged = AddUnique(_onMaxLevelChanged, value);
            remove => _onMaxLevelChanged -= value;
        }
        public void MaxLevelChanged(LevelCallbackStatus status) => _onMaxLevelChanged?.Invoke(status);
        #endregion
        
        #region On Level Up
        private LevelSystemCallbackDelegate _onLevelUp;
        public event LevelSystemCallbackDelegate OnLevelUp
        {
            add => _onLevelUp = AddUnique(_onLevelUp, value);
            remove => _onLevelUp -= value;
        }
        public void LevelUp(LevelCallbackStatus status) => _onLevelUp?.Invoke(status);
        #endregion
        
        #region On Level Down
        private LevelSystemCallbackDelegate _onLevelDown;
        public event LevelSystemCallbackDelegate OnLevelDown
        {
            add => _onLevelDown = AddUnique(_onLevelDown, value);
            remove => _onLevelDown -= value;
        }
        public void LevelDown(LevelCallbackStatus status) => _onLevelDown?.Invoke(status);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // HELPER
        // ═══════════════════════════════════════════════════════════════
        
        private static LevelSystemCallbackDelegate AddUnique(LevelSystemCallbackDelegate existing, LevelSystemCallbackDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
    }
}
