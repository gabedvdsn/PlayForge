using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct TagRef
    {
        public int Id;
        public string Name;
        
        public bool IsEmpty => string.IsNullOrEmpty(Id.ToString());
        
        public static implicit operator Tag(TagRef aref)
        {
            return RuntimeStore.ResolveTag(aref);
        }
    }
}
