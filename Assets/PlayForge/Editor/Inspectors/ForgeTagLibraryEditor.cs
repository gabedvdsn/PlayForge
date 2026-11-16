using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>A Tag Library's inspector: the shared header, then its entries and its contexts (FN-12).</summary>
    [CustomEditor(typeof(ForgeTagLibrary))]
    public class ForgeTagLibraryEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }
}
