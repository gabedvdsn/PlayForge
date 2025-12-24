using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(AttributeSet))]
    public class AttributeSetEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset m_InspectorUXML;
        
        private VisualElement root;
        private AttributeSet attributeSet;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            root = new VisualElement();
            attributeSet = serializedObject.targetObject as AttributeSet;

            if (attributeSet == null) return null;

            if (m_InspectorUXML != null)
            {
                m_InspectorUXML.CloneTree(root);
            }
            
            BindHeader();
            BindAttributes();
            BindSubsets();
            BindSettings();
            BindSummary();
            
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
                nameLabel.text = attributeSet.name;
            }
            
            UpdateHeaderDescription(descLabel);
            
            var refreshBtn = header.Q<Button>("Refresh");
            refreshBtn?.RegisterCallback<ClickEvent>(_ => RefreshInspector());
            
            var lookupBtn = header.Q<Button>("Lookup");
            lookupBtn?.RegisterCallback<ClickEvent>(_ => FindReferences());
        }

        private void UpdateHeaderDescription(Label descLabel)
        {
            if (descLabel == null) return;
            
            int attrCount = attributeSet.Attributes?.Count ?? 0;
            int subsetCount = attributeSet.SubSets?.Count ?? 0;
            var uniqueCount = attributeSet.GetUnique().Count;
            
            descLabel.text = $"Contains {attrCount} attributes, {subsetCount} subsets ({uniqueCount} unique total)";
        }

        private void BindAttributes()
        {
            var section = root.Q("Attributes");
            if (section == null) return;
            
            BindPropertyField(section, "Attributes", "Attributes");
            
            // Update count label
            var countLabel = root.Q<Label>("AttributeCount");
            if (countLabel != null)
            {
                countLabel.text = (attributeSet.Attributes?.Count ?? 0).ToString();
            }
        }
        
        private void BindSubsets()
        {
            var section = root.Q("Subsets");
            if (section == null) return;
            
            BindPropertyField(section, "SubSets", "SubSets");
            
            // Update count label
            var countLabel = root.Q<Label>("SubsetCount");
            if (countLabel != null)
            {
                countLabel.text = (attributeSet.SubSets?.Count ?? 0).ToString();
            }
        }
        
        private void BindSettings()
        {
            var section = root.Q("Settings");
            if (section == null) return;
            
            var collisionPolicy = section.Q<EnumField>("CollisionPolicy");
            if (collisionPolicy != null)
            {
                collisionPolicy.value = attributeSet.CollisionResolutionPolicy;
                collisionPolicy.RegisterValueChangedCallback(evt =>
                {
                    attributeSet.CollisionResolutionPolicy = (EValueCollisionPolicy)evt.newValue;
                    MarkDirty();
                });
            }
        }
        
        private void BindSummary()
        {
            var section = root.Q("Summary");
            if (section == null) return;
            
            UpdateSummary();
            
            var listBtn = section.Q<Button>("ListAttributes");
            if (listBtn != null)
            {
                listBtn.clicked += ListAllUniqueAttributes;
            }
        }
        
        private void UpdateSummary()
        {
            var uniqueLabel = root.Q<Label>("UniqueAttributesLabel");
            if (uniqueLabel != null)
            {
                var unique = attributeSet.GetUnique();
                uniqueLabel.text = $"Total unique attributes: {unique.Count}";
            }
        }
        
        private void ListAllUniqueAttributes()
        {
            var unique = attributeSet.GetUnique();
            
            if (unique.Count == 0)
            {
                EditorUtility.DisplayDialog("Unique Attributes", "No attributes defined in this set.", "OK");
                return;
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"Unique Attributes ({unique.Count}):");
            sb.AppendLine();
            
            foreach (var attr in unique.OrderBy(a => a?.Name ?? ""))
            {
                if (attr != null)
                {
                    sb.AppendLine($"• {attr.Name}");
                }
            }
            
            EditorUtility.DisplayDialog("Unique Attributes", sb.ToString(), "OK");
        }

        private void BindPropertyField(VisualElement container, string fieldName, string propertyPath)
        {
            var field = container.Q<PropertyField>(fieldName);
            var prop = serializedObject.FindProperty(propertyPath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
                
                // Track changes to update counts
                field.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshCounts());
            }
        }
        
        private void RefreshCounts()
        {
            var attrCountLabel = root.Q<Label>("AttributeCount");
            if (attrCountLabel != null)
            {
                attrCountLabel.text = (attributeSet.Attributes?.Count ?? 0).ToString();
            }
            
            var subsetCountLabel = root.Q<Label>("SubsetCount");
            if (subsetCountLabel != null)
            {
                subsetCountLabel.text = (attributeSet.SubSets?.Count ?? 0).ToString();
            }
            
            UpdateSummary();
            
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            UpdateHeaderDescription(descLabel);
        }
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(attributeSet);
        }
        
        private void FindReferences()
        {
            EditorGUIUtility.PingObject(attributeSet);
        }
        
        private void RefreshInspector()
        {
            serializedObject.Update();
            RefreshCounts();
        }
    }
}