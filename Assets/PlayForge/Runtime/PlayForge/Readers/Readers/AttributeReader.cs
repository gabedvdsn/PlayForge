using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarEmerald.PlayForge
{
    public class AttributeReader : AbstractReader, IAttributeReader
    {
        [Header("Reader")]
        
        public TMP_Text CurrentText;
        public TMP_Text BaseText;
        public Slider ValueSlider;

        private Attribute attribute;

        public override void WhenUpdate(ProcessRelay relay)
        {
            if (Source is null) return;
            if (!Source.GetAttributeSystem().TryGetAttributeValue(attribute, out AttributeValue attributeValue)) return;

            Set(attributeValue);
        }

        protected override void SubscribeIfOnChangePolicy()
        {
            Source.GetAttributeSystem().Callbacks.OnAttributeChanged += data =>
            {
                if (data.Attribute.Equals(attribute)) Set(Source.GetAttributeSystem().TryGetAttributeValue(attribute, out AttributeValue value) ? value : default);
            };
        }

        private void Set(AttributeValue attributeValue)
        {
            CurrentText.text = $"{attributeValue.CurrentValue:0.0}";
            BaseText.text = $"{attributeValue.BaseValue:0.0}";
            ValueSlider.value = attributeValue.Ratio;
        }

        public void AssignAttribute(Attribute attr)
        {
            attribute = attr;
        }
    }

    public interface ISystemReader
    {
        
        public void Assign(IGameplayAbilitySystem gas);
    }

    public interface IAttributeReader
    {
        public void AssignAttribute(Attribute attr);
    }

    public interface ITagReader
    {
        public void Assign(Tag _target);
    }

    public interface ITagReadable
    {
        public ITagReadableReport Read();
    }

    public interface ITagReadableReport
    {
        
    }

    public enum EReaderPolicy
    {
        OnChange,  // Recommended
        UseProcessTimIng
    }
}
