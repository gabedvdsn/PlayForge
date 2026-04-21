using UnityEngine;
using UnityEngine.UI;

namespace FarEmerald.PlayForge
{
    public class Healthbar : LazyMonoProcess
    {
        public Attribute Attribute;
        public Image HealthBar;
        public GameplayAbilitySystem Gas;
        
        public override void WhenUpdate()
        {
            Gas.TryGetAttributeValue(Attribute, out var value);
            HealthBar.fillAmount = value.RatioMinZero;
        }
    }

}