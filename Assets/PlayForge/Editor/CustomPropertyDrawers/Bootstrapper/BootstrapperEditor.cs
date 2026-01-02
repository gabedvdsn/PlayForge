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
        
        public override VisualElement CreateInspectorGUI()
        {
            return base.CreateInspectorGUI();
        }

        void UpdateFrameworkVisuals()
        {
            
        }
    }
}
