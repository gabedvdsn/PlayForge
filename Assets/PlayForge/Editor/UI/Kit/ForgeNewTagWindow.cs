using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// New Tag, which a tag field's picker opens (ED-55): a path, as "Status.Debuff.Burn", its contexts, then Create, which
    /// makes the tag unique, as the tag picker made each new one, and gives it to the field. A path the registry can't take
    /// says why under the field. The picker made a tag from its search text, with a button that read "+". Its contexts
    /// start as the field's, which its use there gives it; others chosen declare it for them too, so their fields offer it
    /// (ED-83). A tag's contexts came from its uses alone. A path that is one of PlayForge's own tags' text makes that very
    /// tag, so it says so as it's typed, and offers the core tag, where it made it with no word (TAG-2, Decision 71).
    /// </summary>
    internal sealed class ForgeNewTagWindow : EditorWindow
    {
        private string start = "";
        private string[] fieldContexts = Array.Empty<string>();
        private Action<Tag> made;
        private TextField path;
        private ForgeTagContextsField contexts;
        private HelpBox problem;
        private HelpBox core;
        private Tag coreTag;

        /// <summary>Opens New Tag with a path to start from, as the field's tag's parents.</summary>
        public static ForgeNewTagWindow Show(string start, Action<Tag> made) => ShowFor(start, null, made);

        /// <summary>Opens New Tag for a field of the contexts given, which the tag's contexts start as (ED-83).</summary>
        public static ForgeNewTagWindow ShowFor(string start, string[] fieldContexts, Action<Tag> made)
        {
            var window = CreateInstance<ForgeNewTagWindow>();
            window.titleContent = new GUIContent("New Tag");
            window.start = start ?? "";
            window.fieldContexts = fieldContexts ?? Array.Empty<string>();
            window.made = made;
            window.minSize = new Vector2(380f, 118f);
            window.maxSize = new Vector2(380f, 200f);
            window.ShowUtility();
            return window;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            path = new TextField("Path")
            {
                name = "TagPath",
                value = start,
                tooltip = "The tag's path, its parents first, as Status.Debuff.Burn",
            };
            path.AddToClassList(TextField.alignedFieldUssClassName);
            path.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is KeyCode.Return or KeyCode.KeypadEnter) Create();
                else if (key.keyCode == KeyCode.Escape) Close();
            }, TrickleDown.TrickleDown);
            root.Add(path);

            // The field's contexts, which its use there gives the tag; another chosen declares it for that one too (ED-83)
            contexts = new ForgeTagContextsField("Contexts", fieldContexts)
            {
                name = "TagContexts",
                tooltip = "The contexts whose fields offer the tag: the field's, and any other chosen",
            };
            root.Add(contexts);

            problem = new HelpBox("", HelpBoxMessageType.Warning) { name = "PathProblem" };
            problem.AddToClassList("pf-hidden");
            root.Add(problem);

            // A path that is a core tag's text makes that very tag: said as it's typed, with the core tag to use (TAG-2)
            core = new HelpBox("", HelpBoxMessageType.Warning) { name = "CoreTag" };
            core.AddToClassList("pf-hidden");
            core.Add(new Button(UseCore) { name = "UseCoreTag", text = "Use It", tooltip = "Give the field PlayForge's own tag" });
            root.Add(core);
            path.RegisterValueChangedCallback(_ => ShowCore());
            ShowCore();

            var footer = new VisualElement();
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            footer.Add(new Button(Create) { name = "Create", text = "Create" });
            root.Add(footer);
            path.schedule.Execute(() =>
            {
                path.Focus();
                path.SelectRange(path.value.Length, path.value.Length);
            });
        }

        // Makes the tag, if the path makes one, and gives it to the field
        internal void Create()
        {
            var text = path?.value?.Trim() ?? "";
            var why = ProblemOf(text);
            if (why != null)
            {
                problem.text = why;
                problem.RemoveFromClassList("pf-hidden");
                return;
            }
            if (IsCoreText(text, out _))
            {
                ShowCore();
                return;
            }
            var tag = Tag.GenerateAsUnique(text);
            // Contexts other than the field's declare it, so their fields offer it (ED-83)
            var chosen = contexts?.Contexts ?? (IReadOnlyList<string>)fieldContexts;
            if (!new HashSet<string>(chosen).SetEquals(fieldContexts)) ForgeTagContexts.Declare(tag, chosen);
            made?.Invoke(tag);
            Close();
        }

        // Shows whether the path is a core tag's text, and makes room for the word
        private void ShowCore()
        {
            if (core == null) return;
            bool isCore = IsCoreText(path?.value?.Trim(), out coreTag);
            core.text = isCore
                ? $"{coreTag} is one of PlayForge's own tags, and a tag made of this path would be that very one. Use it, or give another path."
                : "";
            core.EnableInClassList("pf-hidden", !isCore);
            if (isCore && position.height < 170f) position = new Rect(position.position, new Vector2(position.width, 170f));
        }

        // Gives the field the core tag the path names, and closes
        private void UseCore()
        {
            if (!coreTag.IsValid) return;
            made?.Invoke(coreTag);
            Close();
        }

        // PlayForge's own tags by the text each reads as, which a path of that text would make into that very tag (TAG-2)
        private static Dictionary<string, Tag> coreByText;

        /// <summary>Whether a name is one of PlayForge's own tags', which code writes (FN-9).</summary>
        internal static bool IsCoreName(string name)
        {
            IsCoreText("", out _);
            return !string.IsNullOrEmpty(name) && coreByText.Values.Any(tag => tag.Name == name);
        }

        /// <summary>Whether a path is one of PlayForge's own tags' text, and that tag (TAG-2).</summary>
        internal static bool IsCoreText(string text, out Tag found)
        {
            coreByText ??= typeof(Tags).GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.PropertyType == typeof(Tag))
                .Select(property => (Tag)property.GetValue(null))
                .Where(tag => tag.IsValid)
                .GroupBy(tag => tag.ToString(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            found = default;
            return !string.IsNullOrEmpty(text) && coreByText.TryGetValue(text, out found);
        }

        /// <summary>Why a path makes no tag, or null when it makes one.</summary>
        internal static string ProblemOf(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "A tag needs a path.";
            if (!TagRegistry.IsValidPath(text)) return "A path is names of letters, numbers and underscores, joined by dots.";
            return null;
        }
    }
}
