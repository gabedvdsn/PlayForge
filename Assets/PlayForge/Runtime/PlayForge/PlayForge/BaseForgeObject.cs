using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class BaseForgeObject : ScriptableObject, IHasReadableDefinition, ITagSource
    {
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        
        public bool TryGetLocalData(Tag key, out DataWrapper data)
        {
            data = LocalData.FirstOrDefault(ld => ld.Key == key);
            return data != null;
        }
        
        public abstract IEnumerable<Tag> GetGrantedTags();
        public abstract string GetName();
        public abstract string GetDescription();
        public abstract Texture2D GetPrimaryIcon();
    }
}
