using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A new asset's name and folder, then Create, as The Forge's Create menu asks (Q20). It makes the asset through the one
    /// factory a field's New uses too, <see cref="ForgeAssetCreation.CreateNew"/>: a copy of the type's New Asset default,
    /// named, with the tag its name gives, in the folder Project Settings names. The Create tab's dialog set type
    /// settings that did nothing, and logged each asset it made.
    /// </summary>
    public sealed class ForgeNewAssetWindow : EditorWindow
    {
        private Type type;
        private Action<Object> created;
        private string assetName;
        private string folder;
        private Label fileLabel;

        public static ForgeNewAssetWindow Show(Type type, Action<Object> onCreated)
        {
            var window = CreateInstance<ForgeNewAssetWindow>();
            window.type = type;
            window.created = onCreated;
            window.assetName = $"New {ForgeAssetTypes.NameOf(type)}";
            window.folder = PlayForgeSettings.FolderFor(type);
            window.titleContent = new GUIContent($"New {ForgeAssetTypes.NameOf(type)}");
            window.minSize = new Vector2(380, 128);
            window.ShowUtility();
            window.Build();
            return window;
        }

        private void OnEnable()
        {
            // A domain reload leaves the window with no type to make
            EditorApplication.delayCall += () =>
            {
                if (this != null && type == null) Close();
            };
        }

        /// <summary>Makes the asset, named, in the folder, then closes and hands it on. Returns null for an empty name.</summary>
        public ScriptableObject CreateAsset()
        {
            if (type == null || string.IsNullOrWhiteSpace(assetName)) return null;
            var asset = ForgeAssetCreation.CreateNew(type, assetName.Trim(), folder);
            Close();
            created?.Invoke(asset);
            return asset;
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            var nameField = new TextField("Name") { name = "AssetName", value = assetName, tooltip = "The new asset's name, which gives its asset tag" };
            nameField.AddToClassList(TextField.alignedFieldUssClassName);
            nameField.RegisterValueChangedCallback(change =>
            {
                assetName = change.newValue;
                ShowFile();
            });
            root.Add(nameField);

            var folderRow = new VisualElement { name = "FolderRow" };
            folderRow.AddToClassList("pf-dialog__row");
            var folderField = new TextField("Folder") { name = "Folder", value = folder, tooltip = "The folder the asset goes in" };
            folderField.AddToClassList(TextField.alignedFieldUssClassName);
            folderField.AddToClassList("pf-dialog__grow");
            folderField.RegisterValueChangedCallback(change => folder = change.newValue);
            folderRow.Add(folderField);
            folderRow.Add(new Button(() =>
            {
                var chosen = EditorUtility.OpenFolderPanel($"Make the {ForgeAssetTypes.NameOf(type)} in", folder, "");
                if (string.IsNullOrEmpty(chosen)) return;
                var data = Application.dataPath.Replace('\\', '/');
                chosen = chosen.Replace('\\', '/');
                if (!chosen.StartsWith(data)) return;
                folder = "Assets" + chosen.Substring(data.Length);
                folderField.SetValueWithoutNotify(folder);
            }) { name = "Choose", text = "Choose…" });
            root.Add(folderRow);

            fileLabel = new Label { name = "File" };
            fileLabel.AddToClassList("pf-note");
            root.Add(fileLabel);
            ShowFile();

            var footer = new VisualElement { name = "Footer" };
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            footer.Add(new Button(() => CreateAsset()) { name = "CreateAsset", text = "Create" });
            root.Add(footer);

            // Enter makes it, and Escape closes
            root.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is KeyCode.Return or KeyCode.KeypadEnter) CreateAsset();
                else if (key.keyCode == KeyCode.Escape) Close();
            }, TrickleDown.TrickleDown);
            nameField.schedule.Execute(() =>
            {
                nameField.Focus();
                nameField.SelectAll();
            });
        }

        // The file the asset will be, with the type's prefix and suffix
        private void ShowFile()
        {
            if (fileLabel == null || type == null) return;
            fileLabel.text = $"File: {PlayForgeSettings.PrefixFor(type)}{ForgeAssetCreation.FileNameOf(assetName)}{PlayForgeSettings.SuffixFor(type)}.asset";
        }
    }
}
