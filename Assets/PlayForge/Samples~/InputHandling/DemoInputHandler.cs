using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace FESGameplayAbilitySystem.Demo
{
    public class DemoInputHandler : MonoBehaviour
    {
        [HideInInspector] public GASComponent System;
        public List<KeyCode> AbilityKeyMaps;

        private void Update()
        {
            for (int i = 0; i < AbilityKeyMaps.Count; i++)
            {
                if (Input.GetKeyDown(AbilityKeyMaps[i])) System.AbilitySystem.TryActivateAbility(System.AbilitySystem.CreateActivationRequest(i));
            }
        }
    }
}
