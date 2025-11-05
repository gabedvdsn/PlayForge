using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(FrameworkIndex))]
    public class IndexEditor : UnityEditor.Editor
    {
        public VisualTreeAsset m_InspectorUXML;
        private VisualElement root;

        private FrameworkIndex index;

        private void OnEnable()
        {
            index = (FrameworkIndex)target;
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

            var nameLabel = root.Q<VisualElement>("NameHolder").Q<Label>("NameLabel");
            nameLabel.text = ObjectNames.NicifyVariableName(index.FrameworkKey);

            var editBtn = root.Q<VisualElement>("EditHold").Q<Button>("EditButton");
            editBtn.clicked += () =>
            {
                PlayForgeEditor.OpenTo(index.FrameworkKey);
            };

            var openBtn = root.Q<VisualElement>("SIEHold").Q<Button>("SIEButton");
            openBtn.clicked += () =>
            {
                var path = ForgePaths.FrameworkFolder(index.FrameworkKey);
                if (Directory.Exists(path)) EditorUtility.RevealInFinder(path);
            };
     
            // Return the finished Inspector UI.
            return root;
        }
    }
}
