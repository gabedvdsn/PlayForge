using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FarEmerald.PlayForge;
using FarEmerald.PlayForge.Extended;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A reference to an effect: Unity's object field, with Open or New beside it, and under it a line that says how the
    /// effect takes its levels, with Link to make it take them from the asset that holds the field (ED-38). Its
    /// right-click menu holds the field's template, a pick of the effects nothing links, and New Linked Effect. Setting
    /// the field asked in a dialog whether to link the effect, and the template, the link filter and a create button sat
    /// beside the field as tinted buttons.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayEffect))]
    public class GameplayEffectDrawer : AbstractForgeObjectDrawer<GameplayEffect>
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty prop)
        {
            var root = new VisualElement { name = "EffectReference" };
            // The style sheet sizes and colors the kit's glyphs, wherever the drawer is drawn: in Unity's own inspector too (ED-20)
            ForgeTheme.Apply(root);
            root.Add(base.CreatePropertyGUI(prop));
            var field = root.Q<ObjectField>("Reference");
            var holder = prop.serializedObject.targetObject as BaseForgeLevelProvider;

            var line = new VisualElement { name = "EffectLink" };
            line.AddToClassList("pf-link-line");
            root.Add(line);

            // The line follows the field, and the effect it names as that effect's link changes, an Undo among them; it
            // rebuilt every 500 ms for as long as the inspector stayed open (ED-11)
            VisualElement watch = null;
            GameplayEffect watched = null;
            void Refresh(GameplayEffect effect)
            {
                ShowLink(line, effect, holder, () => Refresh(field.value as GameplayEffect));
                if (effect == watched) return;
                watched = effect;
                watch?.RemoveFromHierarchy();
                watch = null;
                if (effect == null) return;
                watch = new VisualElement { name = "ReferencedEffectWatch" };
                root.Add(watch);
                watch.TrackSerializedObjectValue(new SerializedObject(effect), _ => Refresh(field.value as GameplayEffect));
            }
            field.RegisterValueChangedCallback(change => Refresh(change.newValue as GameplayEffect));
            root.TrackPropertyValue(prop, property => Refresh(property.objectReferenceValue as GameplayEffect));
            Refresh(prop.objectReferenceValue as GameplayEffect);
            return root;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // How the effect takes its levels
        // ═══════════════════════════════════════════════════════════════════════════

        // On a line of its own: from the asset that holds the field, from another asset, or from none, with Link to make
        // it take the holder's, as one Undo step. A dialog asked this as the field was set, and again when the effect was
        // linked elsewhere (ED-38)
        private static void ShowLink(VisualElement line, GameplayEffect effect, BaseForgeLevelProvider holder, Action refresh)
        {
            line.Clear();
            var shown = effect != null && holder != null && holder != effect;
            line.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shown) return;

            if (effect.IsLinkedTo(holder))
            {
                line.Add(new ForgeGlyph(ForgeGlyphShape.Link));
                line.Add(LineText($"Linked to {NameOf(holder)} · {Levels(holder)}"));
                return;
            }
            if (effect.IsLinked)
            {
                line.Add(new ForgeGlyph(ForgeGlyphShape.Link));
                line.Add(LineText($"Linked to {NameOf(effect.LinkedProvider)}"));
                line.Add(LinkButton("Link Here", effect, holder, refresh));
                return;
            }
            var warning = new ForgeGlyph(ForgeGlyphShape.Warning);
            warning.AddToClassList("pf-glyph--warning");
            line.Add(warning);
            line.Add(LineText("Not linked"));
            line.Add(LinkButton("Link", effect, holder, refresh));
        }

        private static Label LineText(string text)
        {
            var label = new Label(text);
            label.AddToClassList("pf-link-line__text");
            return label;
        }

        private static Button LinkButton(string text, GameplayEffect effect, BaseForgeLevelProvider holder, Action refresh)
        {
            var button = new Button(() =>
            {
                if (effect.HasCircularDependency(holder, out _, out var chain))
                {
                    Debug.LogError($"[PlayForge] Cannot link {effect.GetProviderName()} to {holder.GetProviderName()} due to a circular dependency." +
                                   $"\nLink Chain: {chain}.");
                    return;
                }
                Undo.RecordObject(effect, $"Link {NameOf(effect)}");
                effect.LinkToProvider(holder);
                EditorUtility.SetDirty(effect);
                refresh();
            })
            {
                text = text,
                tooltip = $"Make {NameOf(effect)} take its levels from {NameOf(holder)}",
            };
            button.AddToClassList("pf-link-line__button");
            return button;
        }

        private static string Levels(BaseForgeLevelProvider provider)
        {
            int starting = provider.GetStartingLevel(), max = provider.GetMaxLevel();
            return starting == max ? $"Level {starting}" : $"Levels {starting}–{max}";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // New, and the right-click menu
        // ═══════════════════════════════════════════════════════════════════════════

        // An effect New makes takes its levels from the asset that holds the field, as the + button's did
        protected override GameplayEffect New(Object host, string name)
        {
            var effect = base.New(host, name);
            Link(effect, host as BaseForgeLevelProvider);
            return effect;
        }

        // The field's template, a pick of the effects nothing links, and the named create, which sat beside the field as
        // tinted buttons (Docs/EDITOR_STYLE.md: template import, the link filter and create-linked go in its right-click
        // menu). The template asked in a dialog whether to copy it or use it, and told in another that it wasn't set
        protected override void AddMenuItems(DropdownMenu menu, ObjectField field, Object host)
        {
            var holder = host as BaseForgeLevelProvider;
            var template = GetFieldAttributes<ForgeEffectTemplateAttribute>().FirstOrDefault();
            if (template != null)
            {
                var display = EffectTemplateRegistry.GetDisplayName(template.TemplateKey);
                var asset = EffectTemplateRegistry.GetTemplate(template.TemplateKey);
                var status = asset != null ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
                menu.AppendAction($"Copy the {display} Template", _ => field.value = CopyTemplate(asset, holder, display), status);
                menu.AppendAction($"Use the {display} Template", _ => field.value = asset, status);
                if (asset == null)
                    menu.AppendAction($"No {display} Template Is Set in Project Settings", _ => { }, DropdownMenuAction.Status.Disabled);
                menu.AppendSeparator();
            }
            menu.AppendAction("Pick an Unlinked Effect...", _ => UnlinkedEffectPicker.Show(field));
            if (holder != null)
                menu.AppendAction("New Linked Effect...", _ => ShowCreateEffectDialog(field, holder, template?.TemplateKey));
        }

        // A copy of the template, named for the asset that holds the field, where The Forge makes effects, and linked to
        // the holder. It went next to the template, named with an underscore
        private static GameplayEffect CopyTemplate(GameplayEffect template, BaseForgeLevelProvider holder, string display)
        {
            if (template == null) return null;
            var copy = ForgeAssetCreation.Save(Object.Instantiate(template), $"{NameOf(holder)} {display}");
            Link(copy, holder);
            EditorGUIUtility.PingObject(copy);
            return copy;
        }

        private static void Link(GameplayEffect effect, BaseForgeLevelProvider holder)
        {
            if (effect == null || holder == null || holder == effect || effect.HasCircularDependency(holder, out _, out _)) return;
            effect.LinkToProvider(holder);
            EditorUtility.SetDirty(effect);
        }

        private static void ShowCreateEffectDialog(ObjectField field, BaseForgeLevelProvider holder, string templateKey)
        {
            var dialog = ScriptableObject.CreateInstance<CreateLinkedEffectDialog>();
            dialog.Initialize(holder, holder, created => field.value = created, templateKey);
            dialog.ShowUtility();
        }

        // The effects nothing links, to pick from: the link filter that sat beside the field, in its right-click menu
        // (ED-38)
        private sealed class UnlinkedEffectPicker : AdvancedDropdown
        {
            private readonly ObjectField field;
            private readonly List<GameplayEffect> effects;

            private UnlinkedEffectPicker(ObjectField field, List<GameplayEffect> effects) : base(new AdvancedDropdownState())
            {
                this.field = field;
                this.effects = effects;
                minimumSize = new Vector2(240, 280);
            }

            public static void Show(ObjectField field)
            {
                var effects = AssetDatabase.FindAssets($"t:{nameof(GameplayEffect)}")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<GameplayEffect>)
                    .Where(effect => effect != null && !effect.IsLinked)
                    .OrderBy(effect => NameOf(effect), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                new UnlinkedEffectPicker(field, effects).Show(field.worldBound);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Unlinked Effects");
                for (int i = 0; i < effects.Count; i++) root.AddChild(new AdvancedDropdownItem(NameOf(effects[i])) { id = i });
                if (effects.Count == 0) root.AddChild(new AdvancedDropdownItem("None") { enabled = false });
                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item.id >= 0 && item.id < effects.Count) field.value = effects[item.id];
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Create Linked Effect Dialog
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class CreateLinkedEffectDialog : EditorWindow
    {
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
        
        private BaseForgeLevelProvider _linkTarget;
        private ILevelProvider _provider;
        private Action<GameplayEffect> _onCreated;
        private string _templateKey;
        
        private string _effectName = "";
        private Label _previewLabel;
        private bool _useTemplate = false;
        private GameplayEffect _template;
        
        public void Initialize(BaseForgeLevelProvider linkTarget, ILevelProvider provider, Action<GameplayEffect> onCreated, string templateKey = null)
        {
            _linkTarget = linkTarget;
            _provider = provider;
            _onCreated = onCreated;
            _templateKey = templateKey;
            
            if (!string.IsNullOrEmpty(_templateKey))
            {
                _template = EffectTemplateRegistry.GetTemplate(_templateKey);
            }
            
            titleContent = new GUIContent("Create Linked Effect");
            minSize = maxSize = new Vector2(380, _template != null ? 240 : 200);
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            
            // Header
            var header = new Label("Create New Effect");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Colors.HeaderText;
            header.style.marginBottom = 12;
            root.Add(header);
            
            // Link preview
            var providerType = _linkTarget is Ability ? "Ability" : "Entity";
            var providerColor = _linkTarget is Ability ? Colors.AccentOrange : Colors.AccentPurple;
            
            var linkBox = new VisualElement();
            linkBox.style.flexDirection = FlexDirection.Row;
            linkBox.style.alignItems = Align.Center;
            linkBox.style.paddingLeft = 8;
            linkBox.style.paddingRight = 8;
            linkBox.style.paddingTop = 6;
            linkBox.style.paddingBottom = 6;
            linkBox.style.marginBottom = 12;
            linkBox.style.backgroundColor = new Color(0.2f, 0.25f, 0.2f, 0.5f);
            linkBox.style.borderTopLeftRadius = 4;
            linkBox.style.borderTopRightRadius = 4;
            linkBox.style.borderBottomLeftRadius = 4;
            linkBox.style.borderBottomRightRadius = 4;
            linkBox.style.borderLeftWidth = 3;
            linkBox.style.borderLeftColor = Colors.AccentGreen;
            root.Add(linkBox);
            
            var linkIcon = new ForgeGlyph(ForgeGlyphShape.Link);
            linkIcon.style.marginRight = 6;
            linkBox.Add(linkIcon);
            
            var linkText = new Label($"Will link to: ");
            linkText.style.color = Colors.LabelText;
            linkText.style.fontSize = 11;
            linkBox.Add(linkText);
            
            var providerBadge = new Label(providerType);
            providerBadge.style.fontSize = 9;
            providerBadge.style.color = providerColor;
            providerBadge.style.backgroundColor = new Color(providerColor.r, providerColor.g, providerColor.b, 0.2f);
            providerBadge.style.paddingLeft = 4;
            providerBadge.style.paddingRight = 4;
            providerBadge.style.paddingTop = 2;
            providerBadge.style.paddingBottom = 2;
            providerBadge.style.borderTopLeftRadius = 3;
            providerBadge.style.borderTopRightRadius = 3;
            providerBadge.style.borderBottomLeftRadius = 3;
            providerBadge.style.borderBottomRightRadius = 3;
            providerBadge.style.marginRight = 4;
            linkBox.Add(providerBadge);
            
            var providerName = new Label(_provider.GetProviderName());
            providerName.style.fontSize = 11;
            providerName.style.color = Colors.HeaderText;
            providerName.style.unityFontStyleAndWeight = FontStyle.Bold;
            linkBox.Add(providerName);
            
            var levelLabel = new Label($" (Max Lv{_provider.GetMaxLevel()})");
            levelLabel.style.fontSize = 10;
            levelLabel.style.color = Colors.AccentGreen;
            linkBox.Add(levelLabel);
            
            // Template option (if template exists)
            if (_template != null)
            {
                var templateRow = new VisualElement();
                templateRow.style.flexDirection = FlexDirection.Row;
                templateRow.style.alignItems = Align.Center;
                templateRow.style.marginBottom = 8;
                root.Add(templateRow);
                
                var templateToggle = new Toggle("Use template:");
                templateToggle.value = _useTemplate;
                templateToggle.style.marginRight = 8;
                templateToggle.RegisterValueChangedCallback(evt => _useTemplate = evt.newValue);
                templateRow.Add(templateToggle);
                
                var templateLabel = new Label(_template.GetName());
                templateLabel.style.fontSize = 10;
                templateLabel.style.color = Colors.AccentCyan;
                templateLabel.style.backgroundColor = new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, 0.15f);
                templateLabel.style.paddingLeft = 4;
                templateLabel.style.paddingRight = 4;
                templateLabel.style.paddingTop = 2;
                templateLabel.style.paddingBottom = 2;
                templateLabel.style.borderTopLeftRadius = 3;
                templateLabel.style.borderTopRightRadius = 3;
                templateLabel.style.borderBottomLeftRadius = 3;
                templateLabel.style.borderBottomRightRadius = 3;
                templateRow.Add(templateLabel);
            }
            
            // Name input
            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 4;
            root.Add(nameRow);
            
            var nameLabel = new Label("Effect Name");
            nameLabel.style.width = 80;
            nameRow.Add(nameLabel);
            
            var nameField = new TextField();
            nameField.style.flexGrow = 1;
            nameField.RegisterValueChangedCallback(evt =>
            {
                _effectName = evt.newValue;
                UpdatePreview();
            });
            nameRow.Add(nameField);
            
            // File preview
            _previewLabel = new Label();
            _previewLabel.style.fontSize = 10;
            _previewLabel.style.color = Colors.HintText;
            _previewLabel.style.marginLeft = 80;
            _previewLabel.style.marginBottom = 8;
            root.Add(_previewLabel);
            UpdatePreview();
            
            // Path info
            var pathLabel = new Label($"Path: {GetEffectPath()}/");
            pathLabel.style.fontSize = 10;
            pathLabel.style.color = Colors.HintText;
            pathLabel.style.marginBottom = 16;
            root.Add(pathLabel);
            
            // Buttons
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            root.Add(btnRow);
            
            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.style.marginRight = 8;
            btnRow.Add(cancelBtn);
            
            var createBtn = new Button(CreateEffect) { text = "Create & Link" };
            createBtn.style.backgroundColor = Colors.AccentGreen;
            btnRow.Add(createBtn);
            
            nameField.Focus();
        }
        
        private void UpdatePreview()
        {
            if (_previewLabel == null) return;
            
            if (string.IsNullOrEmpty(_effectName))
            {
                _previewLabel.text = "File: (enter name)";
            }
            else
            {
                var fileName = GetSanitizedFileName(_effectName);
                _previewLabel.text = $"File: {fileName}.asset";
            }
        }
        
        private string GetSanitizedFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            
            var sanitized = Regex.Replace(name, @"\s+", "");
            foreach (var c in InvalidFileNameChars)
            {
                sanitized = sanitized.Replace(c.ToString(), "");
            }
            sanitized = Regex.Replace(sanitized, @"[<>:""/\\|?*]", "");
            
            // The prefix and suffix Project Settings → PlayForge names for effects. This dialog read them with defaults of
            // its own, an "Effect_" prefix among them (ED-21)
            var prefix = PlayForgeSettings.PrefixFor(typeof(GameplayEffect));
            var suffix = PlayForgeSettings.SuffixFor(typeof(GameplayEffect));
            
            return $"{prefix}{sanitized}{suffix}";
        }
        
        private string GetEffectPath()
        {
            return PlayForgeSettings.FolderFor(typeof(GameplayEffect));
        }
        
        private void CreateEffect()
        {
            if (string.IsNullOrEmpty(_effectName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a name for the effect.", "OK");
                return;
            }
            
            var sanitizedFileName = GetSanitizedFileName(_effectName);
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                EditorUtility.DisplayDialog("Error", "The name contains only invalid characters.", "OK");
                return;
            }
            
            var path = GetEffectPath();
            if (!AssetDatabase.IsValidFolder(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
            
            // Create the effect (optionally from template)
            GameplayEffect effect;
            if (_useTemplate && _template != null)
            {
                effect = Instantiate(_template);
            }
            else
            {
                effect = CreateInstance<GameplayEffect>();
            }
            
            // Set the name
            effect.Definition = new GameplayEffectDefinition
            {
                Name = _effectName,
                Description = "",
                Textures = new System.Collections.Generic.List<TextureItem>()
            };
            
            // Link to the provider
            effect.LinkToProvider(_linkTarget);
            
            // Save the asset
            var fullPath = $"{path}/{sanitizedFileName}.asset";
            fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath);
            
            AssetDatabase.CreateAsset(effect, fullPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[PlayForge] Created effect '{_effectName}' linked to {_provider.GetProviderName()} at {fullPath}");
            
            // Callback
            _onCreated?.Invoke(effect);
            
            // Select and ping
            Selection.activeObject = effect;
            EditorGUIUtility.PingObject(effect);
            
            Close();
        }
    }
}