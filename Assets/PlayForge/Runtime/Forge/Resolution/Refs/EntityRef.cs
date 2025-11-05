using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct EntityRef
    {
        public int Id;
        public string Name;
        
        public bool IsEmpty => string.IsNullOrEmpty(Id.ToString());
        
        public static implicit operator EntityIdentity(EntityRef aref)
        {
            return RuntimeStore.ResolveEntity(aref);
        }
    }
}
