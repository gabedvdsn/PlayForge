using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Attribute", fileName = "Attribute_")]
    public class Attribute : BaseForgeObject, IHasReadableDefinition, IValidationReady
    {
        public string Name;
        public string Description;
        
        #region Internal
        
        public string GetName()
        {
            return Name;
        }
        public string GetDescription()
        {
            return Description;
        }
        public Texture2D GetPrimaryIcon()
        {
            return null;
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