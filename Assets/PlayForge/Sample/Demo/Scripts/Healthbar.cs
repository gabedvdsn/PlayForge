using UnityEngine;
using UnityEngine.UI;

namespace FarEmerald.PlayForge
{
    public class Healthbar : LazyMonoProcess
    {
        public Attribute Attribute;
        public Image HealthBar;
        public GameplayAbilitySystem Gas;

        private Canvas canvas;

        public override void WhenInitialize()
        {
            foreach (var img in GetComponentsInChildren<Image>())
            {
                img.transform.LookAt(Camera.main.transform);
            }
        }

        public override void WhenUpdate()
        {
            Gas.TryGetAttributeValue(Attribute, out var value);
            HealthBar.fillAmount = value.ClampedRatio;
        }
    }

}