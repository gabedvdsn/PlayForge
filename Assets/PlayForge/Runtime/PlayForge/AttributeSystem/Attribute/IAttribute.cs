using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An attribute as the attribute system knows it: a name, a description and an icon, an abbreviation and its
    /// textures. The <c>Attribute</c> asset is one and a <c>RuntimeAttribute</c> made in code is another; an entity's
    /// attributes, an effect's impacts and workers each name one.
    /// </summary>
    public interface IAttribute : IHasReadableDefinition, ITagSource, IEquatable<IAttribute>
    {
        public string GetAbbreviation();
        public List<TextureItem> GetTextures();
    }

    /// <summary>
    /// An attribute made in code, with no asset behind it: a name, an abbreviation and a description where it's given
    /// them, and textures <c>AddTexture</c> adds. <c>AttributeRegistry.GetByName</c> makes one for a name it doesn't
    /// know, where it's asked to, and an entity's attribute system takes one with <c>ProvideAttribute(attribute,
    /// value)</c>.
    /// </summary>
    public class RuntimeAttribute : IAttribute
    {
        public readonly string Name;
        public readonly string Abbreviation;
        public readonly string Description;
        public readonly List<TextureItem> Textures = new();

        public readonly Tag? DataTag;

        public RuntimeAttribute(string name, Tag? dataTag = null)
        {
            Name = name;
            DataTag = dataTag;
        }

        public RuntimeAttribute(string name, string abbreviation, string description, Tag? dataTag = null)
        {
            Name = name;
            Abbreviation = abbreviation;
            Description = description;
            DataTag = dataTag;
        }

        public void AddTexture(Tag tag, Texture2D texture)
        {
            Textures.Add(new TextureItem()
            {
                Tag = tag, Texture = texture
            });
        }
        
        public string GetName()
        {
            return Name;
        }
        public string GetDescription()
        {
            return Description;
        }
        public Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
        }
        public IEnumerable<Tag> GetGrantedTags()
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

        public bool Equals(IAttribute other)
        {
            if (ReferenceEquals(null, other)) return false;

            return Name == other.GetName() 
                   && Abbreviation == other.GetAbbreviation() 
                   && Description == other.GetDescription() 
                   && Equals(Textures, other.GetTextures());
        }
        private bool Equals(RuntimeAttribute other)
        {
            return Name == other.Name 
                   && Abbreviation == other.Abbreviation 
                   && Description == other.Description 
                   && Equals(Textures, other.Textures);
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IAttribute)obj);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Abbreviation);
        }
    }
}
