using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct EffectRef
    {
        public int Id;
        public string Name;
        
        public bool IsEmpty => string.IsNullOrEmpty(Id.ToString());
        
        public static implicit operator GameplayEffect(EffectRef aref)
        {
            return RuntimeStore.ResolveEffect(aref);
        }
    }
}
