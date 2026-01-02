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
            };
            
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
                                    ValuePreview = GetValuePreview(scaler)
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
            var groups = filteredList.GroupBy(r => r.ScalerTypeName).OrderBy(g => g.Key);
            
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
    // Quick Fill Window
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class QuickFillWindow : EditorWindow
    {
        public enum FillType { Constant, Linear, Exponential, Logarithmic, Additive, Multiplicative, Steps, Sigmoid, DiminishingReturns }
        
        private SerializedProperty targetProp;
        private VisualElement rootElement;
        private Action onApply;
        private FillType fillType;
        
        private float startVal = 1f, endVal = 10f, constVal = 1f, baseVal = 1f;
        private float incr = 0.5f, mult = 1.1f, exp = 2f;
        private float steepness = 5f, midpoint = 0.5f, diminishFactor = 0.5f;
        private int steps = 5;
        
        public static void Show(SerializedProperty prop, VisualElement root, FillType type, Action onApply)
        {
            var w = GetWindow<QuickFillWindow>(true, GetWindowTitle(type));
            w.targetProp = prop;
            w.rootElement = root;
            w.onApply = onApply;
            w.fillType = type;
            
            var lvp = prop.FindPropertyRelative("LevelValues");
            if (lvp != null && lvp.arraySize > 0)
            {
                w.startVal = w.baseVal = w.constVal = lvp.GetArrayElementAtIndex(0).floatValue;
                w.endVal = lvp.GetArrayElementAtIndex(lvp.arraySize - 1).floatValue;
            }
            
            w.minSize = new Vector2(300, 260);
            w.maxSize = new Vector2(380, 320);
            w.Show();
        }
        
        private static string GetWindowTitle(FillType type) => type switch
        {
            FillType.DiminishingReturns => "Diminishing Returns Fill",
            FillType.Sigmoid => "S-Curve Fill",
            _ => type.ToString() + " Fill"
        };
        
        void OnGUI()
        {
            if (targetProp?.serializedObject == null) { Close(); return; }
            
            EditorGUILayout.Space(6);
            DrawParameters();
            EditorGUILayout.Space(6);
            
            var lvp = targetProp.FindPropertyRelative("LevelValues");
            int count = lvp?.arraySize ?? 10;
            if (count <= 0) count = 10;
            
            var preview = CalculatePreview(count);
            
            var curve = new AnimationCurve();
            for (int i = 0; i < count; i++)
                curve.AddKey((float)i / Mathf.Max(1, count - 1), preview[i]);
            
            float minV = preview.Min(), maxV = preview.Max();
            float range = maxV - minV;
            if (range < 0.001f) range = 1f;
            
            EditorGUILayout.CurveField(curve, Colors.AccentGreen, new Rect(0, minV - range * 0.1f, 1, range * 1.2f), GUILayout.Height(70));
            EditorGUILayout.LabelField($"Lv1: {preview[0]:F2}  |  Lv{count}: {preview[^1]:F2}", EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.Space(8);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel")) Close();
            GUI.backgroundColor = Colors.AccentGreen;
            if (GUILayout.Button("Apply")) { Apply(preview); Close(); }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawParameters()
        {
            switch (fillType)
            {
                case FillType.Constant:
                    constVal = EditorGUILayout.FloatField("Value", constVal);
                    break;
                case FillType.Linear:
                    startVal = EditorGUILayout.FloatField("Start (Lv1)", startVal);
                    endVal = EditorGUILayout.FloatField("End (Max)", endVal);
                    break;
                case FillType.Exponential:
                    startVal = EditorGUILayout.FloatField("Start", startVal);
                    endVal = EditorGUILayout.FloatField("End", endVal);
                    exp = EditorGUILayout.Slider("Exponent", exp, 0.1f, 5f);
                    EditorGUILayout.HelpBox("Higher = slower start, faster end", MessageType.None);
                    break;
                case FillType.Logarithmic:
                    startVal = EditorGUILayout.FloatField("Start", startVal);
                    endVal = EditorGUILayout.FloatField("End", endVal);
                    break;
                case FillType.Additive:
                    baseVal = EditorGUILayout.FloatField("Base (Lv1)", baseVal);
                    incr = EditorGUILayout.FloatField("+ Per Level", incr);
                    break;
                case FillType.Multiplicative:
                    baseVal = EditorGUILayout.FloatField("Base (Lv1)", baseVal);
                    mult = EditorGUILayout.FloatField("× Per Level", mult);
                    EditorGUILayout.HelpBox($"Lv10 ≈ {baseVal * Mathf.Pow(mult, 9):F1}", MessageType.None);
                    break;
                case FillType.Steps:
                    startVal = EditorGUILayout.FloatField("Start", startVal);
                    endVal = EditorGUILayout.FloatField("End", endVal);
                    steps = EditorGUILayout.IntSlider("Steps", steps, 2, 20);
                    break;
                case FillType.Sigmoid:
                    startVal = EditorGUILayout.FloatField("Start", startVal);
                    endVal = EditorGUILayout.FloatField("End", endVal);
                    steepness = EditorGUILayout.Slider("Steepness", steepness, 1f, 15f);
                    midpoint = EditorGUILayout.Slider("Midpoint", midpoint, 0.2f, 0.8f);
                    break;
                case FillType.DiminishingReturns:
                    startVal = EditorGUILayout.FloatField("Start", startVal);
                    endVal = EditorGUILayout.FloatField("Cap (Asymptote)", endVal);
                    diminishFactor = EditorGUILayout.Slider("Falloff", diminishFactor, 0.1f, 2f);
                    EditorGUILayout.HelpBox("Approaches cap but never reaches", MessageType.None);
                    break;
            }
        }
        
        private float[] CalculatePreview(int count)
        {
            var preview = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                preview[i] = fillType switch
                {
                    FillType.Constant => constVal,
                    FillType.Linear => Mathf.Lerp(startVal, endVal, t),
                    FillType.Exponential => Mathf.Lerp(startVal, endVal, Mathf.Pow(t, exp)),
                    FillType.Logarithmic => Mathf.Lerp(startVal, endVal, Mathf.Sqrt(t)),
                    FillType.Additive => baseVal + incr * i,
                    FillType.Multiplicative => baseVal * Mathf.Pow(mult, i),
                    FillType.Steps => Mathf.Lerp(startVal, endVal, Mathf.Floor(t * steps) / steps),
                    FillType.Sigmoid => CalculateSigmoid(t),
                    FillType.DiminishingReturns => startVal + (endVal - startVal) * (1f - Mathf.Exp(-diminishFactor * t * 5f)),
                    _ => 1f
                };
            }
            return preview;
        }
        
        private float CalculateSigmoid(float t)
        {
            float x = (t - midpoint) * steepness;
            float sigmoid = 1f / (1f + Mathf.Exp(-x));
            return Mathf.Lerp(startVal, endVal, sigmoid);
        }
        
        void Apply(float[] values)
        {
            var lvp = targetProp?.FindPropertyRelative("LevelValues");
            var sp = targetProp?.FindPropertyRelative("Scaling");
            var ip = targetProp?.FindPropertyRelative("Interpolation");
            if (lvp == null) return;
            
            for (int i = 0; i < lvp.arraySize && i < values.Length; i++)
                lvp.GetArrayElementAtIndex(i).floatValue = values[i];
            
            var curve = new AnimationCurve();
            for (int i = 0; i < values.Length; i++)
                curve.AddKey((float)i / Mathf.Max(1, values.Length - 1), values[i]);
            
            var mode = ip != null ? (EScalerInterpolation)ip.enumValueIndex : EScalerInterpolation.Linear;
            var tm = mode switch
            {
                EScalerInterpolation.Constant => UnityEditor.AnimationUtility.TangentMode.Constant,
                EScalerInterpolation.Linear => UnityEditor.AnimationUtility.TangentMode.Linear,
                _ => UnityEditor.AnimationUtility.TangentMode.ClampedAuto
            };
            
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, tm);
                AnimationUtility.SetKeyRightTangentMode(curve, i, tm);
            }
            
            if (sp != null) sp.animationCurveValue = curve;
            targetProp.serializedObject.ApplyModifiedProperties();
            onApply?.Invoke();
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
                AnimationUtility.SetKeyLeftTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
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