using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class BaseForgeObject : ScriptableObject
    {
        public abstract HashSet<Tag> GetAllTags();
    }
}
