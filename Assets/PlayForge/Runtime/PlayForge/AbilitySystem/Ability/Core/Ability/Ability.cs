using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class Ability : IHasReadableDefinition
    {
        public AbilityDefinition Definition;
        public AbilityTags Tags;
        public AbilityProxySpecification Proxy;
        
        [Min(0)] public int StartingLevel = 1;
        [Min(0)] public int MaxLevel = 4;
        public bool IgnoreWhenLevelZero = true;
        
        public GameplayEffect Cost;
        public GameplayEffect Cooldown;

        public AbilitySpec Generate(ISource owner, int level)
        {
            return new AbilitySpec(owner, this, level);
        }

        public string GetName()
        {
            return Definition.Name;
        }
        
        public string GetDescription()
        {
            return Definition.Description;
        }
        public Sprite GetPrimaryIcon()
        {
            return Definition.NormalIcon;
        }

        public override string ToString()
        {
            return Tags.AssetTag.GetName();
        }
    }
}
