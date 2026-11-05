using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Templates as prefabs work (Q23, ED-13). A template's changes reach its children when it's pushed to them, as an
    /// explicit step: each child, and each of theirs, takes the template's fields but those it overrides, and one Undo
    /// takes the push back. An edit to a child's field makes the field the child's own, an override, which its inspector
    /// marks, and Revert to Template takes back. Saving a template opened a dialog in the middle of the save, loaded every
    /// asset of its type, and changed the children with no Undo.
    /// </summary>
    [InitializeOnLoad]
    public static class ForgeTemplatePush
    {
        // While a push or a revert writes a child, its writes override nothing
        private static bool writing;

        static ForgeTemplatePush()
        {
            Undo.postprocessModifications += MarkOverrides;
            Undo.undoRedoEvent += OnUndoRedo;
            EditorApplication.contextualPropertyMenu += AddRevert;
        }

        /// <summary>The assets that name an asset as their template.</summary>
        public static List<BaseForgeAsset> ChildrenOf(BaseForgeAsset template) =>
            template == null ? new List<BaseForgeAsset>() : ForgeTemplatePropagator.FindChildAssets(template);

        /// <summary>
        /// Pushes a template to its children, and theirs: each takes the template's fields but those it overrides, as one
        /// Undo step. Returns the assets it changed.
        /// </summary>
        public static List<BaseForgeAsset> PushToChildren(BaseForgeAsset template)
        {
            var changed = new List<BaseForgeAsset>();
            if (template == null) return changed;
            // Unity hands over an edit's modifications as it flushes them, at the end of a frame, so an edit made just now,
            // as by a script, marks its override before the push reads the overrides
            Undo.FlushUndoRecordObjects();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Push {NameOf(template)} to Its Children");
            writing = true;
            try
            {
                // The family from one search of the project: each child's own children were searched for over the project
                // again, so a push to 2,000 children loaded 2,000 assets 2,000 times (ED-44)
                var family = ForgeTemplatePropagator.ChildrenByTemplate(template.GetType());
                Push(template, changed, new HashSet<BaseForgeAsset>(), family);
            }
            finally
            {
                writing = false;
            }
            Undo.CollapseUndoOperations(group);
            return changed;
        }

        private static void Push(BaseForgeAsset template, List<BaseForgeAsset> changed, HashSet<BaseForgeAsset> seen,
            Dictionary<BaseForgeAsset, List<BaseForgeAsset>> family)
        {
            // A loop of templates stops at the first asset it comes back to
            if (!seen.Add(template) || !family.TryGetValue(template, out var children)) return;
            foreach (var child in children)
            {
                Undo.RegisterCompleteObjectUndo(child, $"Push {NameOf(template)} to Its Children");
                ForgeTemplatePropagator.PropagateToChild(template, child);
                EditorUtility.SetDirty(child);
                changed.Add(child);
                Push(child, changed, seen, family);
            }
        }

        /// <summary>
        /// Takes a child's field back to its template's value, and makes the field the template's again, as one Undo step.
        /// </summary>
        public static void RevertToTemplate(SerializedProperty property)
        {
            if (property?.serializedObject.targetObject is not BaseForgeAsset child || !child.HasTemplate) return;
            var serialized = property.serializedObject;
            var path = OverridePathOf(child, property.propertyPath) ?? property.propertyPath;
            var own = serialized.FindProperty(path);
            var theirs = new SerializedObject(child.Template).FindProperty(path);
            if (own == null || theirs == null) return;

            // One record of the whole child, so the copy makes no modification of its own, which would mark the field
            // overridden again as Unity flushes it
            Undo.RegisterCompleteObjectUndo(child, "Revert to Template");
            writing = true;
            try
            {
                if (own.propertyType == SerializedPropertyType.ManagedReference)
                {
                    var value = theirs.managedReferenceValue;
                    object copy = null;
                    if (value != null)
                    {
                        copy = Activator.CreateInstance(value.GetType());
                        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(value), copy);
                    }
                    own.managedReferenceValue = copy;
                }
                else own.boxedValue = theirs.boxedValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                writing = false;
            }
            child.ClearFieldOverride(path);
            EditorUtility.SetDirty(child);
            serialized.Update();
        }

        // An edit to a child's field, an inspector's, an Import's or a script's through a SerializedObject, makes the field
        // the child's own, as a prefab instance's override is (Q23), as Unity flushes the edit's modifications. The
        // overrides were worked out on each save, against the template as it was then
        private static UndoPropertyModification[] MarkOverrides(UndoPropertyModification[] modifications)
        {
            if (writing) return modifications;
            var serialized = new Dictionary<BaseForgeAsset, SerializedObject>();
            try
            {
                foreach (var modification in modifications)
                {
                    if (modification.currentValue?.target is not BaseForgeAsset child || !child.HasTemplate) continue;
                    if (!serialized.TryGetValue(child, out var own)) serialized[child] = own = new SerializedObject(child);
                    var path = OverridePathOf(own, modification.currentValue.propertyPath);
                    if (path == null || child.IsFieldOverridden(path)) continue;
                    child.MarkFieldOverridden(path);
                    EditorUtility.SetDirty(child);
                    Remember(child, path);
                }
            }
            finally
            {
                foreach (var own in serialized.Values) own.Dispose();
            }
            return modifications;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The marks' Undo
        // ═══════════════════════════════════════════════════════════════════════════

        // Unity's Undo takes back what a postprocessModifications hook adds to an edit but never makes it again on Redo,
        // drops a record the hook starts, and puts a whole-object record ahead of the edit's own, where Undo would make the
        // edit again. So each mark is remembered with the Undo step it was made in: Undo of that step takes the mark back,
        // and its Redo makes it again. The marks live in SessionState, as Unity's Undo outlives a domain reload
        [Serializable]
        private sealed class Mark
        {
            public int Group;
            public int Asset;
            public string Path;
            // The Undo step whose Undo took the mark back, or -1 while it stands
            public int UndoneBy = -1;
        }

        [Serializable]
        private sealed class MarkList
        {
            public List<Mark> Marks = new();
        }

        private const string MarksKey = "PlayForge.TemplateOverrideMarks";
        private const int MarksKept = 512;
        private static MarkList marks;

        private static MarkList Marks
        {
            get
            {
                if (marks != null) return marks;
                var json = SessionState.GetString(MarksKey, "");
                marks = string.IsNullOrEmpty(json) ? new MarkList() : JsonUtility.FromJson<MarkList>(json) ?? new MarkList();
                return marks;
            }
        }

        private static void SaveMarks() => SessionState.SetString(MarksKey, JsonUtility.ToJson(Marks));

        private static void Remember(BaseForgeAsset child, string path)
        {
            var list = Marks.Marks;
            // A new edit ends what Redo could make again, as it does in Unity's Undo
            list.RemoveAll(mark => mark.UndoneBy >= 0);
            list.Add(new Mark { Group = Undo.GetCurrentGroup(), Asset = child.GetInstanceID(), Path = path });
            if (list.Count > MarksKept) list.RemoveRange(0, list.Count - MarksKept);
            SaveMarks();
        }

        // Undo of a step takes back the marks made in it, and in any step collapsed into it; Redo makes them again
        private static void OnUndoRedo(in UndoRedoInfo undo)
        {
            bool changed = false;
            foreach (var mark in Marks.Marks)
            {
                bool takeBack = !undo.isRedo && mark.UndoneBy < 0 && mark.Group >= undo.undoGroup;
                bool makeAgain = undo.isRedo && mark.UndoneBy == undo.undoGroup;
                if (!takeBack && !makeAgain) continue;
                if (EditorUtility.InstanceIDToObject(mark.Asset) is BaseForgeAsset child)
                {
                    if (takeBack) child.ClearFieldOverride(mark.Path);
                    else child.MarkFieldOverridden(mark.Path);
                    EditorUtility.SetDirty(child);
                }
                mark.UndoneBy = takeBack ? undo.undoGroup : -1;
                changed = true;
            }
            if (changed) SaveMarks();
        }

        // A child's overridden field offers Revert to Template in its right-click menu
        private static void AddRevert(GenericMenu menu, SerializedProperty property)
        {
            if (property?.serializedObject.targetObject is not BaseForgeAsset child || !child.HasTemplate) return;
            if (property.serializedObject.isEditingMultipleObjects) return;
            var path = OverridePathOf(child, property.propertyPath);
            if (path == null || !child.IsFieldOverridden(path)) return;
            var copy = property.Copy();
            menu.AddItem(new GUIContent($"Revert to Template \"{NameOf(child.Template)}\""), false, () => RevertToTemplate(copy));
        }

        /// <summary>
        /// The path an edit overrides, at the granularity the push keeps: a plain field's own path, or the outermost
        /// [SerializeReference] field it's inside, which the push copies whole. An edit inside a list overrides nothing,
        /// since a push merges lists; neither does an edit to a field the push leaves, such as the name. Null when it
        /// overrides nothing.
        /// </summary>
        internal static string OverridePathOf(BaseForgeAsset child, string path)
        {
            using var serialized = new SerializedObject(child);
            return OverridePathOf(serialized, path);
        }

        private static string OverridePathOf(SerializedObject serialized, string path)
        {
            if (string.IsNullOrEmpty(path) || ForgeTemplateConfig.IsExcluded(path, serialized.targetObject.GetType())) return null;
            if (path.Contains(".Array.")) return null;
            var parts = path.Split('.');
            for (int i = 1; i <= parts.Length; i++)
            {
                var prefix = string.Join(".", parts.Take(i));
                var property = serialized.FindProperty(prefix);
                if (property == null) return null;
                if (property.propertyType == SerializedPropertyType.ManagedReference) return prefix;
                if (property.isArray && property.propertyType == SerializedPropertyType.Generic) return null;
            }
            return path;
        }

        private static string NameOf(BaseForgeAsset asset) => !string.IsNullOrWhiteSpace(asset.GetName()) ? asset.GetName() : asset.name;
    }
}
