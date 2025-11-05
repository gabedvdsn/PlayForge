using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct AttributeSetRef
    {
        public int Id;
        public string Name;
        
        public bool IsEmpty => string.IsNullOrEmpty(Id.ToString());
        
        public static implicit operator AttributeSet(AttributeSetRef aref)
        {
            return RuntimeStore.ResolveAttributeSet(aref);
        }
    }
}
