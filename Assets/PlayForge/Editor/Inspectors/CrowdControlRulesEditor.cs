using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>A Crowd Control Rules asset's inspector: the shared header, then its rules.</summary>
    [CustomEditor(typeof(CrowdControlRules))]
    public class CrowdControlRulesEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }
}
