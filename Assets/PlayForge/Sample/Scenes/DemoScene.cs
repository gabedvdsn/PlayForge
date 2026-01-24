using System;
using System.Collections.Generic;
using FarEmerald.PlayForge;
using UnityEngine;
using UnityEngine.UIElements;
using Attribute = FarEmerald.PlayForge.Attribute;

public class DemoScene : MonoBehaviour
{
    public UIDocument DemoUI;
    public GameplayAbilitySystem Source;
    public Attribute Health;
    public Attribute Mana;

    private VisualElement Root;

    private List<VisualElement> Containers;
    private AbilitySpecContainer[] Abilities;

    private Slider HealthSlider;
    private Slider ManaSlider;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        Root = DemoUI.rootVisualElement;

        Abilities = new AbilitySpecContainer[4];
        for (int i = 0; i < 4; i++)
        {
            Abilities[i] = null;
        }
        
        Containers = Root.Q("AbilityBar").Query("AbilityContainer").ToList();

        HealthSlider = Root.Q<Slider>("Health");
        ManaSlider = Root.Q<Slider>("Mana");
    }

    private void Start()
    {
        var abilities = Source.GetAbilitySystem().GetAbilityContainers();
        for (int i = 0; i < 4; i++)
        {
            if (abilities.Count <= i) continue;

            var ability = abilities[i];
            Containers[i].style.display = DisplayStyle.Flex;
            AddAbility(i, ability);

        }
    }

    public void AddAbility(int index, AbilitySpecContainer spec)
    {
        if (index < 0 || index > 3) return;
        
        Abilities[index] = spec;
        var c = Containers[index];
        
        var img = c.Q("Image");
        img.style.backgroundImage = spec.Spec.Base.GetPrimaryIcon();
        
        var cooldown = Containers[index].Q<Slider>("Cooldown");
        cooldown.lowValue = 0f;
        cooldown.highValue = 1f;
        cooldown.style.display = DisplayStyle.None;
    }

    void TryActivateAbility(int index)
    {
        if (Abilities[index] is null) return;

        var asc = Source.GetAbilitySystem();
        asc.TryActivateAbility(new AbilitySystemComponent.AbilityActivationRequest(Abilities[index].Spec, index, asc));
    }

    private void Update()
    {
        if (Source is null)
        {
            Debug.Log($"Source is null");
            return;
        }
        
        if (Input.GetKeyDown(KeyCode.Q)) TryActivateAbility(0);
        if (Input.GetKeyDown(KeyCode.W)) TryActivateAbility(1);
        if (Input.GetKeyDown(KeyCode.E)) TryActivateAbility(2);
        if (Input.GetKeyDown(KeyCode.R)) TryActivateAbility(3);
        
        for (int i = 0; i < 4; i++)
        {
            if (Abilities[i] is null) continue;

            var dur = Source.GetLongestDurationFor(Abilities[i].Spec.Base.Tags.AssetTag);
            
            var slider = Containers[i].Q<Slider>("Cooldown");
            if (!dur.FoundDuration)
            {
                slider.style.display = DisplayStyle.None;
                continue;
            }
            
            
            slider.style.display = DisplayStyle.Flex;
            slider.value = dur.Ratio;
        }

        HealthSlider.value = Source.TryGetAttributeValue(Health, out var health) ? health.Ratio : 0f;
        ManaSlider.value = Source.TryGetAttributeValue(Mana, out var mana) ? mana.Ratio : 0f;
    }
}
