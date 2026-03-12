using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Attribute", fileName = "Attribute_")]
    public class Attribute : BaseForgeAsset, IAttribute
    {
        public string Name;
        public string Abbreviation;
        public string Description;
        public List<TextureItem> Textures;
        
        #region Internal
        
        public override string GetName()
        {
            return Name;
        }
        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield break;
        }
        public string GetAbbreviation()
        {
            return Abbreviation;
        }
        public List<TextureItem> GetTextures()
        {
            return Textures;
        }
        public override string GetDescription()
        {
            return Description;
        }
        public override Texture2D GetPrimaryIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
        }

        public bool Equals(Attribute other)
        {
            return other == this;
        }

        public bool Equals(IAttribute other)
        {
            return GetName() == other.GetName();
        }
        
        public override bool Equals(object obj) => obj is IAttribute other && Equals(other);

        public override int GetHashCode() => base.GetHashCode();

        #endregion
    }
}