using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(GameplayEffect))]
    public class GameplayEffectEditor : BasePlayForgeEditor
    {
        [SerializeField] private VisualTreeAsset m_InspectorUXML;
        
        private GameplayEffect effect;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            root = new VisualElement();
            effect = serializedObject.targetObject as GameplayEffect;

            if (effect == null) return null;

            if (m_InspectorUXML != null)
            {
                m_InspectorUXML.CloneTree(root);
            }
            
            BindHeader();
            BindDefinition();
            BindTags();
            BindImpact();
            BindWorkers();
            BindRequirements();
            
            root.Bind(serializedObject);
            
            return root;
        }

        private void BindHeader()
        {
            var header = root.Q("Header");
            if (header == null) return;
            
            var nameLabel = header.Q<Label>("Name");
            var descLabel = header.Q<Label>("Description");
            
            if (nameLabel != null)
            {
                nameLabel.text = !string.IsNullOrEmpty(effect.GetName()) 
                    ? effect.GetName() 
                    : "Unnamed Effect";
            }
            
            if (descLabel != null)
            {
                descLabel.text = !string.IsNullOrEmpty(effect.GetDescription()) 
                    ? effect.GetDescription() 
                    : "No description provided.";
            }
            
            var refreshBtn = header.Q<Button>("Refresh");
            refreshBtn?.RegisterCallback<ClickEvent>(_ => Refresh());
            
            var lookupBtn = header.Q<Button>("Lookup");
            lookupBtn?.RegisterCallback<ClickEvent>(_ => Lookup());
        }

        private void BindDefinition()
        {
            var section = root.Q("Definition");
            if (section == null) return;
            
            var definition = serializedObject.FindProperty("Definition");
            if (definition == null) return;
            
            var nameLabel = root.Q("Header")?.Q<Label>("Name");
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            
            // Name field with live header update
            var nameField = section.Q<TextField>("Name");
            if (nameField != null)
            {
                nameField.value = effect.Definition.Name;
                nameField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    effect.Definition.Name = nameField.value;
                    if (nameLabel != null) 
                        nameLabel.text = !string.IsNullOrEmpty(nameField.value) ? nameField.value : "Unnamed Effect";
                    MarkDirty();
                });
            }

            // Description field with live header update
            var descField = section.Q<TextField>("Description");
            if (descField != null)
            {
                descField.value = effect.Definition.Description;
                descField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    effect.Definition.Description = descField.value;
                    if (descLabel != null) 
                        descLabel.text = !string.IsNullOrEmpty(descField.value) ? descField.value : "No description provided.";
                    MarkDirty();
                });
            }
            
            BindPropertyField(section, "Visibility", definition, "Visibility");
            BindPropertyField(section, "Icon", definition, "Icon");
        }
        
        private void BindTags()
        {
            var section = root.Q("Tags");
            if (section == null) return;
            
            var tags = serializedObject.FindProperty("Tags");
            if (tags == null) return;
            
            BindPropertyField(section, "AssetTag", tags, "AssetTag");
            BindPropertyField(section, "ContextTags", tags, "ContextTags");
            BindPropertyField(section, "GrantedTags", tags, "GrantedTags");
        }
        
        private void BindImpact()
        {
            var section = root.Q("Impact");
            if (section == null) return;
            
            BindPropertyField(section, "ImpactSpecification", "ImpactSpecification");
            BindPropertyField(section, "DurationSpecification", "DurationSpecification");
        }
        
        private void BindWorkers()
        {
            var section = root.Q("Workers");
            if (section == null) return;
            
            BindPropertyField(section, "Workers", "Workers");
        }
        
        private void BindRequirements()
        {
            var section = root.Q("Requirements");
            if (section == null) return;
            
            BindPropertyField(section, "SourceRequirements", "SourceRequirements");
            BindPropertyField(section, "TargetRequirements", "TargetRequirements");
        }
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(effect);
        }

        protected override void SetupCollapsibleSections()
        {
            
        }
        protected override void Lookup()
        {
            EditorGUIUtility.PingObject(effect);
        }
        protected override void Refresh()
        {
            serializedObject.Update();
            
            var nameLabel = root.Q("Header")?.Q<Label>("Name");
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            
            if (nameLabel != null)
                nameLabel.text = !string.IsNullOrEmpty(effect.GetName()) ? effect.GetName() : "Unnamed Effect";
            if (descLabel != null)
                descLabel.text = !string.IsNullOrEmpty(effect.GetDescription()) ? effect.GetDescription() : "No description provided.";
        }
    }
}