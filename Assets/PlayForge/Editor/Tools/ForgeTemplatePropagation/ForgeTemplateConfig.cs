#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Defines which property paths are excluded from template propagation for each asset type.
    /// Excluded fields are identity fields (name, tags, textures), linking fields (link mode,
    /// linked source), and template system internals.
    /// </summary>
    public static class ForgeTemplateConfig
    {
        // Private/Unity serialized field names that cannot use nameof
        private const string LinkedSourceField = "_linkedSource";
        private const string TemplateField = "_template";
        private const string TemplateOverridesField = "_templateOverrides";
        private const string TemplateListSnapshotsField = "_templateListSnapshots";
        
        // Sub-field names for types whose source is not available here (AbilityDefinition, ItemDefinition)
        // If these types use different field names, update these constants.
        private const string SubFieldName = "Name";
        private const string SubFieldDescription = "Description";
        private const string SubFieldTextures = "Textures";
        private const string SubFieldAssetTag = "AssetTag";
        
        // ═══════════════════════════════════════════════════════════════════════════
        // COMMON EXCLUSIONS (apply to all BaseForgeAsset subtypes)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static readonly HashSet<string> CommonExclusions = new()
        {
            // Unity internals
            "m_Script",
            "m_Name",
            "m_ObjectHideFlags",
            
            // BaseForgeAsset template system fields
            TemplateField,
            TemplateOverridesField,
            TemplateListSnapshotsField,
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PER-TYPE EXCLUSIONS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// EntityIdentity: identity, affiliation, context, and linking fields excluded.
        /// Propagated: levels, activation policy, abilities, items, attributes, workers, local data.
        /// </summary>
        private static readonly HashSet<string> EntityIdentityExclusions = new()
        {
            nameof(EntityIdentity.Name),
            nameof(EntityIdentity.Description),
            nameof(EntityIdentity.Textures),
            nameof(EntityIdentity.AssetTag),
            // nameof(EntityIdentity.Affiliation),
            // nameof(EntityIdentity.ContextTags),
            
            // Linking
            nameof(EntityIdentity.LinkMode),
            LinkedSourceField,
        };
        
        /// <summary>
        /// GameplayEffect: definition identity, asset tag, context tags, and linking excluded.
        /// Propagated: impact, duration, workers, requirements, granted tags, local data.
        /// </summary>
        private static readonly HashSet<string> GameplayEffectExclusions = new()
        {
            $"{nameof(GameplayEffect.Definition)}.{nameof(GameplayEffectDefinition.Name)}",
            $"{nameof(GameplayEffect.Definition)}.{nameof(GameplayEffectDefinition.Description)}",
            $"{nameof(GameplayEffect.Definition)}.{nameof(GameplayEffectDefinition.Textures)}",
            $"{nameof(GameplayEffect.Tags)}.{nameof(GameplayEffectTags.AssetTag)}",
            //$"{nameof(GameplayEffect.Tags)}.{nameof(GameplayEffectTags.ContextTags)}",
            
            // Linking
            nameof(GameplayEffect.LinkMode),
            LinkedSourceField,
        };
        
        /// <summary>
        /// Ability: definition identity, asset tag, and linking excluded.
        /// Propagated: behaviour, validation rules, cost, cooldown, workers, levels, local data.
        /// </summary>
        private static readonly HashSet<string> AbilityExclusions = new()
        {
            // AbilityDefinition sub-fields (type not available for nameof, uses string constants)
            $"{nameof(Ability.Definition)}.{SubFieldName}",
            $"{nameof(Ability.Definition)}.{SubFieldDescription}",
            $"{nameof(Ability.Definition)}.{SubFieldTextures}",
            $"{nameof(Ability.Tags)}.{SubFieldAssetTag}",
            
            // Linking
            nameof(Ability.LinkMode),
            LinkedSourceField,
        };
        
        /// <summary>
        /// Item: definition identity, asset tag, and linking excluded.
        /// Propagated: granted effects, active ability, workers, levels, local data.
        /// </summary>
        private static readonly HashSet<string> ItemExclusions = new()
        {
            // ItemDefinition sub-fields (type not available for nameof, uses string constants)
            $"{nameof(Item.Definition)}.{SubFieldName}",
            $"{nameof(Item.Definition)}.{SubFieldDescription}",
            $"{nameof(Item.Definition)}.{SubFieldTextures}",
            $"{nameof(Item.Tags)}.{SubFieldAssetTag}",
            
            // Linking
            nameof(Item.LinkMode),
            LinkedSourceField,
        };
        
        /// <summary>
        /// AttributeSet: identity fields excluded.
        /// Propagated: attributes, subsets, collision policy, workers, local data.
        /// </summary>
        private static readonly HashSet<string> AttributeSetExclusions = new()
        {
            nameof(AttributeSet.Name),
            nameof(AttributeSet.Description),
            nameof(AttributeSet.Textures),
            nameof(AttributeSet.AssetTag),
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TYPE → EXCLUSION MAPPING
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static readonly Dictionary<Type, HashSet<string>> TypeExclusions = new()
        {
            { typeof(EntityIdentity), EntityIdentityExclusions },
            { typeof(GameplayEffect), GameplayEffectExclusions },
            { typeof(Ability), AbilityExclusions },
            { typeof(Item), ItemExclusions },
            { typeof(AttributeSet), AttributeSetExclusions },
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if the given property path should be excluded from template propagation
        /// for the given asset type. Checks both common exclusions and type-specific exclusions.
        /// 
        /// Matching rules:
        ///   - Exact match: "Name" matches "Name"
        ///   - Prefix match: "Textures" matches "Textures.Array.data[0]" etc.
        ///   - Sub-field match: "Definition.Name" matches "Definition.Name" and any children
        /// </summary>
        public static bool IsExcluded(string propertyPath, Type assetType)
        {
            // Check common exclusions
            if (MatchesAny(propertyPath, CommonExclusions)) return true;
            
            // Check type-specific exclusions
            if (TypeExclusions.TryGetValue(assetType, out var typeSet))
            {
                if (MatchesAny(propertyPath, typeSet)) return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Returns true if the asset type supports templates.
        /// </summary>
        public static bool SupportsTemplates(Type assetType)
        {
            return TypeExclusions.ContainsKey(assetType);
        }
        
        /// <summary>
        /// Returns the full set of excluded paths for a given type (common + type-specific).
        /// </summary>
        public static HashSet<string> GetAllExclusions(Type assetType)
        {
            var result = new HashSet<string>(CommonExclusions);
            if (TypeExclusions.TryGetValue(assetType, out var typeSet))
            {
                result.UnionWith(typeSet);
            }
            return result;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // MATCHING
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static bool MatchesAny(string propertyPath, HashSet<string> exclusions)
        {
            // Exact match
            if (exclusions.Contains(propertyPath)) return true;
            
            // Prefix match: check if path starts with any exclusion + "."
            // This handles cases like "Textures" excluding "Textures.Array.data[0]"
            // and "Definition.Name" excluding "Definition.Name.SomethingNested"
            foreach (var exclusion in exclusions)
            {
                if (propertyPath.StartsWith(exclusion + ".") || propertyPath.StartsWith(exclusion + ".Array"))
                    return true;
            }
            
            return false;
        }
    }
}
#endif