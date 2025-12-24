using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public abstract class BasePlayForgeEditor : UnityEditor.Editor
    {
        protected VisualElement root;
        
        protected virtual void SetupHeader()
        {
            var header = root.Q("Header");

            var visualize = header.Q<Button>("Visualize");
            //visualize.style.backgroundImage = EditorGUIUtility.IconContent("d_forward@2x").image as Texture2D;
            
            var refresh = header.Q<Button>("Refresh");
            //refresh.style.backgroundImage = EditorGUIUtility.IconContent("d_Refresh@2x").image as Texture2D;
            
            var lookup = header.Q<Button>("Lookup");
            //lookup.style.backgroundImage = EditorGUIUtility.IconContent("d_Search Icon").image as Texture2D;
        }

        protected virtual void ConfigureScrollView()
        {
            var scrollView = root.Q<ScrollView>("ContentScrollView");
            if (scrollView != null)
            {
                scrollView.style.maxHeight = 1000;
                scrollView.style.minHeight = 200;
                scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }
        }

        protected abstract void SetupCollapsibleSections();
        
        protected void SetupCollapsibleSection(string sectionName)
        {
            var header = root.Q($"{sectionName}Header");
            var content = root.Q(sectionName);
            var arrow = root.Q<Label>($"{sectionName}Arrow");
            
            if (header == null || content == null) return;
            
            // Start expanded
            bool isExpanded = true;
            
            // Add hover effect
            header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                header.style.backgroundColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            });
            
            header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                header.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            });
            
            // Toggle on click
            header.RegisterCallback<ClickEvent>(evt =>
            {
                // Don't toggle if clicking the help button
                if (evt.target is Button) return;
                
                isExpanded = !isExpanded;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (arrow != null)
                {
                    arrow.text = isExpanded ? "▼" : "►";
                }
                
                evt.StopPropagation();
            });
        }

        protected virtual void UpdateAssetTagDisplay(Label assetTagLabel, string _name, string fallback)
        {
            if (assetTagLabel == null) return;
            
            string generatedTag = GenerateAssetTag(_name, fallback);
            assetTagLabel.text = generatedTag;
            
            // Also update the actual asset tag in the ability
            // You may want to create a Tag object here or store as string
            // For now, we'll just update the display
        }

        protected string GenerateAssetTag(string _name, string fallback)
        {
            if (string.IsNullOrEmpty(_name))
                return "Ability";
            
            // Remove special characters (keep only alphanumeric and spaces)
            string cleaned = Regex.Replace(_name, @"[^a-zA-Z0-9\s]", "");
            
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
            
            return string.IsNullOrEmpty(result) ? $"{fallback}_" : result;
        }
        
        protected void BindPropertyField(VisualElement container, string fieldName, string propertyPath)
        {
            var field = container.Q<PropertyField>(fieldName);
            var prop = serializedObject.FindProperty(propertyPath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
        }
        
        protected void BindPropertyField(VisualElement container, string fieldName, SerializedProperty parent, string relativePath)
        {
            var field = container.Q<PropertyField>(fieldName);
            var prop = parent.FindPropertyRelative(relativePath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
        }

        protected abstract void Lookup();

        protected virtual void Visualize()
        {
            
        }

        protected abstract void Refresh();
        
    }
}
