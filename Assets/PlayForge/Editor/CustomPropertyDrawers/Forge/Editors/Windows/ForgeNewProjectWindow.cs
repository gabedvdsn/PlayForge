using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// New Project, from The Forge's project menu (FEAT-34): a name, then Create, which makes a Forge Project in a folder of
    /// its own at the project's root and makes it the active one, so The Forge shows its assets, none yet.
    /// </summary>
    internal sealed class ForgeNewProjectWindow : EditorWindow
    {
        private TextField projectName;

        /// <summary>Opens New Project.</summary>
        public static ForgeNewProjectWindow Open()
        {
            var window = CreateInstance<ForgeNewProjectWindow>();
            window.titleContent = new GUIContent("New Forge Project");
            window.minSize = window.maxSize = new Vector2(380f, 74f);
            window.ShowUtility();
            return window;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            projectName = new TextField("Name") { name = "ProjectName", value = "New Project", tooltip = "The project's name, and its folder's" };
            projectName.AddToClassList(TextField.alignedFieldUssClassName);
            projectName.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is KeyCode.Return or KeyCode.KeypadEnter) Create();
                else if (key.keyCode == KeyCode.Escape) Close();
            }, TrickleDown.TrickleDown);
            root.Add(projectName);

            var footer = new VisualElement();
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            footer.Add(new Button(Create) { name = "Create", text = "Create" });
            root.Add(footer);
            projectName.schedule.Execute(() => projectName.Focus());
        }

        // Makes the project, the active one, and closes
        internal void Create()
        {
            ForgeProjects.Make(projectName?.value);
            Close();
        }
    }
}
