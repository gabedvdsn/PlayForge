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
    public class Attribute : BaseForgeObject, IValidationReady
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
        public override string GetDescription()
        {
            return Description;
        }
        public override Texture2D GetPrimaryIcon()
        {
            foreach (var ti in Textures)
            {
                if (ti.Tag == Tags.PRIMARY) return ti.Texture;
            }
            return Textures.Count > 0 ? Textures[0].Texture : null;
        }

        public bool Equals(Attribute other)
        {
            return other == this;
        }
        
        public override bool Equals(object obj) => obj is Attribute other && Equals(other);

        public override int GetHashCode() => base.GetHashCode();

        #endregion
    }
}