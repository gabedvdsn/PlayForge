using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEditor;

#if UNITY_EDITOR

namespace FarEmerald.PlayForge
{
    public class RefDrawers
    {
        [UnityEditor.CustomPropertyDrawer(typeof(FrameworkRef))]
        public class FrameworkRefDrawer : AbstractRefDrawer
        {
            protected override IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx)
            {
                return new[] { new DataEntry(){ Name = idx.FrameworkKey, Id = 0 } };
            }
        }
        
        [UnityEditor.CustomPropertyDrawer(typeof(AttributeRef))]
        public class AttributeRefDrawer : AbstractRefDrawer
        {
            protected override System.Collections.Generic.IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx) => idx?.Attributes;
        }
        
        [UnityEditor.CustomPropertyDrawer(typeof(TagRef))]
        public class TagRefDrawer : AbstractRefDrawer
        {
            protected override IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx) => idx?.Tags;
            protected override IEnumerable<DataEntry> ApplyValidation(FrameworkProject fp, SerializedProperty property, IEnumerable<DataEntry> source)
            {
                var contextAttrs = GetFieldAttributes<ForgeCategory>(property).ToArray();
                if (contextAttrs.Length == 0) return base.ApplyValidation(fp, property, source);

                var entries = source as DataEntry[] ?? source.ToArray();

                var valid = entries.Where(
                    e => fp.TryGet(e.Id, e.Type, out var node) && contextAttrs.Any(a => a.Context == node.TagStatus<string>(ForgeTags.EDITOR_CATEGORIES)));
                return base.ApplyValidation(fp, property, valid);

            }
        }

        [UnityEditor.CustomPropertyDrawer(typeof(AbilityRef))]
        public class AbilityRefDrawer : AbstractRefDrawer
        {
            protected override System.Collections.Generic.IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx) => idx?.Abilities;
        }

        [UnityEditor.CustomPropertyDrawer(typeof(EntityRef))]
        public class EntityRefDrawer : AbstractRefDrawer
        {
            protected override System.Collections.Generic.IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx) => idx?.Entities;
        }

        [UnityEditor.CustomPropertyDrawer(typeof(EffectRef))]
        public class EffectRefDrawer : AbstractRefDrawer
        {
            protected override System.Collections.Generic.IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx) => idx?.Effects;
        }

        [UnityEditor.CustomPropertyDrawer(typeof(AttributeSetRef))]
        public class AttributeSetRefDrawer : AbstractRefDrawer
        {
            protected override System.Collections.Generic.IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx) => idx?.AttributeSets;
        }
    }
}
#endif