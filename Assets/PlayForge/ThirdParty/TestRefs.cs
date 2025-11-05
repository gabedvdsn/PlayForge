using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class TestRefs : MonoBehaviour
    {
        public AttributeRef Attribute;
        
        //[GasifyContext(EditorTagService.Contexts.Identifier)]
        [ForgeFilterName("Raw")]
        public TagRef Tag;

        public EntityRef Entity;
        public AbilityRef Ability;
        public AttributeSetRef AttributeSet;
        public EffectRef Effect;
    }
}
