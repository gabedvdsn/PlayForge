#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Core engine for template propagation. Handles:
    ///   - Propagating template field changes to child assets (non-overridden, non-excluded)
    ///   - Merging list properties (additions and removals from template)
    ///   - Detecting and recording field overrides by comparing child against template
    ///   - Cascading propagation through template chains (A → B → C)
    ///   - Finding all child assets of a given template via project scan
    /// </summary>
    public static class ForgeTemplatePropagator
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Propagates all non-excluded, non-overridden fields from the template to the child.
        /// Lists are merged (additions/removals from template applied).
        /// Does NOT save the child — caller is responsible for marking dirty and saving.
        /// </summary>
        public static void PropagateToChild(BaseForgeAsset template, BaseForgeAsset child)
        {
            if (template == null || child == null) return;
            if (template.GetType() != child.GetType()) return;
            if (child.Template != template) return;
            
            var assetType = template.GetType();
            var templateSO = new SerializedObject(template);
            var childSO = new SerializedObject(child);
            
            var iterator = templateSO.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                // Default: don't enter children. Each branch decides explicitly.
                enterChildren = false;
                string path = iterator.propertyPath;
                
                // Skip excluded paths (identity, linking, template internals)
                if (ForgeTemplateConfig.IsExcluded(path, assetType))
                    continue;
                
                // Arrays: merge (additions/removals from template)
                if (iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic)
                {
                    MergeArrayProperty(templateSO, childSO, child, path);
                    continue;
                }
                
                // ManagedReference ([SerializeReference]): copy as a whole unit
                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    if (!child.IsFieldOverridden(path))
                    {
                        var childProp = childSO.FindProperty(path);
                        if (childProp != null)
                            CopyManagedReference(iterator, childProp);
                    }
                    continue;
                }
                
                // Generic composite (non-array): propagate children explicitly using Next()
                // instead of relying on NextVisible which skips children of custom-drawn properties
                if (iterator.propertyType == SerializedPropertyType.Generic)
                {
                    var childProp = childSO.FindProperty(path);
                    if (childProp != null)
                        PropagateComposite(iterator, childProp, child, assetType);
                    continue;
                }
                
                // Leaf property: skip if overridden, otherwise copy
                if (child.IsFieldOverridden(path)) continue;
                
                var leafProp = childSO.FindProperty(path);
                if (leafProp != null)
                    CopyPropertyValue(iterator, leafProp);
                
                // Allow NextVisible to continue at the same depth
                enterChildren = true;
            }
            
            childSO.ApplyModifiedPropertiesWithoutUndo();
        }
        
        /// <summary>
        /// Propagates from a template to all its children, then cascades to grandchildren.
        /// Returns the list of all assets that were modified.
        /// </summary>
        public static List<BaseForgeAsset> PropagateToAllChildren(BaseForgeAsset template)
        {
            var modified = new List<BaseForgeAsset>();
            var children = FindChildAssets(template);
            
            foreach (var child in children)
            {
                PropagateToChild(template, child);
                EditorUtility.SetDirty(child);
                modified.Add(child);
                
                // Cascade: if this child is itself a template, propagate further
                var grandchildren = FindChildAssets(child);
                if (grandchildren.Count > 0)
                {
                    modified.AddRange(PropagateToAllChildren(child));
                }
            }
            
            return modified;
        }
        
        /// <summary>
        /// Compares a child asset against its template and automatically updates the override set.
        /// Fields that differ from the template are marked as overridden.
        /// Fields that match the template have their overrides cleared.
        /// Only operates on non-excluded leaf properties.
        /// </summary>
        public static void UpdateOverrides(BaseForgeAsset child)
        {
            if (!child.HasTemplate) return;
            
            var template = child.Template;
            var assetType = child.GetType();
            var templateSO = new SerializedObject(template);
            var childSO = new SerializedObject(child);
            
            var iterator = templateSO.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                string path = iterator.propertyPath;
                
                if (ForgeTemplateConfig.IsExcluded(path, assetType))
                {
                    enterChildren = false;
                    continue;
                }
                
                // Skip arrays — overrides don't apply to lists
                if (iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic)
                {
                    enterChildren = false;
                    continue;
                }
                
                // ManagedReference: compare as whole unit (type + content)
                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    enterChildren = false;
                    var childProp = childSO.FindProperty(path);
                    if (childProp == null) continue;
                    
                    if (AreManagedReferencesEqual(iterator, childProp))
                    {
                        child.ClearFieldOverride(path);
                    }
                    else
                    {
                        child.MarkFieldOverridden(path);
                    }
                    continue;
                }
                
                // Skip composites — their children will be visited individually
                if (iterator.propertyType == SerializedPropertyType.Generic)
                {
                    continue;
                }
                
                // Leaf property: compare and update override status
                var _childProp = childSO.FindProperty(path);
                if (_childProp == null) continue;
                
                if (ArePropertiesEqual(iterator, _childProp))
                {
                    child.ClearFieldOverride(path);
                }
                else
                {
                    child.MarkFieldOverridden(path);
                }
            }
            
            EditorUtility.SetDirty(child);
        }
        
        /// <summary>
        /// Finds all assets in the project that use the given asset as their template.
        /// </summary>
        public static List<BaseForgeAsset> FindChildAssets(BaseForgeAsset template)
        {
            var results = new List<BaseForgeAsset>();
            if (template == null) return results;
            
            var type = template.GetType();
            var guids = AssetDatabase.FindAssets($"t:{type.Name}");
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, type) as BaseForgeAsset;
                
                if (asset != null && asset != template && asset.Template == template)
                {
                    results.Add(asset);
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Checks whether the given asset is used as a template by any other assets.
        /// </summary>
        public static bool IsUsedAsTemplate(BaseForgeAsset asset)
        {
            return FindChildAssets(asset).Count > 0;
        }
        
        /// <summary>
        /// Returns true if propagating from the template would actually modify at least one child.
        /// Used to avoid prompting when only excluded fields (name, textures, etc.) were changed.
        /// Checks non-excluded leaf fields, managed references, and list diffs.
        /// </summary>
        public static bool WouldPropagationModifyAny(BaseForgeAsset template, List<BaseForgeAsset> children)
        {
            if (template == null || children == null || children.Count == 0) return false;
            
            var assetType = template.GetType();
            var templateSO = new SerializedObject(template);
            
            foreach (var child in children)
            {
                if (child == null || child.Template != template) continue;
                
                var childSO = new SerializedObject(child);
                var iterator = templateSO.GetIterator();
                bool enterChildren = true;
                
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    string path = iterator.propertyPath;
                    
                    if (ForgeTemplateConfig.IsExcluded(path, assetType)) continue;
                    
                    // Array: check if template list differs from snapshot (additions or removals)
                    if (iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic)
                    {
                        var templateIds = ExtractArrayIdentifiers(iterator);
                        var snapshot = child.GetListSnapshot(path);
                        var snapshotIds = snapshot?.ElementIdentifiers ?? new List<string>();
                        
                        bool hasAdded = templateIds.Any(id => !snapshotIds.Contains(id));
                        bool hasRemoved = snapshotIds.Any(id => !templateIds.Contains(id));
                        if (hasAdded || hasRemoved) return true;
                        continue;
                    }
                    
                    // ManagedReference: compare as whole unit
                    if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        if (!child.IsFieldOverridden(path))
                        {
                            var childProp = childSO.FindProperty(path);
                            if (childProp != null && !AreManagedReferencesEqual(iterator, childProp))
                                return true;
                        }
                        continue;
                    }
                    
                    // Generic composite: use Next to check sub-fields
                    if (iterator.propertyType == SerializedPropertyType.Generic)
                    {
                        if (WouldCompositeModify(iterator, childSO, child, assetType))
                            return true;
                        continue;
                    }
                    
                    // Leaf: check if non-overridden and different
                    if (!child.IsFieldOverridden(path))
                    {
                        var childProp = childSO.FindProperty(path);
                        if (childProp != null && !ArePropertiesEqual(iterator, childProp))
                            return true;
                    }
                    
                    enterChildren = true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks if any leaf within a composite differs between template and child.
        /// </summary>
        private static bool WouldCompositeModify(
            SerializedProperty templateComposite,
            SerializedObject childSO,
            BaseForgeAsset child,
            Type assetType)
        {
            var srcIter = templateComposite.Copy();
            var srcEnd = templateComposite.GetEndProperty();
            bool enter = true;
            
            while (srcIter.Next(enter) && !SerializedProperty.EqualContents(srcIter, srcEnd))
            {
                enter = true;
                string path = srcIter.propertyPath;
                
                if (ForgeTemplateConfig.IsExcluded(path, assetType)) { enter = false; continue; }
                
                if (srcIter.isArray && srcIter.propertyType == SerializedPropertyType.Generic)
                {
                    enter = false;
                    continue; // Skip nested arrays in quick check
                }
                
                if (srcIter.propertyType == SerializedPropertyType.ManagedReference)
                {
                    if (!child.IsFieldOverridden(path))
                    {
                        var childProp = childSO.FindProperty(path);
                        if (childProp != null && !AreManagedReferencesEqual(srcIter, childProp))
                            return true;
                    }
                    enter = false;
                    continue;
                }
                
                if (srcIter.propertyType == SerializedPropertyType.Generic) continue;
                
                if (!child.IsFieldOverridden(path))
                {
                    var childProp = childSO.FindProperty(path);
                    if (childProp != null && !ArePropertiesEqual(srcIter, childProp))
                        return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Performs initial list snapshot capture after a template is assigned.
        /// Should be called once after SetTemplate to establish baseline for merge diffs.
        /// </summary>
        public static void CaptureInitialListSnapshots(BaseForgeAsset child)
        {
            if (!child.HasTemplate) return;
            
            var template = child.Template;
            var assetType = template.GetType();
            var templateSO = new SerializedObject(template);
            
            var iterator = templateSO.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                string path = iterator.propertyPath;
                
                if (ForgeTemplateConfig.IsExcluded(path, assetType))
                {
                    enterChildren = false;
                    continue;
                }
                
                if (iterator.isArray && iterator.propertyType == SerializedPropertyType.Generic)
                {
                    var identifiers = ExtractArrayIdentifiers(iterator);
                    child.SetListSnapshot(path, identifiers);
                    enterChildren = false;
                }
            }
            
            EditorUtility.SetDirty(child);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // COMPOSITE PROPAGATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Propagates a composite (struct/class) property's children from template to child.
        /// Uses Next(true) instead of NextVisible to capture all serialized fields,
        /// including those hidden by custom property drawers or attributes.
        /// Respects per-sub-field overrides.
        /// </summary>
        private static void PropagateComposite(
            SerializedProperty templateComposite,
            SerializedProperty childComposite,
            BaseForgeAsset child,
            Type assetType)
        {
            var srcIter = templateComposite.Copy();
            var srcEnd = templateComposite.GetEndProperty();
            bool enter = true;
            
            while (srcIter.Next(enter) && !SerializedProperty.EqualContents(srcIter, srcEnd))
            {
                enter = true;
                string path = srcIter.propertyPath;
                
                // Skip excluded paths
                if (ForgeTemplateConfig.IsExcluded(path, assetType))
                {
                    enter = false;
                    continue;
                }
                
                // Nested arrays: merge
                if (srcIter.isArray && srcIter.propertyType == SerializedPropertyType.Generic)
                {
                    // Need the SerializedObjects for merge
                    MergeArrayProperty(
                        templateComposite.serializedObject,
                        childComposite.serializedObject,
                        child, path);
                    enter = false;
                    continue;
                }
                
                // Nested ManagedReference: copy as whole unit
                if (srcIter.propertyType == SerializedPropertyType.ManagedReference)
                {
                    if (!child.IsFieldOverridden(path))
                    {
                        var destProp = childComposite.serializedObject.FindProperty(path);
                        if (destProp != null)
                            CopyManagedReference(srcIter, destProp);
                    }
                    enter = false;
                    continue;
                }
                
                // Nested composite: recurse
                if (srcIter.propertyType == SerializedPropertyType.Generic)
                {
                    // Don't skip — Next(true) will enter its children on the next iteration
                    continue;
                }
                
                // Leaf property: skip if overridden, otherwise copy
                if (child.IsFieldOverridden(path)) continue;
                
                var childProp = childComposite.serializedObject.FindProperty(path);
                if (childProp != null)
                    CopyPropertyValue(srcIter, childProp);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // LIST MERGE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Merges a list property from template into child using snapshot-based diff:
        ///   1. Computes added elements (in template now, not in snapshot)
        ///   2. Computes removed elements (in snapshot, not in template now)
        ///   3. Adds new elements to child (if not already present)
        ///   4. Removes deleted elements from child (if present and not locally added)
        ///   5. Updates the snapshot to current template state
        /// </summary>
        private static void MergeArrayProperty(
            SerializedObject templateSO,
            SerializedObject childSO,
            BaseForgeAsset child,
            string arrayPath)
        {
            var templateArray = templateSO.FindProperty(arrayPath);
            var childArray = childSO.FindProperty(arrayPath);
            if (templateArray == null || childArray == null) return;
            
            // Get current template element identifiers
            var templateIds = ExtractArrayIdentifiers(templateArray);
            
            // Get snapshot from last sync (null if first sync)
            var snapshot = child.GetListSnapshot(arrayPath);
            var snapshotIds = snapshot?.ElementIdentifiers ?? new List<string>();
            
            // Get current child element identifiers
            var childIds = ExtractArrayIdentifiers(childArray);
            
            // Compute diffs
            var added = templateIds.Where(id => !snapshotIds.Contains(id)).ToList();
            var removed = snapshotIds.Where(id => !templateIds.Contains(id)).ToList();
            
            // Determine which child elements are "locally added" (not from template)
            var localChildIds = childIds.Where(id => !snapshotIds.Contains(id) && !templateIds.Contains(id)).ToList();
            
            bool modified = false;
            
            // Remove elements that were deleted from template
            // Walk backwards to maintain indices
            for (int i = childArray.arraySize - 1; i >= 0; i--)
            {
                var elementId = GetElementIdentifier(childArray.GetArrayElementAtIndex(i));
                if (removed.Contains(elementId) && !localChildIds.Contains(elementId))
                {
                    childArray.DeleteArrayElementAtIndex(i);
                    // For object references, DeleteArrayElementAtIndex first sets to null, need to delete again
                    if (i < childArray.arraySize)
                    {
                        var check = childArray.GetArrayElementAtIndex(i);
                        if (check.propertyType == SerializedPropertyType.ObjectReference && 
                            check.objectReferenceValue == null)
                        {
                            childArray.DeleteArrayElementAtIndex(i);
                        }
                    }
                    modified = true;
                }
            }
            
            // Refresh child IDs after removals
            if (modified) childSO.ApplyModifiedPropertiesWithoutUndo();
            var updatedChildIds = ExtractArrayIdentifiers(childArray);
            
            // Add elements that were added to template (if not already in child)
            foreach (var addedId in added)
            {
                if (updatedChildIds.Contains(addedId)) continue;
                
                // Find the element in template
                int templateIdx = FindElementByIdentifier(templateArray, addedId);
                if (templateIdx < 0) continue;
                
                var templateElement = templateArray.GetArrayElementAtIndex(templateIdx);
                
                // Add to child — apply and re-fetch after insert so struct sub-properties
                // are fully initialized before we try to copy into them
                int newIdx = childArray.arraySize;
                childArray.InsertArrayElementAtIndex(newIdx);
                childSO.ApplyModifiedPropertiesWithoutUndo();
                childSO.Update();
                childArray = childSO.FindProperty(arrayPath);
                
                var newElement = childArray.GetArrayElementAtIndex(newIdx);
                CopyArrayElement(templateElement, newElement);
                childSO.ApplyModifiedPropertiesWithoutUndo();
                
                modified = true;
            }
            
            if (modified) childSO.ApplyModifiedPropertiesWithoutUndo();
            
            // Update snapshot to current template state
            child.SetListSnapshot(arrayPath, templateIds);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ELEMENT IDENTIFICATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Extracts a list of string identifiers for all elements in an array property.
        /// </summary>
        private static List<string> ExtractArrayIdentifiers(SerializedProperty arrayProp)
        {
            var ids = new List<string>();
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                ids.Add(GetElementIdentifier(arrayProp.GetArrayElementAtIndex(i)));
            }
            return ids;
        }
        
        /// <summary>
        /// Returns a stable, deterministic string identifier for an array element.
        /// Must survive .NET AppDomain reloads (Unity recompile, play mode toggle).
        /// - Object references: uses asset GUID (stable across sessions)
        /// - Managed references (SerializeReference): uses type name + JSON content
        /// - Composites (structs): concatenated leaf values (deterministic)
        /// - Primitives: string representation
        /// </summary>
        private static string GetElementIdentifier(SerializedProperty element)
        {
            switch (element.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    var obj = element.objectReferenceValue;
                    if (obj == null) return "null";
                    var assetPath = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var guid = AssetDatabase.AssetPathToGUID(assetPath);
                        return $"ref:{guid}";
                    }
                    return $"ref:instance:{obj.GetInstanceID()}";
                    
                case SerializedPropertyType.ManagedReference:
                    var managedRef = element.managedReferenceValue;
                    if (managedRef == null) return "managed:null";
                    var typeName = managedRef.GetType().FullName;
                    var json = JsonUtility.ToJson(managedRef);
                    // Use the JSON directly — it's deterministic, unlike string.GetHashCode()
                    return $"managed:{typeName}:{json}";
                    
                case SerializedPropertyType.Generic:
                    // Struct/class composite — build deterministic fingerprint from leaf values
                    return $"composite:{FingerprintCompositeElement(element)}";
                    
                default:
                    // Primitive leaf in array — use string representation
                    return $"value:{GetPropertyValueString(element)}";
            }
        }
        
        /// <summary>
        /// Builds a deterministic string fingerprint of a composite element by
        /// concatenating all leaf property values. Unlike GetHashCode(), this is
        /// stable across .NET AppDomain reloads.
        /// </summary>
        private static string FingerprintCompositeElement(SerializedProperty composite)
        {
            var sb = new System.Text.StringBuilder(128);
            var iter = composite.Copy();
            var end = composite.GetEndProperty();
            bool enter = true;
            
            while (iter.Next(enter) && !SerializedProperty.EqualContents(iter, end))
            {
                enter = true;
                if (iter.isArray) { enter = false; continue; }
                if (iter.propertyType == SerializedPropertyType.ManagedReference) { enter = false; continue; }
                if (iter.propertyType == SerializedPropertyType.Generic) continue;
                
                if (sb.Length > 0) sb.Append('|');
                sb.Append(GetPropertyValueString(iter));
            }
            
            return sb.ToString();
        }
        
        private static int FindElementByIdentifier(SerializedProperty arrayProp, string identifier)
        {
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                if (GetElementIdentifier(arrayProp.GetArrayElementAtIndex(i)) == identifier)
                    return i;
            }
            return -1;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PROPERTY COMPARISON
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Compares two ManagedReference properties by checking type and JSON content.
        /// </summary>
        private static bool AreManagedReferencesEqual(SerializedProperty a, SerializedProperty b)
        {
            var aObj = a.managedReferenceValue;
            var bObj = b.managedReferenceValue;
            
            if (aObj == null && bObj == null) return true;
            if (aObj == null || bObj == null) return false;
            if (aObj.GetType() != bObj.GetType()) return false;
            
            return JsonUtility.ToJson(aObj) == JsonUtility.ToJson(bObj);
        }
        
        /// <summary>
        /// Compares two leaf SerializedProperties for value equality.
        /// </summary>
        private static bool ArePropertiesEqual(SerializedProperty a, SerializedProperty b)
        {
            if (a.propertyType != b.propertyType) return false;
            
            return a.propertyType switch
            {
                SerializedPropertyType.Integer      => a.intValue == b.intValue,
                SerializedPropertyType.Boolean       => a.boolValue == b.boolValue,
                SerializedPropertyType.Float         => Mathf.Approximately(a.floatValue, b.floatValue),
                SerializedPropertyType.String        => a.stringValue == b.stringValue,
                SerializedPropertyType.Enum          => a.enumValueIndex == b.enumValueIndex,
                SerializedPropertyType.ObjectReference => a.objectReferenceValue == b.objectReferenceValue,
                SerializedPropertyType.Color         => a.colorValue == b.colorValue,
                SerializedPropertyType.Vector2       => a.vector2Value == b.vector2Value,
                SerializedPropertyType.Vector3       => a.vector3Value == b.vector3Value,
                SerializedPropertyType.Vector4       => a.vector4Value == b.vector4Value,
                SerializedPropertyType.Vector2Int    => a.vector2IntValue == b.vector2IntValue,
                SerializedPropertyType.Vector3Int    => a.vector3IntValue == b.vector3IntValue,
                SerializedPropertyType.Rect          => a.rectValue == b.rectValue,
                SerializedPropertyType.RectInt       => a.rectIntValue.Equals(b.rectIntValue),
                SerializedPropertyType.Bounds        => a.boundsValue == b.boundsValue,
                SerializedPropertyType.BoundsInt     => a.boundsIntValue.Equals(b.boundsIntValue),
                SerializedPropertyType.Quaternion    => a.quaternionValue == b.quaternionValue,
                SerializedPropertyType.AnimationCurve => AnimationCurvesEqual(a.animationCurveValue, b.animationCurveValue),
                SerializedPropertyType.Hash128       => a.hash128Value == b.hash128Value,
                SerializedPropertyType.LayerMask     => a.intValue == b.intValue,
                SerializedPropertyType.Character     => a.intValue == b.intValue,
                _ => false // Unknown types are treated as non-equal (safe default)
            };
        }
        
        private static bool AnimationCurvesEqual(AnimationCurve a, AnimationCurve b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.keys.Length != b.keys.Length) return false;
            
            for (int i = 0; i < a.keys.Length; i++)
            {
                var ka = a.keys[i];
                var kb = b.keys[i];
                if (!Mathf.Approximately(ka.time, kb.time) || !Mathf.Approximately(ka.value, kb.value))
                    return false;
            }
            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PROPERTY COPYING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Copies the value of a leaf SerializedProperty from source to destination.
        /// </summary>
        private static void CopyPropertyValue(SerializedProperty source, SerializedProperty dest)
        {
            switch (source.propertyType)
            {
                case SerializedPropertyType.Integer:
                    dest.intValue = source.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    dest.boolValue = source.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    dest.floatValue = source.floatValue;
                    break;
                case SerializedPropertyType.String:
                    dest.stringValue = source.stringValue;
                    break;
                case SerializedPropertyType.Enum:
                    dest.enumValueIndex = source.enumValueIndex;
                    break;
                case SerializedPropertyType.ObjectReference:
                    dest.objectReferenceValue = source.objectReferenceValue;
                    break;
                case SerializedPropertyType.Color:
                    dest.colorValue = source.colorValue;
                    break;
                case SerializedPropertyType.Vector2:
                    dest.vector2Value = source.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    dest.vector3Value = source.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    dest.vector4Value = source.vector4Value;
                    break;
                case SerializedPropertyType.Vector2Int:
                    dest.vector2IntValue = source.vector2IntValue;
                    break;
                case SerializedPropertyType.Vector3Int:
                    dest.vector3IntValue = source.vector3IntValue;
                    break;
                case SerializedPropertyType.Rect:
                    dest.rectValue = source.rectValue;
                    break;
                case SerializedPropertyType.RectInt:
                    dest.rectIntValue = source.rectIntValue;
                    break;
                case SerializedPropertyType.Bounds:
                    dest.boundsValue = source.boundsValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    dest.boundsIntValue = source.boundsIntValue;
                    break;
                case SerializedPropertyType.Quaternion:
                    dest.quaternionValue = source.quaternionValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    dest.animationCurveValue = new AnimationCurve(source.animationCurveValue.keys);
                    break;
                case SerializedPropertyType.Hash128:
                    dest.hash128Value = source.hash128Value;
                    break;
                case SerializedPropertyType.LayerMask:
                    dest.intValue = source.intValue;
                    break;
                case SerializedPropertyType.Character:
                    dest.intValue = source.intValue;
                    break;
            }
        }
        
        /// <summary>
        /// Deep-copies a [SerializeReference] managed reference from source to dest.
        /// Uses JSON round-trip to create an independent clone of the concrete type.
        /// </summary>
        private static void CopyManagedReference(SerializedProperty source, SerializedProperty dest)
        {
            var srcObj = source.managedReferenceValue;
            if (srcObj != null)
            {
                var json = JsonUtility.ToJson(srcObj);
                var clone = JsonUtility.FromJson(json, srcObj.GetType());
                dest.managedReferenceValue = clone;
            }
            else
            {
                dest.managedReferenceValue = null;
            }
        }
        
        /// <summary>
        /// Copies all leaf values of an array element (handles composites, object refs,
        /// managed references, and primitives).
        /// </summary>
        private static void CopyArrayElement(SerializedProperty source, SerializedProperty dest)
        {
            switch (source.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    dest.objectReferenceValue = source.objectReferenceValue;
                    return;
                    
                case SerializedPropertyType.ManagedReference:
                    var srcObj = source.managedReferenceValue;
                    if (srcObj != null)
                    {
                        // Deep copy via JSON round-trip
                        var json = JsonUtility.ToJson(srcObj);
                        var clone = JsonUtility.FromJson(json, srcObj.GetType());
                        dest.managedReferenceValue = clone;
                    }
                    else
                    {
                        dest.managedReferenceValue = null;
                    }
                    return;
                    
                case SerializedPropertyType.Generic:
                    // Composite element: copy all leaf children
                    CopyCompositeChildren(source, dest);
                    return;
                    
                default:
                    // Leaf element
                    CopyPropertyValue(source, dest);
                    return;
            }
        }
        
        /// <summary>
        /// Copies all children of a composite (struct/class) property from source to dest.
        /// Uses lockstep iteration: since source and dest are the same type, Next() visits
        /// fields in the same order on both. Avoids FindPropertyRelative which can fail
        /// for struct array elements after InsertArrayElementAtIndex.
        /// </summary>
        private static void CopyCompositeChildren(SerializedProperty source, SerializedProperty dest)
        {
            var srcIter = source.Copy();
            var dstIter = dest.Copy();
            var srcEnd = source.GetEndProperty();
            bool enter = true;
            
            while (srcIter.Next(enter) && !SerializedProperty.EqualContents(srcIter, srcEnd))
            {
                dstIter.Next(enter);
                enter = true;
                
                if (srcIter.isArray && srcIter.propertyType == SerializedPropertyType.Generic)
                {
                    CopyArray(srcIter, dstIter);
                    enter = false;
                    continue;
                }
                
                if (srcIter.propertyType == SerializedPropertyType.ManagedReference)
                {
                    CopyManagedReference(srcIter, dstIter);
                    enter = false;
                    continue;
                }
                
                if (srcIter.propertyType == SerializedPropertyType.Generic)
                {
                    continue; // Enter children of both in lockstep
                }
                
                CopyPropertyValue(srcIter, dstIter);
            }
        }
        
        /// <summary>
        /// Full array copy (not merge). Used for nested arrays within composite elements.
        /// </summary>
        private static void CopyArray(SerializedProperty source, SerializedProperty dest)
        {
            dest.arraySize = source.arraySize;
            for (int i = 0; i < source.arraySize; i++)
            {
                CopyArrayElement(source.GetArrayElementAtIndex(i), dest.GetArrayElementAtIndex(i));
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // UTILITIES
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Extracts the relative path portion from a full property path given a parent path.
        /// E.g., parent="Definition", full="Definition.Name" → "Name"
        /// </summary>
        private static string GetRelativePath(string parentPath, string fullPath)
        {
            if (fullPath.StartsWith(parentPath + "."))
                return fullPath.Substring(parentPath.Length + 1);
            return fullPath;
        }
        
        /// <summary>
        /// Returns a human-readable string representation of a property value (for hashing).
        /// </summary>
        private static string GetPropertyValueString(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer       => prop.intValue.ToString(),
                SerializedPropertyType.Boolean        => prop.boolValue.ToString(),
                SerializedPropertyType.Float          => prop.floatValue.ToString("R"),
                SerializedPropertyType.String         => prop.stringValue ?? "",
                SerializedPropertyType.Enum           => prop.enumValueIndex.ToString(),
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue?.GetInstanceID().ToString() ?? "null",
                SerializedPropertyType.Color          => prop.colorValue.ToString(),
                SerializedPropertyType.Vector2        => prop.vector2Value.ToString(),
                SerializedPropertyType.Vector3        => prop.vector3Value.ToString(),
                SerializedPropertyType.Vector4        => prop.vector4Value.ToString(),
                _ => prop.type
            };
        }
    }
}
#endif