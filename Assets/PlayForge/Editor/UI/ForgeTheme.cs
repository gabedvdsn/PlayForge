using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// PlayForge's one style sheet, and the editor's theme on each PlayForge window and inspector (Decision 20). A
    /// root takes the sheet and a theme class, .pf-theme-dark or .pf-theme-light, which set the sheet's variables, so
    /// every control under it reads the theme's colors. The windows and inspectors drew in hard-coded dark colors,
    /// whatever the theme (ED-20).
    /// </summary>
    internal static class ForgeTheme
    {
        // PlayForge.uss, found by its GUID so it loads wherever the package lives: in Packages, or in Assets
        private const string SheetGuid = "990c59ba70c94807baac80f23261f077";

        public const string RootClass = "pf-root";
        public const string DarkClass = "pf-theme-dark";
        public const string LightClass = "pf-theme-light";

        private static StyleSheet sheet;

        public static StyleSheet Sheet
        {
            get
            {
                if (sheet == null) sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(SheetGuid));
                return sheet;
            }
        }

        /// <summary>Gives a root the style sheet and the editor's current theme.</summary>
        public static void Apply(VisualElement root) => Apply(root, EditorGUIUtility.isProSkin);

        public static void Apply(VisualElement root, bool dark)
        {
            if (root == null) return;
            if (Sheet != null && !root.styleSheets.Contains(Sheet)) root.styleSheets.Add(Sheet);
            root.AddToClassList(RootClass);
            root.EnableInClassList(DarkClass, dark);
            root.EnableInClassList(LightClass, !dark);
        }
    }
}
