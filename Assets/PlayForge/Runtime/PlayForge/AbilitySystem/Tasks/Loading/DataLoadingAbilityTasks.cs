using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of an asset loader, a Set task: it puts assets in the activation's data under a key as it's prepared,
    /// for the tasks after it to read, as a Run Sequence's sequence reads them with
    /// <c>data.GetLoadedAssets&lt;T&gt;(tag)</c>; its own run ends at once. An ability's Asset Loaders run once an
    /// activation, after its targeting and before its first stage, and one among a stage's tasks loads as the stage
    /// starts. A loader of your own overrides <c>Prepare</c> and <c>IsCriticalSection</c>.
    /// </summary>
    [Serializable]
    [ForgeMenu("Asset Loaders")]
    public abstract class AbstractSetAssetsTask : AbstractAbilityTask
    {
        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
    }
    
    /// <summary>
    /// An asset loader that puts its <c>Assets</c>, PlayForge assets of any kind, in the activation's data under its
    /// <c>Tag</c>, Assets by default.
    /// </summary>
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
    
    /// <summary>
    /// An asset loader that puts its <c>Effects</c> in the activation's data under its <c>Tag</c>, Effects by default,
    /// for a later task to apply, as a sequence does with <c>data.TryApplyEffects(target, tag)</c>.
    /// </summary>
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
    
    /// <summary>
    /// An asset loader that puts its <c>Entities</c>, Entity assets, in the activation's data under its <c>Tag</c>,
    /// Entities by default.
    /// </summary>
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
    
    /// <summary>
    /// An asset loader that puts its <c>Attributes</c> in the activation's data under its <c>Tag</c>, Attributes by
    /// default.
    /// </summary>
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
    
    /// <summary>
    /// An asset loader that puts its <c>AttributeSets</c> in the activation's data under its <c>Tag</c>, Attribute Sets
    /// by default.
    /// </summary>
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
    
    /// <summary>
    /// An asset loader that puts its <c>Abilities</c>, Ability assets, in the activation's data under its <c>Tag</c>,
    /// Abilities by default.
    /// </summary>
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
    
    /// <summary>
    /// An asset loader that puts its <c>Tags</c> in the activation's data under its <c>Tag</c>, Tags by default.
    /// </summary>
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
