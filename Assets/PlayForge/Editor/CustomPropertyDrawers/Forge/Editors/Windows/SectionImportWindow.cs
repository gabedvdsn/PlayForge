using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Generic window for importing section property values from another asset of the same type.
    /// </summary>
    public class SectionImportWindow : EditorWindow
    {
        private SerializedObject _targetObject;
        private string[] _propertyPaths;
        private string _sectionName;
        private Action _onImportComplete;
        
        private List<(UnityEngine.Object asset, string label)> _sources = new();
        private string _searchFilter = "";
        private Vector2 _scrollPosition;
        
        public static void Show(SerializedObject targetObject, string sectionName, string[] propertyPaths, Action onImportComplete = null)
        {
            if (targetObject?.targetObject == null || propertyPaths == null || propertyPaths.Length == 0)
            {
                Debug.LogWarning("SectionImportWindow: Invalid parameters");
                return;
            }
            
            var window = GetWindow<SectionImportWindow>(true, $"Import {sectionName}");
            window._targetObject = targetObject;
            window._propertyPaths = propertyPaths;
            window._sectionName = sectionName;
            window._onImportComplete = onImportComplete;
            window.minSize = new Vector2(450, 400);
            window.maxSize = new Vector2(650, 600);
            window.RefreshSources();
            window.ShowUtility();
        }
        
        private void RefreshSources()
        {
            _sources.Clear();
            
            if (_targetObject?.targetObject == null) return;
            
            var targetType = _targetObject.targetObject.GetType();
            var targetAsset = _targetObject.targetObject;
            
            // Find all assets of the same type
            var guids = AssetDatabase.FindAssets($"t:{targetType.Name}");
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, targetType);
                
                // Skip the target asset itself
                if (asset == null || asset == targetAsset) continue;
                
                // Get display name
                string displayName = asset.name;
                
                // Try to get a better name if the asset has a Definition.Name or similar
                var so = new SerializedObject(asset);
                var nameProp = so.FindProperty("Definition.Name");
                if (nameProp != null && !string.IsNullOrEmpty(nameProp.stringValue))
                {
                    displayName = $"{nameProp.stringValue} ({asset.name})";
                }
                
                _sources.Add((asset, displayName));
            }
            
            // Sort by name
            _sources = _sources.OrderBy(s => s.label).ToList();
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            
            // Header
            var headerLabel = new Label($"Import {_sectionName}");
            headerLabel.style.fontSize = 14;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.color = Colors.AccentCyan;
            headerLabel.style.marginBottom = 4;
            root.Add(headerLabel);
            
            // Info
            var targetType = _targetObject?.targetObject?.GetType();
            var infoLabel = new Label($"Select a {targetType?.Name ?? "asset"} to import {_sectionName} data from:");
            infoLabel.style.fontSize = 11;
            infoLabel.style.color = Colors.HintText;
            infoLabel.style.marginBottom = 8;
            root.Add(infoLabel);
            
            // Properties being imported
            var propsLabel = new Label($"Properties: {string.Join(", ", _propertyPaths ?? Array.Empty<string>())}");
            propsLabel.style.fontSize = 9;
            propsLabel.style.color = Colors.HintText;
            propsLabel.style.marginBottom = 8;
            propsLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(propsLabel);
            
            // Search row
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.marginBottom = 8;
            
            var searchField = new TextField { value = _searchFilter };
            searchField.style.flexGrow = 1;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue;
                RebuildList();
            });
            searchRow.Add(searchField);
            
            var refreshBtn = new Button(() => { RefreshSources(); RebuildList(); }) { text = "↻" };
            refreshBtn.style.width = 24;
            refreshBtn.style.marginLeft = 4;
            searchRow.Add(refreshBtn);
            
            root.Add(searchRow);
            
            // Results list
            var scrollView = new ScrollView { name = "ResultsList" };
            scrollView.style.flexGrow = 1;
            scrollView.style.backgroundColor = Colors.SubsectionBackground;
            scrollView.style.borderTopLeftRadius = 4;
            scrollView.style.borderTopRightRadius = 4;
            scrollView.style.borderBottomLeftRadius = 4;
            scrollView.style.borderBottomRightRadius = 4;
            root.Add(scrollView);
            
            // Footer
            var footer = new Label { name = "Footer" };
            footer.style.fontSize = 10;
            footer.style.color = Colors.HintText;
            footer.style.marginTop = 8;
            root.Add(footer);
            
            // Initial build
            root.schedule.Execute(() =>
            {
                if (_sources.Count == 0) RefreshSources();
                RebuildList();
                UpdateFooter();
            });
        }
        
        private void UpdateFooter()
        {
            var footer = rootVisualElement?.Q<Label>("Footer");
            if (footer != null)
            {
                var targetType = _targetObject?.targetObject?.GetType();
                footer.text = $"{_sources.Count} {targetType?.Name ?? "assets"} available";
            }
        }
        
        private void RebuildList()
        {
            var scrollView = rootVisualElement?.Q<ScrollView>("ResultsList");
            if (scrollView == null) return;
            scrollView.Clear();
            
            var filtered = string.IsNullOrEmpty(_searchFilter)
                ? _sources
                : _sources.Where(s => s.label.ToLower().Contains(_searchFilter.ToLower())).ToList();
            
            foreach (var (asset, label) in filtered)
            {
                var item = CreateListItem(asset, label);
                scrollView.Add(item);
            }
            
            if (filtered.Count == 0)
            {
                var emptyLabel = new Label("No assets found");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.paddingTop = 20;
                scrollView.Add(emptyLabel);
            }
        }
        
        private VisualElement CreateListItem(UnityEngine.Object asset, string label)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.paddingTop = 6;
            item.style.paddingBottom = 6;
            item.style.marginLeft = 4;
            item.style.marginRight = 4;
            item.style.marginBottom = 2;
            item.style.backgroundColor = Colors.ItemBackground;
            item.style.borderTopLeftRadius = 3;
            item.style.borderTopRightRadius = 3;
            item.style.borderBottomLeftRadius = 3;
            item.style.borderBottomRightRadius = 3;
            
            item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = Colors.ButtonHover);
            item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = Colors.ItemBackground);
            
            // Icon based on asset type
            var icon = new Label("◆");
            icon.style.color = Colors.GetAssetColor(asset.GetType());
            icon.style.fontSize = 12;
            icon.style.marginRight = 8;
            item.Add(icon);
            
            // Label
            var labelElement = new Label(label);
            labelElement.style.flexGrow = 1;
            labelElement.style.color = Colors.LabelText;
            labelElement.style.fontSize = 11;
            item.Add(labelElement);
            
            // Preview button (optional - shows what will be imported)
            var previewBtn = new Button(() => PreviewImport(asset)) { text = "👁", tooltip = "Preview" };
            previewBtn.style.width = 24;
            previewBtn.style.height = 20;
            previewBtn.style.marginRight = 4;
            previewBtn.style.fontSize = 10;
            item.Add(previewBtn);
            
            // Import button
            var importBtn = new Button(() => DoImport(asset)) { text = "Import" };
            importBtn.style.paddingLeft = 8;
            importBtn.style.paddingRight = 8;
            importBtn.style.height = 20;
            importBtn.style.fontSize = 10;
            item.Add(importBtn);
            
            return item;
        }
        
        private void PreviewImport(UnityEngine.Object sourceAsset)
        {
            if (sourceAsset == null || _propertyPaths == null) return;
            
            var sourceSO = new SerializedObject(sourceAsset);
            var preview = new System.Text.StringBuilder();
            preview.AppendLine($"Preview import from: {sourceAsset.name}");
            preview.AppendLine("─────────────────────────────");
            
            foreach (var path in _propertyPaths)
            {
                var prop = sourceSO.FindProperty(path);
                if (prop != null)
                {
                    string value = GetPropertyPreviewString(prop);
                    preview.AppendLine($"• {path}: {value}");
                }
                else
                {
                    preview.AppendLine($"• {path}: (not found)");
                }
            }
            
            EditorUtility.DisplayDialog($"Import Preview - {_sectionName}", preview.ToString(), "OK");
        }
        
        private string GetPropertyPreviewString(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer => prop.intValue.ToString(),
                SerializedPropertyType.Float => prop.floatValue.ToString("F2"),
                SerializedPropertyType.Boolean => prop.boolValue.ToString(),
                SerializedPropertyType.String => string.IsNullOrEmpty(prop.stringValue) ? "(empty)" : prop.stringValue,
                SerializedPropertyType.Enum => prop.enumDisplayNames[prop.enumValueIndex],
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "(none)",
                SerializedPropertyType.ArraySize => $"[{prop.arraySize} elements]",
                _ => $"({prop.propertyType})"
            };
        }
        
        private void DoImport(UnityEngine.Object sourceAsset)
        {
            if (sourceAsset == null || _targetObject?.targetObject == null || _propertyPaths == null) return;
            
            var sourceSO = new SerializedObject(sourceAsset);
            
            // Record undo
            Undo.RecordObject(_targetObject.targetObject, $"Import {_sectionName}");
            
            // Copy each property
            foreach (var path in _propertyPaths)
            {
                var sourceProp = sourceSO.FindProperty(path);
                var targetProp = _targetObject.FindProperty(path);
                
                if (sourceProp != null && targetProp != null)
                {
                    CopySerializedProperty(sourceProp, targetProp);
                }
            }
            
            _targetObject.ApplyModifiedProperties();
            
            _onImportComplete?.Invoke();
            
            Debug.Log($"Imported {_sectionName} from {sourceAsset.name}");
            Close();
        }
        
        /// <summary>
        /// Deep copies a serialized property value.
        /// </summary>
        public static void CopySerializedProperty(SerializedProperty source, SerializedProperty target)
        {
            if (source == null || target == null) return;
            if (source.propertyType != target.propertyType) return;
            
            switch (source.propertyType)
            {
                case SerializedPropertyType.Integer:
                    target.intValue = source.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    target.boolValue = source.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    target.floatValue = source.floatValue;
                    break;
                case SerializedPropertyType.String:
                    target.stringValue = source.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    target.colorValue = source.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    target.objectReferenceValue = source.objectReferenceValue;
                    break;
                case SerializedPropertyType.LayerMask:
                    target.intValue = source.intValue;
                    break;
                case SerializedPropertyType.Enum:
                    target.enumValueIndex = source.enumValueIndex;
                    break;
                case SerializedPropertyType.Vector2:
                    target.vector2Value = source.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    target.vector3Value = source.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    target.vector4Value = source.vector4Value;
                    break;
                case SerializedPropertyType.Rect:
                    target.rectValue = source.rectValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    target.animationCurveValue = source.animationCurveValue;
                    break;
                case SerializedPropertyType.Bounds:
                    target.boundsValue = source.boundsValue;
                    break;
                case SerializedPropertyType.Quaternion:
                    target.quaternionValue = source.quaternionValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    target.vector2IntValue = source.vector2IntValue;
                    break;
                case SerializedPropertyType.Vector3Int:
                    target.vector3IntValue = source.vector3IntValue;
                    break;
                case SerializedPropertyType.RectInt:
                    target.rectIntValue = source.rectIntValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    target.boundsIntValue = source.boundsIntValue;
                    break;
                case SerializedPropertyType.ManagedReference:
                    // For SerializeReference fields, we need to copy the managed reference
                    target.managedReferenceValue = source.managedReferenceValue != null 
                        ? CloneManagedReference(source.managedReferenceValue) 
                        : null;
                    break;
                case SerializedPropertyType.Generic:
                    // For arrays and complex types, copy children
                    if (source.isArray)
                    {
                        target.arraySize = source.arraySize;
                        for (int i = 0; i < source.arraySize; i++)
                        {
                            CopySerializedProperty(source.GetArrayElementAtIndex(i), target.GetArrayElementAtIndex(i));
                        }
                    }
                    else
                    {
                        // Copy all children
                        var sourceChild = source.Copy();
                        var targetChild = target.Copy();
                        var sourceEnd = source.GetEndProperty();
                        
                        if (sourceChild.NextVisible(true))
                        {
                            do
                            {
                                if (SerializedProperty.EqualContents(sourceChild, sourceEnd)) break;
                                
                                var targetMatch = target.FindPropertyRelative(sourceChild.name);
                                if (targetMatch != null)
                                {
                                    CopySerializedProperty(sourceChild, targetMatch);
                                }
                            } while (sourceChild.NextVisible(false));
                        }
                    }
                    break;
            }
        }
        
        private static object CloneManagedReference(object source)
        {
            if (source == null) return null;
            
            try
            {
                var json = JsonUtility.ToJson(source);
                var clone = Activator.CreateInstance(source.GetType());
                JsonUtility.FromJsonOverwrite(json, clone);
                return clone;
            }
            catch
            {
                return null;
            }
        }
    }
    
    /// <summary>
    /// Utility for clearing section properties to default values.
    /// </summary>
    public static class SectionClearUtility
    {
        /// <summary>
        /// Shows a confirmation dialog and clears the specified properties if confirmed.
        /// </summary>
        public static void ClearWithConfirmation(
            SerializedObject targetObject, 
            string sectionName, 
            string[] propertyPaths,
            Action onClearComplete = null,
            Func<string, object> getDefaultValue = null)
        {
            if (targetObject?.targetObject == null || propertyPaths == null || propertyPaths.Length == 0)
                return;
            
            bool confirmed = EditorUtility.DisplayDialog(
                $"Clear {sectionName}",
                $"Are you sure you want to reset all {sectionName} properties to their default values?",
                "Clear",
                "Cancel");
            
            if (!confirmed) return;
            
            // Record undo
            Undo.RecordObject(targetObject.targetObject, $"Clear {sectionName}");
            
            foreach (var path in propertyPaths)
            {
                var prop = targetObject.FindProperty(path);
                if (prop == null) continue;
                
                // Check if custom default provided
                if (getDefaultValue != null)
                {
                    var defaultValue = getDefaultValue(path);
                    if (defaultValue != null)
                    {
                        SetPropertyValue(prop, defaultValue);
                        continue;
                    }
                }
                
                // Otherwise reset to type default
                ResetPropertyToDefault(prop);
            }
            
            targetObject.ApplyModifiedProperties();
            onClearComplete?.Invoke();
            
            Debug.Log($"Cleared {sectionName} properties");
        }
        
        private static void SetPropertyValue(SerializedProperty prop, object value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value?.ToString() ?? "";
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = Convert.ToInt32(value);
                    break;
                // Add more as needed
            }
        }
        
        private static void ResetPropertyToDefault(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = "";
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = Color.white;
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = Vector2.zero;
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = Vector3.zero;
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = Vector4.zero;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    prop.animationCurveValue = new AnimationCurve();
                    break;
                case SerializedPropertyType.ManagedReference:
                    prop.managedReferenceValue = null;
                    break;
                case SerializedPropertyType.Generic:
                    if (prop.isArray)
                    {
                        prop.arraySize = 0;
                    }
                    else
                    {
                        // Reset children
                        var child = prop.Copy();
                        var end = prop.GetEndProperty();
                        if (child.NextVisible(true))
                        {
                            do
                            {
                                if (SerializedProperty.EqualContents(child, end)) break;
                                ResetPropertyToDefault(child);
                            } while (child.NextVisible(false));
                        }
                    }
                    break;
            }
        }
    }
}
