using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class AbilityDefinition
    {
        public string Name;
        public string Description;
        
        public EAbilityActivationPolicyExtended ActivationPolicy = EAbilityActivationPolicyExtended.SingleActive; 
        public bool ActivateImmediately;

        
        public List<TextureItem> Textures;
    }
}
