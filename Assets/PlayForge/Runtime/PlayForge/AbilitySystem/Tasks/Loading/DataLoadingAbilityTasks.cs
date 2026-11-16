using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    [ForgeMenu("Asset Loaders")]
    public abstract class AbstractSetAssetsTask : AbstractAbilityTask
    {
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
    }
    
    [ForgeMenu(Description = "Puts assets in the ability's data under a key, for its later tasks.")]
    public class SetAssetsTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.DataKey)]  public Tag Tag = Tags.ASSETS;
        public BaseForgeAsset[] Assets;

        public override bool IsCriticalSection => false;

        public override void Prepare(AbilityDataPacket data)
        {
            if (string.IsNullOrEmpty(Tag.Name)) return;
            
            foreach (var asset in Assets)
            {
                if (asset is null) continue;
                data.AddPayload(Tag, asset);
            }
        }
    }
    
    [ForgeMenu(Description = "Puts effects in the ability's data under a key, for its later tasks.")]
    public class SetEffectsTask : AbstractSetAssetsTask
    {
        [ForgeTagContext(ForgeContext.DataKey)]  public Tag Tag = Tags.EFFECTS;
        public GameplayEffect[] Effects;

        public override bool IsCriticalSection => false;
        
        public override void Prepare(AbilityDataPacket data)
        {
            if (string.IsNullOrEmpty(Tag.Name)) return;
            foreach (var asset in Effects)
            {
                if (asset is null) continue;
                data.AddPayload(Tag, asset);
            }
        }
    }
    
    [ForgeMenu(Description = "Puts entities in the ability's data under a key, for its later tasks.")]
    public class SetEntitiesTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.DataKey)]  public Tag Tag = Tags.ENTITIES;
        public EntityIdentity[] Entities;

        public override bool IsCriticalSection => false;
        
        public override void Prepare(AbilityDataPacket data)
        {
            if (string.IsNullOrEmpty(Tag.Name)) return;
            foreach (var asset in Entities)
            {
                if (asset is null) continue;
                data.AddPayload(Tag, asset);
            }
        }
    }
    
    [ForgeMenu(Description = "Puts attributes in the ability's data under a key, for its later tasks.")]
    public class SetAttributesTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.DataKey)]  public Tag Tag = Tags.ATTRIBUTES;
        public Attribute[] Attributes;

        public override bool IsCriticalSection => false;
        
        public override void Prepare(AbilityDataPacket data)
        {
            if (string.IsNullOrEmpty(Tag.Name)) return;
            foreach (var asset in Attributes)
            {
                if (asset is null) continue;
                data.AddPayload(Tag, asset);
            }
        }
    }
    
    [ForgeMenu(Description = "Puts attribute sets in the ability's data under a key, for its later tasks.")]
    public class SetAttributeSetsTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.DataKey)]  public Tag Tag = Tags.ATTRIBUTE_SETS;
        public AttributeSet[] AttributeSets;

        public override bool IsCriticalSection => false;
        
        public override void Prepare(AbilityDataPacket data)
        {
            if (string.IsNullOrEmpty(Tag.Name)) return;
            foreach (var asset in AttributeSets)
            {
                if (asset is null) continue;
                data.AddPayload(Tag, asset);
            }
        }
    }
    
    [ForgeMenu(Description = "Puts abilities in the ability's data under a key, for its later tasks.")]
    public class SetAbilitiesTask : AbstractSetAssetsTask
    {
        [ForgeTagContext(ForgeContext.DataKey)]  public Tag Tag = Tags.ABILITIES;
        public Ability[] Abilities;

        public override bool IsCriticalSection => false;
        
        public override void Prepare(AbilityDataPacket data)
        {
            if (string.IsNullOrEmpty(Tag.Name)) return;
            foreach (var asset in Abilities)
            {
                if (asset is null) continue;
                data.AddPayload(Tag, asset);
            }
        }
    }
    
    [ForgeMenu(Description = "Puts tags in the ability's data under a key, for its later tasks.")]
    public class SetTagsTask : AbstractSetAssetsTask
    {
        [ForgeTagContext(ForgeContext.DataKey)]  public Tag Tag = PlayForge.Tags.TAGS;
        [SerializeReference] public Tag[] Tags;

        public override bool IsCriticalSection => false;
        
        public override void Prepare(AbilityDataPacket data)
        {
            if (string.IsNullOrEmpty(Tag.Name)) return;
            foreach (var asset in Tags)
            {
                data.AddPayload(Tag, asset);
            }
        }
    }
}
