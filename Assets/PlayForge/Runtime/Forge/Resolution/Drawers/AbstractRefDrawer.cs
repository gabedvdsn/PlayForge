using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FarEmerald.PlayForge.Extended;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractRefDrawer : PropertyDrawer
    {
        private const int NoneId = 0;
        
        protected abstract IReadOnlyList<DataEntry> GetEntries(FrameworkIndex idx);

        protected virtual IEnumerable<DataEntry> ApplyValidation(FrameworkProject fp, SerializedProperty property, IEnumerable<DataEntry> source)
        {
            var defaultAttrs = GetFieldAttributes<ForgeFilterName>(property).ToArray();
            if (defaultAttrs.Length == 0) return source;

            return source.Where(e => defaultAttrs.Any(a => a.Names.Any(_name => e.Name.StartsWith(_name, StringComparison.InvariantCultureIgnoreCase))));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var idProp = property.FindPropertyRelative("Id");
            if (idProp == null || idProp.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "<invalid ref: expected int 'Id'>");
                return;
            }

            int currentId = idProp.intValue;
            var idx = LoadActiveFrameworkIndex();
            var raw = idx is not null ? (GetEntries(idx) ?? Array.Empty<DataEntry>()) : Array.Empty<DataEntry>();
            var validated = ApplyValidation(ForgeStores.LoadActiveFramework(), property, raw) ?? Enumerable.Empty<DataEntry>();
            var list = validated.ToList(); // defer sorting to popup

            // Draw field like an object field: label + clickable body + clear "X"
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var fieldRect = EditorGUI.PrefixLabel(position, label);

                var clearRect = new Rect(fieldRect.xMax - 18, fieldRect.y, 18, fieldRect.height);
                var textRect  = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - 20, fieldRect.height);

                using (new EditorGUI.DisabledScope(idx is null))
                {
                    // Field body (click to open popup)
                    var currentName = PrettyName(list, currentId);
                    if (GUI.Button(textRect, currentName, EditorStyles.popup))
                    {
                        SearchableRefPopup.Show(textRect, label.text, list, currentId, newId =>
                        {
                            idProp.intValue = newId;
                            property.serializedObject.ApplyModifiedProperties();
                            GUI.changed = true;
                        });
                    }

                    // Clear button
                    if (GUI.Button(clearRect, "×"))
                    {
                        idProp.intValue = NoneId;
                        property.serializedObject.ApplyModifiedProperties();
                        GUI.changed = true;
                    }
                }

                // Help when index missing or id no longer exists
                var helpRect = new Rect(position.x, position.yMax + 2, position.width, EditorGUIUtility.singleLineHeight);
                if (idx == null)
                    EditorGUI.HelpBox(helpRect, "Framework Index not found. Save framework or build the index.", MessageType.Info);
                else if (currentId != NoneId && !raw.Any(e => e != null && e.Id == currentId))
                    EditorGUI.HelpBox(helpRect, "Selected item no longer exists in this framework.", MessageType.Warning);
            }

        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var idProp = property.FindPropertyRelative("id");
            bool showHelp = false;

            var idx = LoadActiveFrameworkIndex();
            if (idx is null) showHelp = true;
            else if (idProp != null && idProp.propertyType == SerializedPropertyType.Integer)
            {
                int id = idProp.intValue;
                if (id != NoneId)
                {
                    var list = GetEntries(idx) ?? Array.Empty<DataEntry>();
                    if (!list.Any(e => e != null && e.Id == id)) showHelp = true;
                }
            }

            return EditorGUIUtility.singleLineHeight + (showHelp ? EditorGUIUtility.singleLineHeight + 2 : 0);
        }
        
        protected static FrameworkIndex LoadActiveFrameworkIndex()
        {
            // var key = EditorPrefs.GetString(GasifyTools.CurrentFrameworkBuildKey, string.Empty);
            var key = ForgeStores.ActiveFrameworkKey;
            
            if (key is null) return null;
            if (string.IsNullOrEmpty(key)) return null;

            return ForgeStores.LoadIndex(key);
        }
        
        static GUIContent PrettyName(List<DataEntry> list, int id)
        {
            if (id == NoneId) return new GUIContent("<None>");
            var e = list.FirstOrDefault(x => x != null && x.Id == id);
            return new GUIContent(e == null ? $"<Missing>" : (string.IsNullOrEmpty(e.Name) ? $"<{e.Id}>" : e.Name));
        }
        
        static readonly Dictionary<(Type host, string path, Type attr), object[]> _attrCache = new();

        /// <summary>Get attributes defined on the serialized field being drawn.</summary>
        protected IEnumerable<TAttr> GetFieldAttributes<TAttr>(SerializedProperty property) where TAttr : System.Attribute
        {
            // 1) Try Unity's supplied fieldInfo first (works for most cases)
            if (fieldInfo != null)
                return fieldInfo.GetCustomAttributes(typeof(TAttr), inherit: true).Cast<TAttr>();

            // 2) Fallback: resolve FieldInfo by walking propertyPath
            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0) return Enumerable.Empty<TAttr>();
            var hostType = targets[0].GetType();
            var key = (hostType, property.propertyPath, typeof(TAttr));
            if (_attrCache.TryGetValue(key, out var cached))
                return cached.Cast<TAttr>();

            var fi = ResolveFieldInfo(hostType, property.propertyPath);
            var arr = fi != null
                ? fi.GetCustomAttributes(typeof(TAttr), inherit: true).Cast<TAttr>().ToArray()
                : Array.Empty<TAttr>();

            _attrCache[key] = arr.Cast<object>().ToArray();
            return arr;
        }

        // Walks something like "myList.Array.data[2].field.subField"
        static FieldInfo ResolveFieldInfo(Type host, string propertyPath)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type curType = host;
            FieldInfo lastField = null;

            // split path
            var parts = propertyPath.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];

                if (p == "Array")
                {
                    // Next should be "data[<index>]"
                    i++; // skip "data[x]"
                    // move type to element type
                    if (curType.IsArray)
                        curType = curType.GetElementType();
                    else if (curType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(curType))
                        curType = curType.GetGenericArguments()[0];
                    continue;
                }

                // normal field step
                var fi = curType.GetField(p, flags);
                if (fi == null) return lastField; // bail out with best guess
                lastField = fi;
                curType = fi.FieldType;
            }

            return lastField;
        }
    }
}
