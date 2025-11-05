using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct AttributeRef
    {
        public int Id;
        public string Name;
        
        public bool IsEmpty => string.IsNullOrEmpty(Id.ToString());

        public Attribute Resolve => this;
        
        public static implicit operator Attribute(AttributeRef aref)
        {
            return RuntimeStore.ResolveAttribute(aref);
        }
    }
}
