using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(EntityIdentity))]
    public class EntityIdentityEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset m_InspectorUXML;
        
        private VisualElement root;
        private EntityIdentity entity;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            root = new VisualElement();
            entity = serializedObject.targetObject as EntityIdentity;

            if (entity == null) return null;

            if (m_InspectorUXML != null)
            {
                m_InspectorUXML.CloneTree(root);
            }
            
            BindHeader();
            BindIdentity();
            BindAbilities();
            BindAttributes();
            BindWorkers();
            BindLocalData();
            
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
                nameLabel.text = !string.IsNullOrEmpty(entity.GetName()) 
                    ? entity.GetName() 
                    : "Unnamed Entity";
            }
            
            if (descLabel != null)
            {
                descLabel.text = !string.IsNullOrEmpty(entity.GetDescription()) 
                    ? entity.GetDescription() 
                    : "No description provided.";
            }
            
            var refreshBtn = header.Q<Button>("Refresh");
            refreshBtn?.RegisterCallback<ClickEvent>(_ => RefreshInspector());
            
            var lookupBtn = header.Q<Button>("Lookup");
            lookupBtn?.RegisterCallback<ClickEvent>(_ => FindReferences());
        }

        private void BindIdentity()
        {
            var section = root.Q("Identity");
            if (section == null) return;
            
            BindPropertyField(section, "IdentityData", "Identity");
        }
        
        private void BindAbilities()
        {
            var section = root.Q("Abilities");
            if (section == null) return;
            
            var activationPolicy = section.Q<EnumField>("ActivationPolicy");
            if (activationPolicy != null)
            {
                activationPolicy.value = entity.ActivationPolicy;
                activationPolicy.RegisterValueChangedCallback(evt =>
                {
                    entity.ActivationPolicy = (EAbilityActivationPolicy)evt.newValue;
                    MarkDirty();
                });
            }
            
            var maxAbilities = section.Q<IntegerField>("MaxAbilities");
            if (maxAbilities != null)
            {
                maxAbilities.value = entity.MaxAbilities;
                maxAbilities.RegisterValueChangedCallback(evt =>
                {
                    entity.MaxAbilities = evt.newValue;
                    MarkDirty();
                });
            }
            
            var allowDuplicates = section.Q<Toggle>("AllowDuplicates");
            if (allowDuplicates != null)
            {
                allowDuplicates.value = entity.AllowDuplicateAbilities;
                allowDuplicates.RegisterValueChangedCallback(evt =>
                {
                    entity.AllowDuplicateAbilities = evt.newValue;
                    MarkDirty();
                });
            }
            
            BindPropertyField(section, "StartingAbilities", "StartingAbilities");
        }
        
        private void BindAttributes()
        {
            var section = root.Q("Attributes");
            if (section == null) return;
            
            BindPropertyField(section, "AttributeSet", "AttributeSet");
            BindPropertyField(section, "AttributeChangeEvents", "AttributeChangeEvents");
        }
        
        private void BindWorkers()
        {
            var section = root.Q("Workers");
            if (section == null) return;
            
            BindPropertyField(section, "ImpactWorkers", "ImpactWorkers");
            BindPropertyField(section, "TagWorkers", "TagWorkers");
            BindPropertyField(section, "AnalysisWorkers", "AnalysisWorkers");
        }
        
        private void BindLocalData()
        {
            var section = root.Q("LocalData");
            if (section == null) return;
            
            BindPropertyField(section, "LocalData", "LocalData");
        }

        private void BindPropertyField(VisualElement container, string fieldName, string propertyPath)
        {
            var field = container.Q<PropertyField>(fieldName);
            var prop = serializedObject.FindProperty(propertyPath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
        }
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(entity);
        }
        
        private void FindReferences()
        {
            var path = AssetDatabase.GetAssetPath(entity);
            EditorGUIUtility.PingObject(entity);
        }
        
        private void RefreshInspector()
        {
            serializedObject.Update();
            
            var nameLabel = root.Q("Header")?.Q<Label>("Name");
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            
            if (nameLabel != null)
                nameLabel.text = !string.IsNullOrEmpty(entity.GetName()) ? entity.GetName() : "Unnamed Entity";
            if (descLabel != null)
                descLabel.text = !string.IsNullOrEmpty(entity.GetDescription()) ? entity.GetDescription() : "No description provided.";
        }
    }
}