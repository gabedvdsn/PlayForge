using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public readonly struct Attribute : IEquatable<Attribute>, IHasReadableDefinition
    {
        private readonly string Name;
        private readonly string Description;
        
        private Attribute(string name, string description) 
        {
            Name = name;
            Description = description;
        }
        
        public static Attribute Generate(string name, string description)
        {
            return new Attribute(name, description);
        }

        #region Internal
        
        public string GetName()
        {
            return Name;
        }

        public string GetDescription()
        {
            return Description;
        }
        public Sprite GetPrimaryIcon()
        {
            return null;
        }

        public bool Equals(Attribute other)
        {
            return other.Name == Name;
        }
        
        public override bool Equals(object obj) => obj is Attribute other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Name, Description);
        
        #endregion
    }
}