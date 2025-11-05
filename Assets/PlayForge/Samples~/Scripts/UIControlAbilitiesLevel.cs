using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace FESGameplayAbilitySystem.Demo
{
    public class UIControlAbilitiesLevel : MonoBehaviour
    {
        public TMP_Text LevelText;
        public GASComponent Source;

        private int level = 1;

        private void Awake()
        {
            LevelText.text = level.ToString();
        }

        public void IncrementLevel(int amount)
        {
            level += amount;
            Source.AbilitySystem.SetAbilitiesLevel(level);
            LevelText.text = level.ToString();
        }
    }
}
