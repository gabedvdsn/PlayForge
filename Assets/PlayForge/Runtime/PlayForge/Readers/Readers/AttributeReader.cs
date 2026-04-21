using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarEmerald.PlayForge
{
    public class AttributeReader : AbstractReader
    {
        [Header("Reader")]
        
        public TMP_Text CurrentText;
        public TMP_Text BaseText;
        public Slider ValueSlider;

        private IAttribute attribute;

        public override void WhenUpdate()
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
            ValueSlider.value = attributeValue.RatioMinZero;
        }

        public void AssignAttribute(IAttribute attr)
        {
            attribute = attr;
        }
    }
}
