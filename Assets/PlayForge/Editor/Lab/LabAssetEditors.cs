using UnityEditor;
using UnityEngine.UIElements;
using FarEmerald.PlayForge.Extended.Editor;

namespace FarEmerald.PlayForge.Lab.Editor
{
    /// <summary>The Creator Lab's rules' inspector: the shared header, then their fields (FN-12).</summary>
    [CustomEditor(typeof(LabRules), true)]
    public class LabRulesEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }

    /// <summary>A Lab Driver's inspector, a project's own among them: the shared header, then its fields (FN-12).</summary>
    [CustomEditor(typeof(LabDriver), true)]
    public class LabDriverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }
}
