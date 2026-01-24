using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractGameplayMonoProcess : LazyMonoProcess
    {
        public List<TagAttributePair> AttributePairs = new();
        
        protected IEffectOrigin Origin;
        protected SystemComponentData Source;

        public override void WhenInitialize(ProcessRelay relay)
        {
            base.WhenInitialize(relay);
            
            if (!regData.TryGet(Tags.DERIVATION, EProxyDataValueTarget.Primary, out Origin))
            {
                Origin = GameRoot.Instance;
            }

            Source = Origin.GetOwner().AsData();
        }

        protected CachedAttributeValue GetAttributeValue(Tag key)
        {
            foreach (var pair in AttributePairs)
            {
                if (pair.Key == key && Source.AttributeSystem.TryGetAttributeValue(
                        pair.Attribute,
                        out CachedAttributeValue value)
                   ) return value;
            }

            return CachedAttributeValue.GenerateNull();
        }

        public virtual void ReportStatus(Tag status)
        {
            string message = $"({status}): {Origin}";
            
            if (status == Tags.FAILED_TO_INITIALIZE) throw new InvalidOperationException($"({Origin}");
            if (status == Tags.FAILED_WHILE_ACTIVE) throw new InvalidOperationException($"({Origin}");
        }
    }

    [Serializable]
    public struct TagAttributePair
    {
        public Tag Key;
        public Attribute Attribute;

        public TagAttributePair(Tag key, Attribute attribute)
        {
            Key = key;
            Attribute = attribute;
        }
    }
}
