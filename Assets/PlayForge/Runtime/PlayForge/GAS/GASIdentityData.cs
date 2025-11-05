using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class GASIdentityData
    {
        public int Level = 1;
        public int MaxLevel = 100;

        public Tag NameTag;
        public Tag Affiliation;

        public float RelativeLevel => (Level - 1f) / (MaxLevel - 1);

        private GASComponent System;

        public void Initialize(GASComponent system) => System = system;

        public string DistinctName => NameTag.GetName();
        
        public override string ToString()
        {
            return DistinctName;
        }
    }
}
