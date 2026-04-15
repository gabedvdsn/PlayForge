using System.Collections.Generic;
using FarEmerald.PlayForge;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayForgeTesting : MonoBehaviour
{
    public Slider HealthSlider;
    public TMP_Text AttributeText;
    public Attribute SliderAttribute;
    
    public GameplayAbilitySystem Target;
    public List<GameplayEffect> Effects;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        /*if (Target.TryGetAttributeValue(SliderAttribute, out AttributeValue value))
        {
            HealthSlider.value = Mathf.Lerp(HealthSlider.value, value.Ratio, Time.deltaTime * 5f);
            AttributeText.text = $"{value.CurrentValue} / {value.BaseValue}";
        }
        
        if (Input.GetKeyDown(KeyCode.Q) && Effects.Count > 0)
        {
            Target.ApplyGameplayEffect(Effects[0].Generate(IEffectOrigin.GenerateSourceDerivation(GameRoot.Instance), GameRoot.Instance));
        }
        
        if (Input.GetKeyDown(KeyCode.W) && Effects.Count > 1)
        {
            Target.ApplyGameplayEffect(Effects[1].Generate(IEffectOrigin.GenerateSourceDerivation(GameRoot.Instance), GameRoot.Instance));
        }
        
        if (Input.GetKeyDown(KeyCode.E) && Effects.Count > 2)
        {
            Target.ApplyGameplayEffect(Effects[2].Generate(IEffectOrigin.GenerateSourceDerivation(GameRoot.Instance), GameRoot.Instance));
        }
        
        if (Input.GetKeyDown(KeyCode.R) && Effects.Count > 3)
        {
            Target.ApplyGameplayEffect(Effects[3].Generate(IEffectOrigin.GenerateSourceDerivation(GameRoot.Instance), GameRoot.Instance));
        }*/
    }
}
