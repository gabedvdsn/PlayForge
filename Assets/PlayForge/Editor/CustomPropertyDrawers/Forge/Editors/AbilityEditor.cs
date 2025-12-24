using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(Ability))]
    public class AbilityEditor : BasePlayForgeEditor
    {
        [SerializeField] private VisualTreeAsset m_InspectorUXML;
        [SerializeField] private StyleSheet m_StyleSheet;
        [SerializeField] private float m_ScrollViewHeight = 600f;
        
        private Ability ability;
        private Label assetTagValueLabel;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            root = new VisualElement();
            ability = serializedObject.targetObject as Ability;

            if (ability == null) return null;

            // Clone UXML template
            if (m_InspectorUXML != null)
            {
                m_InspectorUXML.CloneTree(root);
            }
            
            // Apply stylesheet
            if (m_StyleSheet != null)
            {
                root.styleSheets.Add(m_StyleSheet);
            }
            
            // Configure ScrollView
            ConfigureScrollView();
            
            // Setup collapsible sections
            SetupCollapsibleSections();
            
            // Bind all sections
            BindHeader();
            BindDefinition();
            BindTags();
            BindRuntime();
            BindLeveling();
            BindValidation();
            BindLocalData();
            BindWorkers();
            
            // Setup help buttons
            SetupHelpButtons();
            
            SetupHeader();
            
            // Final binding pass
            root.Bind(serializedObject);
            
            return root;
        }

        /*private void ConfigureScrollView()
        {
            var scrollView = root.Q<ScrollView>("ContentScrollView");
            if (scrollView != null)
            {
                scrollView.style.maxHeight = m_ScrollViewHeight;
                scrollView.style.minHeight = 200;
                scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }
        }*/

        // ═══════════════════════════════════════════════════════════════════════
        // Collapsible Sections
        // ═══════════════════════════════════════════════════════════════════════
        
        protected override void SetupCollapsibleSections()
        {
            // Setup each section as collapsible
            SetupCollapsibleSection("Definition");
            SetupCollapsibleSection("Tags");
            SetupCollapsibleSection("Runtime");
            SetupCollapsibleSection("Leveling");
            SetupCollapsibleSection("Validation");
            SetupCollapsibleSection("Workers");
            SetupCollapsibleSection("LocalData");
        }

        /*private void SetupCollapsibleSection(string sectionName)
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
        }*/

        // ═══════════════════════════════════════════════════════════════════════
        // Asset Tag Generation
        // ═══════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Generates a clean tag string from the ability name.
        /// Removes spaces and special characters, converts to PascalCase.
        /// </summary>
        private string GenerateAssetTag(string abilityName)
        {
            if (string.IsNullOrEmpty(abilityName))
                return "Ability";
            
            // Remove special characters (keep only alphanumeric and spaces)
            string cleaned = Regex.Replace(abilityName, @"[^a-zA-Z0-9\s]", "");
            
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
                result = "Ability" + result;
            }
            
            return string.IsNullOrEmpty(result) ? "Ability" : result;
        }

        private void UpdateAssetTagDisplay()
        {
            if (assetTagValueLabel == null) return;
            
            string generatedTag = GenerateAssetTag(ability.Definition.Name);
            assetTagValueLabel.text = generatedTag;
            
            // Also update the actual asset tag in the ability
            // You may want to create a Tag object here or store as string
            // For now, we'll just update the display
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Binding Methods
        // ═══════════════════════════════════════════════════════════════════════

        private void BindHeader()
        {
            var header = root.Q("Header");
            if (header == null) return;
            
            var nameLabel = header.Q<Label>("Name");
            var descLabel = header.Q<Label>("Description");
            
            if (nameLabel != null)
            {
                nameLabel.text = !string.IsNullOrEmpty(ability.Definition.Name) 
                    ? ability.Definition.Name 
                    : "Unnamed Ability";
            }
            
            if (descLabel != null)
            {
                descLabel.text = !string.IsNullOrEmpty(ability.Definition.Description) 
                    ? ability.Definition.Description 
                    : "No description provided.";
            }
            
            var lookupBtn = header.Q<Button>("Lookup");
            lookupBtn?.RegisterCallback<ClickEvent>(_ => Lookup());
            
            var refreshBtn = header.Q<Button>("Refresh");
            refreshBtn?.RegisterCallback<ClickEvent>(_ => Refresh());
        }

        private void BindDefinition()
        {
            var section = root.Q("Definition");
            if (section == null) return;
            
            var nameLabel = root.Q("Header")?.Q<Label>("Name");
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            
            // Name field with live header and asset tag update
            var nameField = section.Q<TextField>("Name");
            if (nameField != null)
            {
                nameField.value = ability.Definition.Name;
                nameField.RegisterValueChangedCallback(evt =>
                {
                    // Update asset tag display as user types
                    UpdateAssetTagDisplay();
                });
                nameField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    ability.Definition.Name = nameField.value;
                    if (nameLabel != null) 
                        nameLabel.text = !string.IsNullOrEmpty(nameField.value) ? nameField.value : "Unnamed Ability";
                    UpdateAssetTagDisplay();
                    MarkDirty();
                });
            }

            // Description field with live header update
            var descField = section.Q<TextField>("Description");
            if (descField != null)
            {
                descField.value = ability.Definition.Description;
                descField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    ability.Definition.Description = descField.value;
                    if (descLabel != null) 
                        descLabel.text = !string.IsNullOrEmpty(descField.value) ? descField.value : "No description provided.";
                    MarkDirty();
                });
            }
            
            // Activation Policy
            var activationPolicy = section.Q<EnumField>("ActivationPolicy");
            if (activationPolicy != null)
            {
                activationPolicy.value = ability.Definition.ActivationPolicy;
                activationPolicy.RegisterValueChangedCallback(evt =>
                {
                    ability.Definition.ActivationPolicy = (EAbilityActivationPolicyExtended)evt.newValue;
                    MarkDirty();
                });
            }
            
            // Activate Immediately
            var activateImmediately = section.Q<Toggle>("ActivateImmediately");
            if (activateImmediately != null)
            {
                activateImmediately.value = ability.Definition.ActivateImmediately;
                activateImmediately.RegisterValueChangedCallback(evt =>
                {
                    ability.Definition.ActivateImmediately = evt.newValue;
                    MarkDirty();
                });
            }
            
            // Icon fields
            BindPropertyField(section, "UnlearnedIcon", "Definition.UnlearnedIcon");
            BindPropertyField(section, "NormalIcon", "Definition.NormalIcon");
            BindPropertyField(section, "QueuedIcon", "Definition.QueuedIcon");
            BindPropertyField(section, "OnCooldownIcon", "Definition.OnCooldownIcon");
        }
        
        private void BindTags()
        {
            var section = root.Q("Tags");
            if (section == null) return;
            
            var tags = serializedObject.FindProperty("Tags");
            if (tags == null) return;
            
            // Asset Tag (read-only, auto-generated)
            assetTagValueLabel = section.Q<Label>("AssetTagValue");
            UpdateAssetTagDisplay();
            
            // Other tag fields
            BindPropertyField(section, "ContextTags", tags, "ContextTags");
            BindPropertyField(section, "PassiveTags", tags, "PassiveGrantedTags");
            BindPropertyField(section, "ActiveTags", tags, "ActiveGrantedTags");
            
            // Source Requirements
            var sourceReqs = tags.FindPropertyRelative("SourceRequirements");
            if (sourceReqs != null)
            {
                var container = section.Q("SourceRequirements");
                if (container != null)
                {
                    BindPropertyField(container, "Require", sourceReqs, "RequireTags");
                    BindPropertyField(container, "Avoid", sourceReqs, "AvoidTags");
                }
            }
            
            // Target Requirements
            var targetReqs = tags.FindPropertyRelative("TargetRequirements");
            if (targetReqs != null)
            {
                var container = section.Q("TargetRequirements");
                if (container != null)
                {
                    BindPropertyField(container, "Require", targetReqs, "RequireTags");
                    BindPropertyField(container, "Avoid", targetReqs, "AvoidTags");
                }
            }
        }
        
        private void BindRuntime()
        {
            var section = root.Q("Runtime");
            if (section == null) return;
            
            var proxy = serializedObject.FindProperty("Proxy");
            if (proxy == null) return;
            
            BindPropertyField(section, "TargetingTask", proxy, "Targeting");
            
            var useImplicit = section.Q<Toggle>("UseImplicitTargeting");
            if (useImplicit != null)
            {
                useImplicit.BindProperty(proxy.FindPropertyRelative("UseImplicitTargeting"));
            }
            
            BindPropertyField(section, "Stages", proxy, "Stages");
        }
        
        private void BindLeveling()
        {
            var section = root.Q("Leveling");
            if (section == null) return;
            
            BindPropertyField(section, "StartingLevel", "StartingLevel");
            BindPropertyField(section, "MaxLevel", "MaxLevel");
            
            var ignoreZero = section.Q<Toggle>("IgnoreWhenLevelZero");
            if (ignoreZero != null)
            {
                ignoreZero.BindProperty(serializedObject.FindProperty("IgnoreWhenLevelZero"));
            }
            
            BindPropertyField(section, "Cost", "Cost");
            BindPropertyField(section, "Cooldown", "Cooldown");
        }
        
        private void BindValidation()
        {
            var section = root.Q("Validation");
            if (section == null) return;
            
            BindPropertyField(section, "SourceActivationRules", "SourceActivationRules");
            BindPropertyField(section, "TargetActivationRules", "TargetActivationRules");
        }
        
        private void BindLocalData()
        {
            var section = root.Q("LocalData");
            if (section == null) return;
            
            BindPropertyField(section, "LocalData", "LocalData");
        }

        private void BindWorkers()
        {
            var section = root.Q("Workers");
            if (section == null) return;
            
            BindPropertyField(section, "Attribute", "AttributeWorkers");
            BindPropertyField(section, "Tag", "TagWorkers");
            BindPropertyField(section, "Impact", "ImpactWorkers");
            BindPropertyField(section, "Analysis", "AnalysisWorkers");
        }
        
        private void SetupHelpButtons()
        {
            SetupHelpButton("DefinitionHelp", "https://docs.playforge.dev/abilities/definition");
            SetupHelpButton("TagsHelp", "https://docs.playforge.dev/abilities/tags");
            SetupHelpButton("RuntimeHelp", "https://docs.playforge.dev/abilities/runtime");
            SetupHelpButton("LevelingHelp", "https://docs.playforge.dev/abilities/leveling");
            SetupHelpButton("ValidationHelp", "https://docs.playforge.dev/abilities/validation");
            SetupHelpButton("LocalDataHelp", "https://docs.playforge.dev/abilities/localdata");
            SetupHelpButton("WorkersHelp", "https://docs.playforge.dev/abilities/workers");
        }
        
        private void SetupHelpButton(string buttonName, string url)
        {
            var btn = root.Q<Button>(buttonName);
            if (btn != null)
            {
                btn.clicked += () => Application.OpenURL(url);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════
        
        private void BindPropertyField(VisualElement container, string fieldName, string propertyPath)
        {
            var field = container.Q<PropertyField>(fieldName);
            var prop = serializedObject.FindProperty(propertyPath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
        }
        
        private PropertyField BindPropertyField(VisualElement container, string fieldName, SerializedProperty parent, string relativePath)
        {
            var field = container.Q<PropertyField>(fieldName);
            var prop = parent.FindPropertyRelative(relativePath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
            return field;
        }
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(ability);
        }

        protected override void Lookup()
        {
            var path = AssetDatabase.GetAssetPath(ability);
            var guid = AssetDatabase.AssetPathToGUID(path);
            EditorGUIUtility.PingObject(ability);
        }

        protected override void Refresh()
        {
            serializedObject.Update();
            
            var nameLabel = root.Q("Header")?.Q<Label>("Name");
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            
            if (nameLabel != null)
                nameLabel.text = !string.IsNullOrEmpty(ability.Definition.Name) ? ability.Definition.Name : "Unnamed Ability";
            if (descLabel != null)
                descLabel.text = !string.IsNullOrEmpty(ability.Definition.Description) ? ability.Definition.Description : "No description provided.";
            
            UpdateAssetTagDisplay();
        }
    }
}