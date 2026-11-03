using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // Import Cache (partial class extension)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public partial class ScalerDrawer
    {
        private static List<ScalerImportRecord> _importCache = null;
        private static DateTime _lastImportCacheTime = DateTime.MinValue;
        private const float IMPORT_CACHE_LIFETIME_SECONDS = 120f;
        
        public class ScalerImportRecord
        {
            public ScriptableObject Asset;
            public string AssetName;
            public string AssetTypeName;
            public string AssetTypeIcon;
            public string FieldPath;
            public string Context;
            public AbstractScaler Scaler;
            public string ScalerTypeName;
            public string ValuePreview;
            public bool IsTemplate;
        }
        
        public static List<ScalerImportRecord> GetImportCache()
        {
            if (_importCache == null || (DateTime.Now - _lastImportCacheTime).TotalSeconds > IMPORT_CACHE_LIFETIME_SECONDS)
            {
                _importCache = DiscoverAllScalersForImport();
                _lastImportCacheTime = DateTime.Now;
            }
            return _importCache;
        }
        
        public static void InvalidateImportCache() => _importCache = null;
        
        private static List<ScalerImportRecord> DiscoverAllScalersForImport()
        {
            var records = new List<ScalerImportRecord>();
            var assetTypeInfo = new Dictionary<Type, (string icon, string name)>
            {
                { typeof(Ability), ("⚡", "Ability") },
                { typeof(GameplayEffect), ("✦", "Effect") },
                { typeof(Attribute), ("📈", "Attribute") },
                { typeof(AttributeSet), ("📊", "AttrSet") },
                { typeof(EntityIdentity), ("👤", "Entity") },
                { typeof(ScalerPipeline), ("📋", "Template") },
            };
            
            // First, add ScalerTemplate assets (they appear first and are highlighted)
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(ScalerPipeline)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<ScalerPipeline>(path);
                if (template?.Scaler == null) continue;
                
                var scaler = template.Scaler;
                var scalerTypeName = scaler.GetType().Name;
                if (scalerTypeName.EndsWith("Scaler"))
                    scalerTypeName = scalerTypeName.Substring(0, scalerTypeName.Length - 6);
                
                records.Add(new ScalerImportRecord
                {
                    Asset = template,
                    AssetName = template.DisplayName,
                    AssetTypeName = "Template",
                    AssetTypeIcon = "📋",
                    FieldPath = "Scaler",
                    Context = !string.IsNullOrEmpty(template.Category) ? template.Category : "Template",
                    Scaler = scaler,
                    ScalerTypeName = scalerTypeName,
                    ValuePreview = GetValuePreview(scaler),
                    IsTemplate = true
                });
            }
            
            var assetTypes = new[] { "Ability", "GameplayEffect", "Attribute", "AttributeSet", "EntityIdentity" };
            
            foreach (var typeName in assetTypes)
            {
                var guids = AssetDatabase.FindAssets($"t:{typeName}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset == null) continue;
                    
                    try
                    {
                        var so = new SerializedObject(asset);
                        var iter = so.GetIterator();
                        
                        while (iter.NextVisible(true))
                        {
                            if (iter.propertyType == SerializedPropertyType.ManagedReference && iter.managedReferenceValue is AbstractScaler scaler)
                            {
                                var scalerTypeName = scaler.GetType().Name;
                                if (scalerTypeName.EndsWith("Scaler"))
                                    scalerTypeName = scalerTypeName.Substring(0, scalerTypeName.Length - 6);
                                
                                var assetType = asset.GetType();
                                var (icon, displayName) = assetTypeInfo.TryGetValue(assetType, out var info) ? info : ("?", assetType.Name);
                                
                                string assetDisplayName = asset.name;
                                if (asset is Ability ability && !string.IsNullOrEmpty(ability.GetName()))
                                    assetDisplayName = ability.GetName();
                                else if (asset is GameplayEffect effect && !string.IsNullOrEmpty(effect.GetName()))
                                    assetDisplayName = effect.GetName();
                                else if (asset is Attribute attr && !string.IsNullOrEmpty(attr.Name))
                                    assetDisplayName = attr.Name;
                                else if (asset is EntityIdentity entity && !string.IsNullOrEmpty(entity.GetName()))
                                    assetDisplayName = entity.GetName();
                                
                                records.Add(new ScalerImportRecord
                                {
                                    Asset = asset,
                                    AssetName = assetDisplayName,
                                    AssetTypeName = displayName,
                                    AssetTypeIcon = icon,
                                    FieldPath = iter.propertyPath,
                                    Context = DeriveContext(iter.propertyPath),
                                    Scaler = scaler,
                                    ScalerTypeName = scalerTypeName,
                                    ValuePreview = GetValuePreview(scaler),
                                    IsTemplate = false
                                });
                            }
                        }
                        so.Dispose();
                    }
                    catch { }
                }
            }
            return records;
        }
        
        private static string DeriveContext(string propertyPath)
        {
            var lower = propertyPath.ToLower();
            if (lower.Contains("duration")) return "Duration";
            if (lower.Contains("magnitude") || lower.Contains("impact")) return "Magnitude";
            if (lower.Contains("cost")) return "Cost";
            if (lower.Contains("cooldown")) return "Cooldown";
            if (lower.Contains("period") || lower.Contains("tick")) return "Period";
            if (lower.Contains("stack")) return "Stacking";
            if (lower.Contains("chance") || lower.Contains("probability")) return "Chance";
            if (lower.Contains("range") || lower.Contains("radius")) return "Range";
            if (lower.Contains("damage")) return "Damage";
            if (lower.Contains("heal")) return "Healing";
            return "Value";
        }
        
        private static string GetValuePreview(AbstractScaler scaler)
        {
            if (scaler == null) return "";
            var lvp = scaler.LevelValues;
            if (lvp == null || lvp.Length == 0) return "";
            return lvp.Length == 1 ? $"{lvp[0]:F1}" : $"{lvp[0]:F1}→{lvp[^1]:F1}";
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Scaler Import Window
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class ScalerImportWindow : EditorWindow
    {
        private Action<AbstractScaler> onImport;
        private SerializedProperty targetProp;
        private VisualElement rootElement;
        private string searchFilter = "";
        private string selectedAssetType = null;
        private string selectedContext = null;
        private Vector2 scrollPos;
        
        public static void Show(SerializedProperty prop, VisualElement root, Action<AbstractScaler> onImport)
        {
            var w = GetWindow<ScalerImportWindow>(true, "Import Scaler");
            w.targetProp = prop;
            w.rootElement = root;
            w.onImport = onImport;
            w.minSize = new Vector2(450, 400);
            w.maxSize = new Vector2(600, 600);
            w.Show();
        }
        
        void OnGUI()
        {
            var records = ScalerDrawer.GetImportCache();
            
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Import Scaler from Project", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            
            // Search
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button("✕", GUILayout.Width(24))) searchFilter = "";
            EditorGUILayout.EndHorizontal();
            
            // Filters
            EditorGUILayout.BeginHorizontal();
            var assetTypes = new List<string> { "All" };
            assetTypes.AddRange(records.Select(r => r.AssetTypeName).Distinct().OrderBy(s => s));
            int assetTypeIndex = selectedAssetType == null ? 0 : assetTypes.IndexOf(selectedAssetType);
            if (assetTypeIndex < 0) assetTypeIndex = 0;
            EditorGUILayout.LabelField("Type:", GUILayout.Width(40));
            int newAssetTypeIndex = EditorGUILayout.Popup(assetTypeIndex, assetTypes.ToArray(), GUILayout.Width(80));
            selectedAssetType = newAssetTypeIndex == 0 ? null : assetTypes[newAssetTypeIndex];
            
            var contexts = new List<string> { "All" };
            contexts.AddRange(records.Select(r => r.Context).Distinct().OrderBy(s => s));
            int contextIndex = selectedContext == null ? 0 : contexts.IndexOf(selectedContext);
            if (contextIndex < 0) contextIndex = 0;
            EditorGUILayout.LabelField("Context:", GUILayout.Width(55));
            int newContextIndex = EditorGUILayout.Popup(contextIndex, contexts.ToArray(), GUILayout.Width(80));
            selectedContext = newContextIndex == 0 ? null : contexts[newContextIndex];
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{records.Count} total", EditorStyles.miniLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            
            // Filter
            var filtered = records.AsEnumerable();
            if (!string.IsNullOrEmpty(searchFilter))
            {
                var filter = searchFilter.ToLower();
                filtered = filtered.Where(r => r.AssetName.ToLower().Contains(filter) || r.ScalerTypeName.ToLower().Contains(filter) || r.Context.ToLower().Contains(filter));
            }
            if (selectedAssetType != null) filtered = filtered.Where(r => r.AssetTypeName == selectedAssetType);
            if (selectedContext != null) filtered = filtered.Where(r => r.Context == selectedContext);
            var filteredList = filtered.ToList();
            
            // Results
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            // Show templates section first if any exist
            var templates = filteredList.Where(r => r.IsTemplate).ToList();
            if (templates.Any())
            {
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.7f, 0.3f);
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.LabelField("📋 Templates", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"({templates.Count})", EditorStyles.miniLabel, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
                
                foreach (var record in templates)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    
                    // Green left border indicator for templates
                    var rect = GUILayoutUtility.GetRect(3, 18, GUILayout.Width(3));
                    EditorGUI.DrawRect(rect, Colors.AccentGreen);
                    
                    EditorGUILayout.LabelField(record.AssetTypeIcon, GUILayout.Width(20));
                    
                    if (GUILayout.Button(record.AssetName, EditorStyles.linkLabel, GUILayout.Width(140)))
                    {
                        Selection.activeObject = record.Asset;
                        EditorGUIUtility.PingObject(record.Asset);
                    }
                    
                    GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f, 0.4f);
                    GUILayout.Label(record.Context, EditorStyles.miniButton, GUILayout.Width(70));
                    GUI.backgroundColor = Color.white;
                    
                    EditorGUILayout.LabelField(record.ValuePreview, EditorStyles.miniLabel, GUILayout.Width(70));
                    
                    GUI.backgroundColor = Colors.AccentGreen;
                    if (GUILayout.Button("Import", GUILayout.Width(50)))
                    {
                        onImport?.Invoke(record.Scaler);
                        Close();
                    }
                    GUI.backgroundColor = Color.white;
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.Space(8);
            }
            
            // Group non-template scalers by type
            var nonTemplates = filteredList.Where(r => !r.IsTemplate).ToList();
            var groups = nonTemplates.GroupBy(r => r.ScalerTypeName).OrderBy(g => g.Key);
            
            foreach (var group in groups)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"◆ {group.Key}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"({group.Count()})", EditorStyles.miniLabel, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
                
                foreach (var record in group.Take(15))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    EditorGUILayout.LabelField(record.AssetTypeIcon, GUILayout.Width(20));
                    
                    if (GUILayout.Button(record.AssetName, EditorStyles.linkLabel, GUILayout.Width(140)))
                    {
                        Selection.activeObject = record.Asset;
                        EditorGUIUtility.PingObject(record.Asset);
                    }
                    
                    GUI.backgroundColor = new Color(0.4f, 0.6f, 0.8f, 0.3f);
                    GUILayout.Label(record.Context, EditorStyles.miniButton, GUILayout.Width(70));
                    GUI.backgroundColor = Color.white;
                    
                    EditorGUILayout.LabelField(record.ValuePreview, EditorStyles.miniLabel, GUILayout.Width(70));
                    
                    GUI.backgroundColor = Colors.AccentGreen;
                    if (GUILayout.Button("Import", GUILayout.Width(50)))
                    {
                        onImport?.Invoke(record.Scaler);
                        Close();
                    }
                    GUI.backgroundColor = Color.white;
                    
                    EditorGUILayout.EndHorizontal();
                }
                
                if (group.Count() > 15) EditorGUILayout.LabelField($"    ... and {group.Count() - 15} more", EditorStyles.miniLabel);
                EditorGUILayout.Space(4);
            }
            
            if (!filteredList.Any()) EditorGUILayout.HelpBox("No scalers found matching filters.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(80))) Close();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Curve Editor Window
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class CurveEditorWindow : EditorWindow
    {
        private SerializedProperty targetProp;
        private AnimationCurve curve;
        private float minValue, maxValue;
        private int levelCount;
        
        public static void Show(SerializedProperty prop)
        {
            var lvp = prop.FindPropertyRelative("LevelValues");
            var sp = prop.FindPropertyRelative("Scaling");
            
            var w = GetWindow<CurveEditorWindow>(true, "Edit Scaling Curve");
            w.targetProp = prop;
            
            if (lvp != null && lvp.arraySize > 0)
            {
                w.levelCount = lvp.arraySize;
                w.minValue = float.MaxValue;
                w.maxValue = float.MinValue;
                
                w.curve = new AnimationCurve();
                for (int i = 0; i < lvp.arraySize; i++)
                {
                    float val = lvp.GetArrayElementAtIndex(i).floatValue;
                    float t = lvp.arraySize > 1 ? (float)i / (lvp.arraySize - 1) : 0f;
                    w.curve.AddKey(t, val);
                    w.minValue = Mathf.Min(w.minValue, val);
                    w.maxValue = Mathf.Max(w.maxValue, val);
                }
                
                float range = w.maxValue - w.minValue;
                if (range < 0.001f) range = 1f;
                w.minValue -= range * 0.1f;
                w.maxValue += range * 0.1f;
            }
            else
            {
                w.curve = sp != null ? new AnimationCurve(sp.animationCurveValue.keys) : AnimationCurve.Linear(0, 1, 1, 10);
                w.minValue = 0;
                w.maxValue = 10;
                w.levelCount = 10;
            }
            
            w.minSize = new Vector2(400, 350);
            w.Show();
        }
        
        void OnGUI()
        {
            if (targetProp?.serializedObject == null) { Close(); return; }
            
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Edit Scaling Curve", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"X: Level 1-{levelCount}  |  Y: Value range", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            
            // Range controls
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Y Range:", GUILayout.Width(55));
            minValue = EditorGUILayout.FloatField(minValue, GUILayout.Width(60));
            EditorGUILayout.LabelField("to", GUILayout.Width(20));
            maxValue = EditorGUILayout.FloatField(maxValue, GUILayout.Width(60));
            if (GUILayout.Button("Auto", GUILayout.Width(45))) AutoFitRange();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            
            Rect curveRect = new Rect(0, minValue, 1, maxValue - minValue);
            EditorGUI.BeginChangeCheck();
            curve = EditorGUILayout.CurveField(curve, Colors.AccentGreen, curveRect, GUILayout.Height(180));
            if (EditorGUI.EndChangeCheck())
            {
                var sp = targetProp.FindPropertyRelative("Scaling");
                if (sp != null)
                {
                    sp.animationCurveValue = curve;
                    targetProp.serializedObject.ApplyModifiedProperties();
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Lv1", EditorStyles.miniLabel, GUILayout.Width(30));
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Lv{levelCount}", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(8);
            
            GUI.backgroundColor = Colors.AccentGreen;
            if (GUILayout.Button("Apply Curve to Level Values", GUILayout.Height(28)))
                ApplyCurveToLevelValues();
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space(4);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset from Values")) ResetCurveFromValues();
            if (GUILayout.Button("Smooth Curve")) SmoothCurve();
            EditorGUILayout.EndHorizontal();
            
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close")) Close();
        }
        
        private void AutoFitRange()
        {
            if (curve == null || curve.length == 0) return;
            minValue = float.MaxValue;
            maxValue = float.MinValue;
            foreach (var key in curve.keys)
            {
                minValue = Mathf.Min(minValue, key.value);
                maxValue = Mathf.Max(maxValue, key.value);
            }
            float range = maxValue - minValue;
            if (range < 0.001f) range = 1f;
            minValue -= range * 0.1f;
            maxValue += range * 0.1f;
        }
        
        private void ApplyCurveToLevelValues()
        {
            var lvp = targetProp.FindPropertyRelative("LevelValues");
            var sp = targetProp.FindPropertyRelative("Scaling");
            if (lvp == null) return;
            
            for (int i = 0; i < lvp.arraySize; i++)
            {
                float t = lvp.arraySize > 1 ? (float)i / (lvp.arraySize - 1) : 0f;
                lvp.GetArrayElementAtIndex(i).floatValue = curve.Evaluate(t);
            }
            
            if (sp != null) sp.animationCurveValue = curve;
            targetProp.serializedObject.ApplyModifiedProperties();
            EditorUtility.DisplayDialog("Applied", $"Curve applied to {lvp.arraySize} levels.", "OK");
        }
        
        private void ResetCurveFromValues()
        {
            var lvp = targetProp.FindPropertyRelative("LevelValues");
            if (lvp == null || lvp.arraySize == 0) return;
            
            curve = new AnimationCurve();
            for (int i = 0; i < lvp.arraySize; i++)
            {
                float t = lvp.arraySize > 1 ? (float)i / (lvp.arraySize - 1) : 0f;
                curve.AddKey(t, lvp.GetArrayElementAtIndex(i).floatValue);
            }
            AutoFitRange();
        }
        
        private void SmoothCurve()
        {
            if (curve == null || curve.length < 2) return;
            for (int i = 0; i < curve.length; i++)
            {
                UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
                UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
            }
            var sp = targetProp.FindPropertyRelative("Scaling");
            if (sp != null)
            {
                sp.animationCurveValue = curve;
                targetProp.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}