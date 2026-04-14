using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Effect Templates settings builder.
    /// Add this to BuildAssetsSettingsTab() in PlayForgeManager_SettingsTab.cs:
    ///     BuildEffectTemplatesSection(scrollView);
    /// Place it before the Template Tags section.
    /// </summary>
    public partial class PlayForgeManager
    {
        private void BuildEffectTemplatesSection(VisualElement parent)
        {
            var section = CreateSettingsSection("Usage Effect Templates", 
                "Configure default templates for Cost and Cooldown effects", Colors.AccentOrange);
            parent.Add(section);
            
            var hint = new Label("These templates are used when importing effects into Ability Cost/Cooldown fields.");
            hint.style.fontSize = 10;
            hint.style.color = Colors.HintText;
            hint.style.marginTop = 4;
            hint.style.marginBottom = 12;
            hint.style.whiteSpace = WhiteSpace.Normal;
            section.Add(hint);
            
            // Build template rows for each known key
            foreach (var key in EffectTemplateRegistry.KnownKeys)
            {
                var row = CreateEffectTemplateRow(key);
                section.Add(row);
            }
            
            // Reset button
            var resetBtn = CreateButton("Clear Usage Templates", () =>
            {
                if (EditorUtility.DisplayDialog("Clear Usage Templates", 
                    "Clear all effect templates? This won't delete the actual effect assets.", "Clear", "Cancel"))
                {
                    foreach (var key in EffectTemplateRegistry.KnownKeys)
                    {
                        EffectTemplateRegistry.ClearTemplate(key);
                    }
                    ShowSettingsSubTab(currentSettingsTab);
                }
            });
            resetBtn.style.alignSelf = Align.FlexStart;
            resetBtn.style.marginTop = 12;
            section.Add(resetBtn);
        }
        
        private VisualElement CreateEffectTemplateRow(string templateKey)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            
            // Label
            var label = new Label(EffectTemplateRegistry.GetDisplayName(templateKey));
            label.style.width = 100;
            label.style.fontSize = 11;
            label.style.color = Colors.LabelText;
            label.tooltip = EffectTemplateRegistry.GetDescription(templateKey);
            row.Add(label);
            
            // Current template display
            var currentTemplate = EffectTemplateRegistry.GetTemplate(templateKey);
            
            var templateField = new ObjectField();
            templateField.objectType = typeof(GameplayEffect);
            templateField.value = currentTemplate;
            templateField.style.flexGrow = 1;
            templateField.label = "";
            templateField.RegisterValueChangedCallback(evt =>
            {
                var effect = evt.newValue as GameplayEffect;
                EffectTemplateRegistry.SetTemplate(templateKey, effect);
            });
            row.Add(templateField);
            
            // Status indicator
            var statusIcon = new Label(currentTemplate != null ? "✓" : "—");
            statusIcon.style.width = 20;
            statusIcon.style.marginLeft = 8;
            statusIcon.style.fontSize = 12;
            statusIcon.style.unityTextAlign = TextAnchor.MiddleCenter;
            statusIcon.style.color = currentTemplate != null ? Colors.AccentGreen : Colors.HintText;
            statusIcon.tooltip = currentTemplate != null ? "Template configured" : "No template set";
            row.Add(statusIcon);
            
            // Clear button
            var clearBtn = CreateButton("✕", () =>
            {
                EffectTemplateRegistry.ClearTemplate(templateKey);
                templateField.value = null;
                statusIcon.text = "—";
                statusIcon.style.color = Colors.HintText;
                statusIcon.tooltip = "No template set";
            });
            clearBtn.style.width = 22;
            clearBtn.style.height = 20;
            clearBtn.style.marginLeft = 4;
            clearBtn.style.fontSize = 10;
            clearBtn.tooltip = "Clear template";
            row.Add(clearBtn);
            
            return row;
        }
    }
}
