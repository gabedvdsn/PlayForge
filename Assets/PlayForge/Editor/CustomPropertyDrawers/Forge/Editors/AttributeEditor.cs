using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(Attribute))]
    public class AttributeEditor : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset m_InspectorUXML;
        
        private VisualElement root;
        private Attribute attribute;

        public override VisualElement CreateInspectorGUI()
        {
            if (serializedObject.isEditingMultipleObjects) return null;
            
            root = new VisualElement();
            attribute = serializedObject.targetObject as Attribute;

            if (attribute == null) return null;

            if (m_InspectorUXML != null)
            {
                m_InspectorUXML.CloneTree(root);
            }
            
            BindHeader();
            BindDefinition();
            SetupUsageSection();
            
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
                nameLabel.text = !string.IsNullOrEmpty(attribute.Name) 
                    ? attribute.Name 
                    : "Unnamed Attribute";
            }
            
            if (descLabel != null)
            {
                descLabel.text = !string.IsNullOrEmpty(attribute.Description) 
                    ? attribute.Description 
                    : "No description provided.";
            }
            
            var refreshBtn = header.Q<Button>("Refresh");
            refreshBtn?.RegisterCallback<ClickEvent>(_ => RefreshInspector());
            
            var lookupBtn = header.Q<Button>("Lookup");
            lookupBtn?.RegisterCallback<ClickEvent>(_ => FindReferences());
        }

        private void BindDefinition()
        {
            var section = root.Q("Definition");
            if (section == null) return;
            
            var nameLabel = root.Q("Header")?.Q<Label>("Name");
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            
            // Name field with live header update
            var nameField = section.Q<TextField>("Name");
            if (nameField != null)
            {
                nameField.value = attribute.Name;
                nameField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    attribute.Name = nameField.value;
                    if (nameLabel != null) 
                        nameLabel.text = !string.IsNullOrEmpty(nameField.value) ? nameField.value : "Unnamed Attribute";
                    MarkDirty();
                });
            }

            // Description field with live header update
            var descField = section.Q<TextField>("Description");
            if (descField != null)
            {
                descField.value = attribute.Description;
                descField.RegisterCallback<FocusOutEvent>(_ =>
                {
                    attribute.Description = descField.value;
                    if (descLabel != null) 
                        descLabel.text = !string.IsNullOrEmpty(descField.value) ? descField.value : "No description provided.";
                    MarkDirty();
                });
            }
        }
        
        private void SetupUsageSection()
        {
            var findReferencesBtn = root.Q<Button>("FindReferences");
            if (findReferencesBtn != null)
            {
                findReferencesBtn.clicked += FindReferences;
            }
        }
        
        private void MarkDirty()
        {
            EditorUtility.SetDirty(attribute);
        }
        
        private void FindReferences()
        {
            // Find all assets that reference this attribute
            var path = AssetDatabase.GetAssetPath(attribute);
            var guid = AssetDatabase.AssetPathToGUID(path);
            
            // This will open the search with the GUID
            // Users can search: ref:GUID in Unity's search
            EditorGUIUtility.PingObject(attribute);
            
            // Show a helpful dialog
            EditorUtility.DisplayDialog("Find References", 
                $"To find all references to this attribute:\n\n" +
                $"1. Open Edit > Project Settings > Search\n" +
                $"2. Use the Search window (Ctrl+K)\n" +
                $"3. Search for: ref:{guid}\n\n" +
                $"Or use 'Find References In Scene' from the context menu.", 
                "OK");
        }
        
        private void RefreshInspector()
        {
            serializedObject.Update();
            
            var nameLabel = root.Q("Header")?.Q<Label>("Name");
            var descLabel = root.Q("Header")?.Q<Label>("Description");
            
            if (nameLabel != null)
                nameLabel.text = !string.IsNullOrEmpty(attribute.Name) ? attribute.Name : "Unnamed Attribute";
            if (descLabel != null)
                descLabel.text = !string.IsNullOrEmpty(attribute.Description) ? attribute.Description : "No description provided.";
        }
    }
}