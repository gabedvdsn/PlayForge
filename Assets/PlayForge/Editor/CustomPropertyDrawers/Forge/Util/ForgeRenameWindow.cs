using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Rename, from an asset's menu in The Forge's list or its header, or F2 on The Forge's list (FN-8): the asset's name,
    /// to type over, then Rename, which renames it as its inspector does (<see cref="ForgeRenames"/>). Enter renames and
    /// Escape cancels.
    /// </summary>
    public sealed class ForgeRenameWindow : EditorWindow
    {
        private Object asset;
        private string newName;

        /// <summary>Opens the dialog for an asset that can be renamed; none for one that can't.</summary>
        public static ForgeRenameWindow Show(Object asset)
        {
            if (!ForgeRenames.CanRename(asset)) return null;
            var window = CreateInstance<ForgeRenameWindow>();
            window.asset = asset;
            window.newName = ForgeRenames.NameOf(asset);
            window.titleContent = new GUIContent($"Rename {ForgeAssetTypes.NameOf(asset.GetType())}");
            window.minSize = new Vector2(360, 76);
            window.ShowUtility();
            window.Build();
            return window;
        }

        private void OnEnable()
        {
            // A domain reload leaves the window with nothing to rename
            EditorApplication.delayCall += () =>
            {
                if (this != null && asset == null) Close();
            };
        }

        /// <summary>Renames the asset as the name field says, and closes.</summary>
        public void Rename()
        {
            if (asset != null) ForgeRenames.Rename(asset, newName);
            Close();
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            var nameField = new TextField("Name") { name = "NewName", value = newName, tooltip = "The asset's new name" };
            nameField.AddToClassList(TextField.alignedFieldUssClassName);
            nameField.RegisterValueChangedCallback(change => newName = change.newValue);
            // Enter renames and Escape cancels, as a rename in the Project window does
            nameField.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    key.StopPropagation();
                    Rename();
                }
                else if (key.keyCode == KeyCode.Escape)
                {
                    key.StopPropagation();
                    Close();
                }
            }, TrickleDown.TrickleDown);
            root.Add(nameField);
            // The name selected, to type over
            nameField.schedule.Execute(() =>
            {
                nameField.Focus();
                nameField.SelectAll();
            });

            var footer = new VisualElement { name = "Footer" };
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            footer.Add(new Button(Rename) { name = "Rename", text = "Rename" });
            root.Add(footer);
        }
    }
}
