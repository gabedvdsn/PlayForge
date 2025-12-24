using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct Tag : IHasReadableDefinition, IEquatable<Tag>
    {
        public string Name;

        private Tag(string name)
        {
            Name = name;
        }

        public static Tag Generate(int _id)
        {
            return Generate(_id.ToString());
        }

        public static Tag Generate(string _name)
        {
            return new Tag(_name);
        }
        

        #region Internal
        
        public string GetName()
        {
            return Name;
        }
        public string GetDescription()
        {
            return "";
        }
        public Sprite GetPrimaryIcon()
        {
            return null;
        }
        
        #endregion

        public override string ToString()
        {
            return GetName();
        }

        public static bool operator !=(Tag a, Tag b)
        {
            return !(a == b);
        }
        public static bool operator ==(Tag a, Tag b)
        {
            return a.Equals(b);
        }

        public bool Equals(Tag other)
        {
            return Name == other.Name;
        }
        public override bool Equals(object obj)
        {
            return obj is Tag other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Name);
        }
    }
}
