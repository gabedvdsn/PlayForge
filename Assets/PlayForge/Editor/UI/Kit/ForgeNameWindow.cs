using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A name asked for, as a tag's new path in The Forge's Tags view or a new tag context (FN-9): a field with the name to
    /// type over, then the action, which Enter presses too, and Escape cancels. A name that won't do says why under the
    /// field, and the window stays.
    /// </summary>
    internal sealed class ForgeNameWindow : EditorWindow
    {
        private string label = "Name";
        private string start = "";
        private string act = "OK";
        private Func<string, string> problemOf;
        private Action<string> done;
        private TextField field;
        private HelpBox problem;

        /// <summary>
        /// Opens the window: its title, the field's label and the name it starts with, the action's name, why a name won't
        /// do (null where it will), and what the action does with the name.
        /// </summary>
        public static ForgeNameWindow Show(string title, string label, string start, string act, Func<string, string> problemOf, Action<string> done)
        {
            var window = CreateInstance<ForgeNameWindow>();
            window.titleContent = new GUIContent(title);
            window.label = label;
            window.start = start ?? "";
            window.act = act;
            window.problemOf = problemOf;
            window.done = done;
            window.minSize = new Vector2(380f, 76f);
            window.maxSize = new Vector2(380f, 150f);
            window.ShowUtility();
            return window;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            field = new TextField(label) { name = "Name", value = start };
            field.AddToClassList(TextField.alignedFieldUssClassName);
            field.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    key.StopPropagation();
                    Confirm();
                }
                else if (key.keyCode == KeyCode.Escape)
                {
                    key.StopPropagation();
                    Close();
                }
            }, TrickleDown.TrickleDown);
            root.Add(field);

            problem = new HelpBox("", HelpBoxMessageType.Warning) { name = "Problem" };
            problem.AddToClassList("pf-hidden");
            root.Add(problem);

            var footer = new VisualElement { name = "Footer" };
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            footer.Add(new Button(Confirm) { name = "Confirm", text = act });
            root.Add(footer);
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
        }

        /// <summary>Does the action with the name, and closes; or says why the name won't do, and stays.</summary>
        internal void Confirm()
        {
            var name = field?.value?.Trim() ?? "";
            var why = problemOf?.Invoke(name);
            if (why != null)
            {
                problem.text = why;
                problem.RemoveFromClassList("pf-hidden");
                if (position.height < 130f) position = new Rect(position.position, new Vector2(position.width, 130f));
                return;
            }
            done?.Invoke(name);
            Close();
        }
    }
}
