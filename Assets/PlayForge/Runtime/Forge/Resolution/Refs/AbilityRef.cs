using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct AbilityRef
    {
        public int Id;
        public string Name;
        
        public bool IsEmpty => string.IsNullOrEmpty(Id.ToString());
        
        public static implicit operator Ability(AbilityRef aref)
        {
            return RuntimeStore.ResolveAbility(aref);
        }
    }
}
