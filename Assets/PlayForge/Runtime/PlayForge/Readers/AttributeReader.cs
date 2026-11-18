using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A reader that shows one of a GAS's attributes in UI: its current and base values in <c>CurrentText</c> and
    /// <c>BaseText</c>, and its current value's share of its base on <c>ValueSlider</c>. <c>AssignAttribute</c> names
    /// the attribute and <c>Assign</c> the GAS; it shows them each Update step, and as the attribute changes.
    /// </summary>
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
            if (!Source.TryGetAttributeValue(attribute, out var attributeValue)) return;

            Set(attributeValue);
        }

        protected override void SubscribeIfOnChangePolicy()
        {
            Source.GetAttributeSystem().Callbacks.OnAttributeChanged += data =>
            {
                if (data.Attribute.Equals(attribute)) Set(Source.TryGetAttributeValue(attribute, out var value) ? value : default);
            };
        }

        private void Set(AttributeValue attributeValue)
        {
            CurrentText.text = $"{attributeValue.CurrentValue:0.0}";
            BaseText.text = $"{attributeValue.BaseValue:0.0}";
            ValueSlider.value = attributeValue.ClampedRatio;
        }

        public void AssignAttribute(IAttribute attr)
        {
            attribute = attr;
        }
    }
}
