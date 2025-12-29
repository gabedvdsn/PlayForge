using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Base class for all PlayForge custom editors.
    /// Supports both UXML-based and fully programmatic UI creation.
    /// </summary>
    public abstract class BasePlayForgeEditor : UnityEditor.Editor
    {
        protected VisualElement root;

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods - Must be implemented by derived classes
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Setup collapsible sections. Override if using UXML-based sections.
        /// For programmatic editors, this can be empty as sections handle their own collapse.
        /// </summary>
        protected abstract void SetupCollapsibleSections();
        
        /// <summary>
        /// Called when the Lookup button is clicked.
        /// </summary>
        protected abstract void Lookup();
        
        /// <summary>
        /// Called when the Refresh button is clicked.
        /// </summary>
        protected abstract void Refresh();

        // ═══════════════════════════════════════════════════════════════════════════
        // Virtual Methods - Can be overridden
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected virtual void Visualize() { }
        protected virtual void Import() { }

        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Tag Generation
        // ═══════════════════════════════════════════════════════════════════════════

        protected const string UnnamedAssetTag = "Unnamed";
        protected const string UnknownAssetName = "Error";
        
        protected (string result, bool isUnknown) GenerateAssetTag(string assetName, string fallback)
        {
            if (string.IsNullOrEmpty(assetName))
                return ($"{UnnamedAssetTag} {fallback}!", true);
            
            // Remove special characters (keep only alphanumeric and spaces)
            string cleaned = Regex.Replace(assetName, @"[^a-zA-Z0-9\s]", "");
            
            // Split by spaces and capitalize each word (PascalCase)
            string[] words = cleaned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + 
                               (words[i].Length > 1 ? words[i].Substring(1) : "");
                }
            }
            
            string result = string.Join("", words);
            
            // Ensure it starts with a letter
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = $"{fallback}_" + result;
            }

            return string.IsNullOrEmpty(result) ? ($"{UnnamedAssetTag} {fallback}!", true) : (result, false);
            
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected void MarkDirty(Object obj)
        {
            EditorUtility.SetDirty(obj);
        }
        
        protected void PingAsset(Object obj)
        {
            EditorGUIUtility.PingObject(obj);
        }
        
        protected string GetAssetGuid(Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            return AssetDatabase.AssetPathToGUID(path);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Legacy UXML Support (for gradual migration)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Sets up a collapsible section from UXML elements.
        /// Use this when migrating from UXML to programmatic.
        /// </summary>
        protected void SetupCollapsibleSection(string sectionName, bool startExpanded = true)
        {
            var header = root.Q($"{sectionName}Header");
            var content = root.Q(sectionName);
            var arrow = root.Q<Label>($"{sectionName}Arrow");
            
            if (header == null || content == null) return;
            
            bool isExpanded = startExpanded;
            
            // Set initial state
            content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (arrow != null)
            {
                arrow.text = isExpanded ? Icons.ChevronDown : Icons.ChevronRight;
            }
            
            // Add hover effect
            header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                header.style.backgroundColor = Colors.SectionHeaderHover;
            });
            
            header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                header.style.backgroundColor = Colors.SectionHeaderBackground;
            });
            
            // Toggle on click
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                
                isExpanded = !isExpanded;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (arrow != null)
                {
                    arrow.text = isExpanded ? Icons.ChevronDown : Icons.ChevronRight;
                }
                
                evt.StopPropagation();
            });
        }
        
        /// <summary>
        /// Binds a PropertyField from UXML to a SerializedProperty.
        /// </summary>
        protected PropertyField BindPropertyField(VisualElement container, string fieldName, string propertyPath, string fieldLabel = "")
        {
            var field = container.Q<PropertyField>(fieldName);
            if (field != null && !string.IsNullOrEmpty(fieldLabel))
            {
                field.label = fieldLabel;
            }
            
            var prop = serializedObject.FindProperty(propertyPath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
            return field;
        }
        
        /// <summary>
        /// Binds a PropertyField from UXML to a relative SerializedProperty.
        /// </summary>
        protected PropertyField BindPropertyField(VisualElement container, string fieldName, SerializedProperty parent, string relativePath, string fieldLabel = "")
        {
            var field = container.Q<PropertyField>(fieldName);
            if (field != null && !string.IsNullOrEmpty(fieldLabel))
            {
                field.label = fieldLabel;
            }
            
            var prop = parent.FindPropertyRelative(relativePath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
            return field;
        }
        
        /// <summary>
        /// Sets up a help button from UXML.
        /// </summary>
        protected void SetupHelpButton(string buttonName, string url)
        {
            var btn = root.Q<Button>(buttonName);
            if (btn != null)
            {
                btn.clicked += () => Application.OpenURL(url);
            }
        }
        
        /// <summary>
        /// Configures an existing ScrollView from UXML.
        /// </summary>
        protected void ConfigureScrollView()
        {
            var scrollView = root.Q<ScrollView>("ContentScrollView");
            if (scrollView != null)
            {
                scrollView.style.maxHeight = 800;
                scrollView.style.minHeight = 200;
                scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }
        }
        
        /// <summary>
        /// Sets up header buttons from UXML.
        /// </summary>
        protected void SetupHeader()
        {
            var header = root.Q("Header");
            if (header == null) return;

            var refreshBtn = header.Q<Button>("Refresh");
            if (refreshBtn != null)
            {
                refreshBtn.clicked += Refresh;
                ApplyButtonHoverStyle(refreshBtn);
            }
            
            var lookupBtn = header.Q<Button>("Lookup");
            if (lookupBtn != null)
            {
                lookupBtn.clicked += Lookup;
                ApplyButtonHoverStyle(lookupBtn);
            }
            
            var visualizeBtn = header.Q<Button>("Visualize");
            if (visualizeBtn != null)
            {
                visualizeBtn.clicked += Visualize;
                ApplyButtonHoverStyle(visualizeBtn);
            }
            
            var importBtn = header.Q<Button>("Import");
            if (importBtn != null)
            {
                importBtn.clicked += Import;
                ApplyButtonHoverStyle(importBtn);
            }
        }
        
        /// <summary>
        /// Applies hover effect to a button.
        /// </summary>
        protected void ApplyButtonHoverStyle(Button btn)
        {
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
    }
}