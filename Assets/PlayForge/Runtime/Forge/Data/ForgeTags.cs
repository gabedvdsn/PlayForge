using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
    public static class ForgeTags
    {
        #region Gasify Tags

        /// <summary>
        /// The key tag for Gasify (editor) related tags
        /// </summary>
        public static Tag GASIFY_TAGS => Tag.Generate("E_GASIFY_TAGS");
        
        /// <summary>
        /// Indicates that data experienced an error loading editor tags when building from JSON
        /// </summary>
        public static Tag BAD_JSON_LOAD => Tag.Generate("E_BAD_JSON_LOAD");
        
        /// <summary>
        /// Indicates that the data is editable in the editor
        /// </summary>
        public static Tag EDITABLE => Tag.Generate("E_EDITABLE");
        
        /// <summary>
        /// Indicates that data has been created (hence added as a working & referencable item in the project)
        /// </summary>
        public static Tag IS_CREATED => Tag.Generate("E_IS_CREATED");
        
        /// <summary>
        /// Indicates the data is valid, without errors
        /// </summary>
        public static Tag VALID_FOR_GAMEPLAY => Tag.Generate("E_STATUS");
        
        /// <summary>
        /// Indicates that this data is a saved copy of data
        /// </summary>
        public static Tag IS_SAVED_COPY => Tag.Generate("E_IS_SAVED_COPY");
        
        /// <summary>
        /// A saved copy will hold the ID of the data it copies
        /// </summary>
        public static Tag COPY_ID => Tag.Generate("E_COPY_ID");
        
        /// <summary>
        /// Indicates that the item has unsaved work (as compared to its saved copy, or if it is marked with UNSAVED_UNCREATED).
        /// </summary>
        public static Tag HAS_UNSAVED_WORK => Tag.Generate("E_HAS_UNSAVED_WORK");

        /// <summary>
        /// Indicates the editor-only classification of the data for ease of navigation and categorization
        /// </summary>
        public static Tag EDITOR_CATEGORIES => Tag.Generate("E_EDITOR_CATEGORY");
        
        /// <summary>
        /// Indicates the data is a template item
        /// </summary>
        public static Tag IS_TEMPLATE => Tag.Generate("E_IS_TEMPLATE");

        /// <summary>
        /// Indicates which base template the data is built from
        /// </summary>
        public static Tag SOURCES_TEMPLATE => Tag.Generate("E_SOURCES_TEMPLATE");

        /// <summary>
        /// Indicates nodes the data references
        /// REFERENCES = { node_id: [ field, field, ... ], ... }
        /// </summary>
        public static Tag REFERENCES => Tag.Generate("E_REFERENCES");
        
        /// <summary>
        /// Indicates nodes the data is referenced by
        /// REFERENCED_BY = { node_id: [ field, field, ... ], ... }
        /// </summary>
        public static Tag REFERENCED_BY => Tag.Generate("E_REFERENCED_BY");
        
        #endregion
        
        #region Styling Tags

        /// <summary>
        /// The key tag for all styling related tags
        /// </summary>
        public static Tag STYLING_TAGS => Tag.Generate("E_STYLING_TAGS");

        /// <summary>
        /// Indicates Preset assignments for node, sections, and fields
        /// Key: Preset
        /// </summary>
        public static Tag FIELD_PRESET_ASSIGNMENT => Tag.Generate("E_FIELD_PRESET_ASSIGNMENT");
        
        #endregion
        
        #region Helpers
        
        #region External
        
        public static bool TagStatus(this ForgeDataNode node, Tag target, bool fallback = false)
        {
            return node.TagStatus<bool>(target, fallback);
        }

        public static T TagStatus<T>(this ForgeDataNode node, Tag target, T fallback = default)
        {
            try
            {
                var editor = GetEditorTags(node);
                var _src = editor[GASIFY_TAGS] as Dictionary<Tag, object>;

                if (_src == null) return fallback;
                
                if (!_src.ContainsKey(target)) return fallback;
                return (T)_src[target];
            }
            catch
            {
                return fallback;
            }
        }

        public static bool TagStatus(this ForgeDataNode node, Tag target, out bool result, bool fallback = false)
        {
            return node.TagStatus<bool>(target, out result, fallback: fallback);
        }

        public static bool TagStatus<T>(this ForgeDataNode node, Tag target, out T result, T fallback = default)
        {
            try
            {
                var editor = GetEditorTags(node);
                var _src = editor[GASIFY_TAGS] as Dictionary<Tag, object>;

                if (_src is null || !_src.ContainsKey(target))
                {
                    result = fallback;
                    return false;
                }

                result = (T)_src[target];
                return true;
            }
            catch
            {
                result = fallback;
                return false;
            }
        }
        
        public static bool TagStatusStyling(this ForgeDataNode node, Tag target, bool fallback = false)
        {
            return node.TagStatusStyling<bool>(target, fallback);
        }

        public static T TagStatusStyling<T>(this ForgeDataNode node, Tag target, T fallback = default)
        {
            try
            {
                var editor = GetEditorTags(node);
                var _src = editor[STYLING_TAGS] as Dictionary<Tag, object>;
                
                if (_src == null) return fallback;
                
                if (!_src.ContainsKey(target)) return fallback;
                return (T)_src[target];
            }
            catch
            {
                return fallback;
            }
        }
        
        public static bool TagStatusStyling(this ForgeDataNode node, Tag target, out bool result, bool fallback = false)
        {
            return node.TagStatusStyling<bool>(target, out result, fallback: fallback);
        }

        public static bool TagStatusStyling<T>(this ForgeDataNode node, Tag target, out T result, T fallback = default)
        {
            try
            {
                var editor = GetEditorTags(node);
                var _src = editor[STYLING_TAGS] as Dictionary<Tag, object>;

                if (_src is null || !_src.ContainsKey(target))
                {
                    result = fallback;
                    return false;
                }

                result = (T)_src[target];
                return true;
            }
            catch
            {
                result = fallback;
                return false;
            }
        }

        public static bool IsValidForEditing(ForgeDataNode node)
        {
            return node.TagStatus(IS_CREATED) && !node.TagStatus(IS_SAVED_COPY);
        }

        public static bool IsValidForProject(ForgeDataNode node)
        {
            return !node.TagStatus(IS_TEMPLATE);
        }

        public static bool IsValidComplete(ForgeDataNode node)
        {
            return node.TagStatus(IS_CREATED) 
                   && !node.TagStatus(IS_TEMPLATE) 
                   && !node.TagStatus(IS_SAVED_COPY)
                   && !node.TagStatus(HAS_UNSAVED_WORK) 
                   && node.TagStatus(VALID_FOR_GAMEPLAY);
        }
        
        #endregion
        
        #region Internal

        private static Dictionary<Tag, object> GetEditorTags(ForgeDataNode node)
        {
            if (!node.editorTags.ContainsKey(GASIFY_TAGS) && !node.editorTags.ContainsKey(STYLING_TAGS)) node.editorTags = CreateEditorTags();
            else if (!node.editorTags.ContainsKey(GASIFY_TAGS) || !node.editorTags.ContainsKey(STYLING_TAGS)) node.editorTags = CreateEditorTags(node.editorTags);
            return node.editorTags;
        }

        private static Dictionary<Tag, object> GetGasifyTags(ForgeDataNode node)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            return gasify;
        }

        private static Dictionary<Tag, object> CreateEditorTags()
        {
            var tags = new Dictionary<Tag, object>
            {
                { GASIFY_TAGS, new Dictionary<Tag, object>() },
                { STYLING_TAGS, new Dictionary<Tag, object>() },
            };
            return tags;
        }
        
        private static Dictionary<Tag, object> CreateEditorTags(Dictionary<Tag, object> editor)
        {
            var tags = new Dictionary<Tag, object>();

            if (!editor.ContainsKey(GASIFY_TAGS)) tags[GASIFY_TAGS] = new Dictionary<Tag, object>();
            else tags[GASIFY_TAGS] = editor[GASIFY_TAGS];
            
            if (!editor.ContainsKey(STYLING_TAGS)) tags[STYLING_TAGS] = new Dictionary<Tag, object>();
            else tags[STYLING_TAGS] = editor[STYLING_TAGS];

            return tags;
        }

        private static void ComputeCompositeTagValues(ForgeDataNode node, Dictionary<Tag, object> gasify)
        {
            
        }
        
        #endregion
        
        #endregion
        
        #region Building Tags

        public static void ValidateEditorTags(ForgeDataNode node)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            
            EnsureTagExists(EDITABLE, true);
            EnsureTagExists(IS_CREATED, true);
            EnsureTagExists(IS_SAVED_COPY, true);
            EnsureTagExists(HAS_UNSAVED_WORK, false);
            EnsureTagExists(EDITOR_CATEGORIES, new List<Tag>());
            EnsureTagExists(IS_TEMPLATE, false);
            
            EnsureTagExists(VALID_FOR_GAMEPLAY, false);
            
            EnsureTagExists(REFERENCES, new Dictionary<int, HashSet<string>>());
            EnsureTagExists(REFERENCED_BY, new Dictionary<int, HashSet<string>>());
            
            ComputeCompositeTagValues(node, gasify);

            void EnsureTagExists<T>(Tag tag, T fallback)
            {
                gasify?.TryAdd(tag, fallback);
            }
        }

        public static void ForNewData(ForgeDataNode node)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            
            gasify[EDITABLE] = true;
            gasify[IS_CREATED] = false;
            gasify[IS_SAVED_COPY] = false;
            gasify[HAS_UNSAVED_WORK] = true;
            gasify[EDITOR_CATEGORIES] = new List<Tag>();
            gasify[IS_TEMPLATE] = false;

            gasify[VALID_FOR_GAMEPLAY] = false;
            
            gasify[REFERENCES] = new Dictionary<int, HashSet<string>>();
            gasify[REFERENCED_BY] = new Dictionary<int, HashSet<string>>();
            
            ComputeCompositeTagValues(node, gasify);
        }

        /// <summary>
        /// Applies copy tags to node 
        /// </summary>
        /// <param name="clone">The clone node</param>
        /// <param name="cloneOf">The original data</param>
        /// <param name="setCloneRef"></param>
        public static void ForCloneData(ForgeDataNode clone, ForgeDataNode cloneOf, bool setCloneRef = true)
        {
            clone.editorTags = CreateEditorTags();
            
            var editor = GetEditorTags(clone);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            
            gasify[EDITABLE] = false;
            gasify[IS_CREATED] = true;
            gasify[IS_SAVED_COPY] = true;
            gasify[COPY_ID] = cloneOf.Id;
            gasify[IS_TEMPLATE] = cloneOf.TagStatus(IS_TEMPLATE);
            gasify[HAS_UNSAVED_WORK] = false;

            gasify[VALID_FOR_GAMEPLAY] = cloneOf.TagStatus(VALID_FOR_GAMEPLAY);
            
            gasify[EDITOR_CATEGORIES] = cloneOf.TagStatus<List<Tag>>(EDITOR_CATEGORIES);
            gasify[REFERENCES] = cloneOf.TagStatus<Dictionary<int, HashSet<string>>>(REFERENCES);
            gasify[REFERENCED_BY] = cloneOf.TagStatus<Dictionary<int, HashSet<string>>>(REFERENCED_BY);
            
            ComputeCompositeTagValues(clone, gasify);
            
            var c_editor = GetEditorTags(cloneOf);
            var c_gasify = c_editor[GASIFY_TAGS] as Dictionary<Tag, object>;

            // Set the master's COPY_ID to the clone ID
            if (!setCloneRef) return;
            c_gasify[COPY_ID] = clone.Id;
        }

        public static void ForLoadedData(ForgeDataNode node)
        {
            node.editorTags = GetEditorTags(node);
            if (node.editorTags is null)
            {
                ForHazardousData(node);
            }
        }

        public static void ForHazardousData(ForgeDataNode node)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;

            gasify[EDITABLE] = true;
            gasify[IS_CREATED] = true;
            gasify[IS_SAVED_COPY] = false;
            gasify[HAS_UNSAVED_WORK] = false;
            gasify[EDITOR_CATEGORIES] = new List<Tag>();
            gasify[REFERENCES] = new Dictionary<int, HashSet<string>>();
            gasify[REFERENCED_BY] = new Dictionary<int, HashSet<string>>();

            gasify[VALID_FOR_GAMEPLAY] = false;
            
            gasify[BAD_JSON_LOAD] = true;
            
            ComputeCompositeTagValues(node, gasify);
        }
        
        #endregion
        
        #region Updating Tags
        
        public static void OnSave(ForgeDataNode node)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            
            gasify[HAS_UNSAVED_WORK] = false;

            ComputeCompositeTagValues(node, gasify);
        }
        
        public static void OnCreate(ForgeDataNode node)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            
            gasify[IS_CREATED] = true;
            
            ComputeCompositeTagValues(node, gasify);
        }

        public static void OnCreateTemplate(ForgeDataNode node)
        {
            OnCreate(node);
            
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            
            gasify[IS_TEMPLATE] = true;
            gasify[IS_SAVED_COPY] = false;
            gasify[HAS_UNSAVED_WORK] = false;
            
            ComputeCompositeTagValues(node, gasify);
        }

        /// <summary>
        /// Node references another node (Id = _id) by field
        /// </summary>
        /// <param name="node">The node which is referencing another node</param>
        /// <param name="reference">The referenced node</param>
        /// <param name="field">The field from reference which references node</param>
        public static void SetReference(this ForgeDataNode node, ForgeDataNode reference, string field)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            var references = gasify[REFERENCES] as Dictionary<int, HashSet<string>>;

            SetReferenceInPlace(references, reference.Id, field);

            reference.SetReferencedBy(node, field);
        }
        
        public static void SetReferencedBy(this ForgeDataNode node, ForgeDataNode reference, string field)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            var references = gasify[REFERENCED_BY] as Dictionary<int, HashSet<string>>;

            SetReferenceInPlace(references, reference.Id, field);
        }

        private static void SetReferenceInPlace(Dictionary<int, HashSet<string>> refs, int _id, string field)
        {
            refs.TryAdd(_id, new HashSet<string>());
            refs[_id].Add(field);
        }

        public static void SetClassification(this ForgeDataNode node, Tag c)
        {
            var gasify = GetGasifyTags(node);
            var cats = gasify[EDITOR_CATEGORIES] as List<Tag>;
            if (cats.Contains(c)) return;
            cats.Add(c);
            gasify[EDITOR_CATEGORIES] = cats;
        }

        // fieldName, focus, msg, descr
        public static void SetAlert(this ForgeDataNode node, Tag code, (string, string, string, string) details)
        {
            var editor = GetEditorTags(node);
            var gasify = editor[GASIFY_TAGS] as Dictionary<Tag, object>;
            var alerts = gasify[code] as List<(string, string, string, string)>;

            //
            if (alerts.Any(detail => detail.Item1 == details.Item1)) return;
            
            alerts.Add(details);
        }
        
        #endregion
        
        #region Settings

        public static class Settings
        {
            #region Master

            /// <summary>
            /// Indicates the name of the last opened framework
            /// </summary>
            public static Tag ACTIVE_FRAMEWORK => Tag.Generate("ESM_ACTIVE_FRAMEWORK");
            public static Tag LAST_OPENED_FRAMEWORK => Tag.Generate("ESM_LAST_OPENED_FRAMEWORK");

            public static Tag SESSION_ID => Tag.Generate("ESM_SESSION_ID");

            public static Tag PROMPT_WHEN_INITIALIZE_EDITOR => Tag.Generate("ESM_PROMPT_WHEN_INITIALIZE_EDITOR");

            #endregion
            
            #region Local

            public static Tag FW_HAS_UNSAVED_WORK => Tag.Generate("ESL_FW_HAS_UNSAVED_WORK");

            /// <summary>
            /// Indicates root level template assignments to new data. When an item is drafted, the template is loaded
            /// </summary>
            public static Tag ROOT_TEMPLATE_ASSIGNMENTS => Tag.Generate("ESL_ROOT_TEMPLATE_ASSIGNMENTS");

            public static Tag QUICK_TEMPLATE_ASSIGNMENTS => Tag.Generate("ESL_QUICK_TEMPLATE_ASSIGNMENTS");

            /// <summary>
            /// Indicates the date a framework was created
            /// </summary>
            public static Tag DATE_CREATED => Tag.Generate("ESL_DATE_CREATED");

            public static Tag CATEGORIES => Tag.Generate("ESL_CATEGORIES");

            /// <summary>
            /// Dictionary<Tag, DataType>
            /// </summary>
            public static Tag CATEGORY_ASSIGNMENTS => Tag.Generate("ESL_CATEGORY_ASSIGNMENTS");

            public static Tag MAX_CONSOLE_ENTRIES => Tag.Generate("ESL_MAX_CONSOLE_ENTRIES");

            #endregion
        }
        
        #endregion
        
        #region About

        public static class AboutPlayForge
        {
            
        }
        
        #endregion
    }
}
