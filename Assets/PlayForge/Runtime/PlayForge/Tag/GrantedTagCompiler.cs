using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Builds a GAS's applied tags from everything that grants them: its attribute set, its effects, its
    /// abilities and its items. Shared by the MonoBehaviour and runtime GAS, which each add their identity's
    /// own tags.
    ///
    /// It walks the sources' tag lists directly, following the same rules as their GetGrantedTags
    /// iterators, because those iterators allocate and compiling runs twice for every effect applied or
    /// removed (PF-007). Items keep their iterator: their equip rules live there, and items are rarer.
    /// </summary>
    internal static class GrantedTagCompiler
    {
        public static void Compile(TagCache cache, AttributeSet attributeSet, EffectSystemComponent effects,
            AbilitySystemComponent abilities, ItemSystemComponent items)
        {
            if (attributeSet) AddAttributeSet(cache, attributeSet);

            var shelf = effects.Shelf;
            for (int i = 0; i < shelf.Count; i++)
            {
                var spec = shelf[i].Spec;
                cache.AddTag(spec.Base.Tags.AssetTag);
                if (spec.Base.GetGrantedTags() is List<Tag> granted) AddAll(cache, granted);
                else foreach (var t in spec.Base.GetGrantedTags()) cache.AddTag(t);
            }

            foreach (var ability in abilities.Containers)
            {
                var tags = ability.Spec.Base.Tags;
                cache.AddTag(tags.AssetTag);
                AddAll(cache, tags.PassiveGrantedTags);
                if (ability.IsActive) AddAll(cache, tags.ActiveGrantedTags);
            }

            var itemContainers = items.GetItemContainers();
            for (int i = 0; i < itemContainers.Count; i++)
            {
                var item = itemContainers[i];
                cache.AddTag(item.Spec.Base.Tags.AssetTag);
                foreach (var t in item.GetGrantedTags()) cache.AddTag(t);
            }
        }

        public static void AddAll(TagCache cache, List<Tag> tags)
        {
            if (tags == null) return;
            for (int i = 0; i < tags.Count; i++) cache.AddTag(tags[i]);
        }

        /// <summary>A set's asset tag, then its subsets' in turn, as <see cref="AttributeSet.GetGrantedTags"/> yields them.</summary>
        private static void AddAttributeSet(TagCache cache, AttributeSet set)
        {
            cache.AddTag(set.AssetTag);
            if (set.SubSets == null) return;
            for (int i = 0; i < set.SubSets.Count; i++)
            {
                if (set.SubSets[i]) AddAttributeSet(cache, set.SubSets[i]);
            }
        }
    }
}
