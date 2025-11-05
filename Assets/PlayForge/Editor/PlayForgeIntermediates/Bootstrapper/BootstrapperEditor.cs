using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(Bootstrapper))]
    public class BootstrapperEditor : UnityEditor.Editor
    {
        public VisualTreeAsset m_InspectorUXML;
        private VisualElement root;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }

        public override VisualElement CreateInspectorGUI()
        {
            // Create a new VisualElement to be the root of our Inspector UI.
            root = new VisualElement();
            
            // Load the UXML file and clone its tree into the inspector.
            if (m_InspectorUXML != null)
            {
                VisualElement uxmlContent = m_InspectorUXML.CloneTree();
                root.Add(uxmlContent);
            }

            var advancedAssignments = root.Q<VisualElement>("ProcessAndGameRoot");
            var toggle = root.Q<VisualElement>("ShowAdvancedContainer").Q<Toggle>("ShowAdvancedToggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                Debug.Log($"{evt.newValue}");
                advancedAssignments.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });
            
            var frameworkField = root.Q<ObjectField>("FrameworkField");
            frameworkField.RegisterValueChangedCallback(evt =>
            {
                UpdateFrameworkVisuals();
            });
            
            UpdateFrameworkVisuals();
     
            // Return the finished Inspector UI.
            return root;
        }

        void UpdateFrameworkVisuals()
        {
            if (root is null)
            {
                return;
            }
            
            var frameworkField = root.Q<ObjectField>("FrameworkField");
            var index = frameworkField.value as FrameworkIndex;
            
            var fpParent = root.Q<VisualElement>("FrameworkData");
            
            if (frameworkField.value is null)
            {
                fpParent.style.display = DisplayStyle.None;
                return;
            }
            
            if (index is null)
            {
                fpParent.style.display = DisplayStyle.None;
                return;
            }
            
            var attributes = fpParent.Q<VisualElement>("Attributes");
            var attributesCount = attributes.Q<IntegerField>("Count");
            attributesCount.value = index.Attributes.Count;
            
            var attributeSets = fpParent.Q<VisualElement>("AttributeSets");
            var attributeSetsCount = attributeSets.Q<IntegerField>("Count");
            attributeSetsCount.value = index.AttributeSets.Count;
            
            var abilities = fpParent.Q<VisualElement>("Abilities");
            var abilitiesCount = abilities.Q<IntegerField>("Count");
            abilitiesCount.value = index.Abilities.Count;
            
            var effects = fpParent.Q<VisualElement>("Effects");
            var effectsCount = effects.Q<IntegerField>("Count");
            effectsCount.value = index.Effects.Count;
            
            var entities = fpParent.Q<VisualElement>("Entities");
            var entitiesCount = entities.Q<IntegerField>("Count");
            entitiesCount.value = index.Entities.Count;
            
            var tags = fpParent.Q<VisualElement>("Tags");
            var tagsCount = tags.Q<IntegerField>("Count");
            tagsCount.value = index.Tags.Count;

            fpParent.style.display = DisplayStyle.Flex;
        }
    }
}
