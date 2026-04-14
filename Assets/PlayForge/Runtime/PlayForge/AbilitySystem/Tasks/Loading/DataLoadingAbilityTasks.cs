using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractSetAssetsTask : AbstractAbilityTask
    {
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
    }
    
    public class SetAssetsTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.Data)]  public Tag Tag = Tags.ASSETS;
        [SerializeReference] public BaseForgeAsset[] Assets;

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
    
    public class SetEffectsTask : AbstractSetAssetsTask
    {
        [ForgeTagContext(ForgeContext.Data)]  public Tag Tag = Tags.EFFECTS;
        [SerializeReference] public GameplayEffect[] Effects;

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
    
    public class SetEntitiesTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.Data)]  public Tag Tag = Tags.ENTITIES;
        [SerializeReference] public EntityIdentity[] Entities;

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
    
    public class SetAttributesTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.Data)]  public Tag Tag = Tags.ATTRIBUTES;
        [SerializeReference] public BaseForgeAsset[] Attributes;

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
    
    public class SetAttributeSetsTask : AbstractSetAssetsTask
    {
        
        [ForgeTagContext(ForgeContext.Data)]  public Tag Tag = Tags.ATTRIBUTE_SETS;
        [SerializeReference] public AttributeSet[] AttributeSets;

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
    
    public class SetAbilitiesTask : AbstractSetAssetsTask
    {
        [ForgeTagContext(ForgeContext.Data)]  public Tag Tag = Tags.ABILITIES;
        [SerializeReference] public Ability[] Abilities;

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
    
    public class SetTagsTask : AbstractSetAssetsTask
    {
        [ForgeTagContext(ForgeContext.Data)]  public Tag Tag = PlayForge.Tags.TAGS;
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
