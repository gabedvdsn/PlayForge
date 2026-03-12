using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class BaseForgeAsset : ScriptableObject, IHasReadableDefinition, ITagSource
    {
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        public BaseForgeAsset Template { get; private set; }
        public readonly List<BaseForgeAsset> TemplateFor = new();
        
        public bool TryGetLocalData(Tag key, out DataWrapper data)
        {
            data = LocalData.FirstOrDefault(ld => ld.Key == key);
            return data != null;
        }

        public bool HasTemplate => Template is not null;

        public void SetTemplate(BaseForgeAsset template)
        {
            if (GetType() != template.GetType()) return;

            Template = template;
            template.SetTemplateFor(this);
        }

        public void SetTemplateFor(BaseForgeAsset other)
        {
            TemplateFor.Add(other);
        }
        
        public T TemplateAs<T>() where T : BaseForgeAsset
        {
            if (!Template) return null;
            return Template as T;
        }
        
        public abstract IEnumerable<Tag> GetGrantedTags();
        public abstract string GetName();
        public abstract string GetDescription();
        public abstract Texture2D GetPrimaryIcon();
    }
}
