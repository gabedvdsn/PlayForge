using System;
using System.Collections.Generic;
using System.Linq;
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
                menu.AppendAction("New Linked Effect...", _ => NewLinked(field, holder));
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

        // A new effect, named in The Forge's New Asset window, which takes its levels from the asset that holds the field
        // (ED-61). A dialog of its own asked for the name, with a tinted link box, colored badges and a green Create, and
        // offered the field's template, which the menu's Copy the Template does
        public static ForgeNewAssetWindow NewLinked(ObjectField field, BaseForgeLevelProvider holder) =>
            ForgeNewAssetWindow.Show(typeof(GameplayEffect), created =>
            {
                if (created is not GameplayEffect effect) return;
                Link(effect, holder);
                field.value = effect;
            });

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
}
