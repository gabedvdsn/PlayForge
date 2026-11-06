using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Views that take turns in one place, as the Debugger's views and The Forge's two lists do. Each view stays laid out
    /// at the place's size while another shows, and only the one shown is visible. Hidden by display: none, a view was
    /// laid out at 0 px, so its split's fixed pane shrank to nothing, and its tables' columns to their least, and neither
    /// came back when the view showed again (ED-45, ED-48).
    /// </summary>
    internal static class ForgeStack
    {
        public const string StackClass = "pf-stack";
        public const string HiddenClass = "pf-stack__hidden";

        /// <summary>Makes an element a place where views take turns.</summary>
        public static T Make<T>(T place) where T : VisualElement
        {
            place.AddToClassList(StackClass);
            return place;
        }

        /// <summary>Shows a view in its place, or hides it there. A view that hides gives up the keyboard.</summary>
        public static void Show(VisualElement view, bool shown)
        {
            if (view == null) return;
            bool hidden = view.ClassListContains(HiddenClass);
            view.EnableInClassList(HiddenClass, !shown);
            if (shown)
            {
                // A split takes no notice of its size while it's hidden, so one whose window changed width meanwhile
                // kept its splitter where its panes met before. Once the view is visible again, its splits hear their size
                if (hidden) ResizeWhenShown(view, 8);
                return;
            }
            if (view.focusController?.focusedElement is VisualElement focused && (focused == view || view.Contains(focused)))
                focused.Blur();
        }

        // A view's style says it's visible only after the panel's next style pass, which can come after its scheduler's
        // next run, so this waits for it a few runs
        private static void ResizeWhenShown(VisualElement view, int tries)
        {
            view.schedule.Execute(() =>
            {
                if (view.ClassListContains(HiddenClass)) return;
                if (view.resolvedStyle.visibility == Visibility.Hidden)
                {
                    if (tries > 0) ResizeWhenShown(view, tries - 1);
                    return;
                }
                view.Query<TwoPaneSplitView>().ForEach(Resized);
            });
        }

        // Tells a split its size, as a change of its size does
        private static void Resized(TwoPaneSplitView split)
        {
            using var changed = GeometryChangedEvent.GetPooled(split.layout, split.layout);
            changed.target = split;
            split.SendEvent(changed);
        }
    }
}
