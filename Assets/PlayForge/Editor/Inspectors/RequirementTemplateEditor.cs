using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>A Requirement Template's inspector: the shared header, then its fields (FN-12).</summary>
    [CustomEditor(typeof(RequirementTemplate))]
    public class RequirementTemplateEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }
}
