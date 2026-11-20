using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    // The ability system's views in the Debugger, Entities, Tags and the Timeline, hooked into the window: the hubs
    // ProcessControl holds, the toolbar's hub filter and Follow Selection, the status bar's count of entities, and each
    // view built, shown, laid out and read with the rest
    public sealed partial class RuntimeDebugger
    {
        private enum HubKind
        {
            Both,
            Mono,
            Runtime,
        }

        private static readonly (HubKind Kind, string Text)[] HubFilters =
        {
            (HubKind.Both, "Both hubs"),
            (HubKind.Mono, "Mono hubs"),
            (HubKind.Runtime, "Runtime hubs"),
        };

        // The hub filter and Follow Selection, which a domain reload keeps
        [SerializeField] private HubKind hubFilter;
        [SerializeField] private bool followSelection = true;

        private VisualElement entitiesView;
        private VisualElement tagsView;
        private ToolbarMenu hubMenu;
        private ToolbarToggle followToggle;
        private Label entityStatus;
        private TwoPaneSplitView entitySplit;
        private TwoPaneSplitView tagSplit;
        private bool timelineLive;

        partial void EnableAbilityViews() => Selection.selectionChanged += OnSelectionChanged;

        partial void DisableAbilityViews()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            ForgetShownEntity();
            recorder?.Dispose();
        }

        partial void AddAbilityViewToggles(List<(View View, string Text, string Tip)> toggles)
        {
            toggles.Insert(0, (View.Entities, "Entities", "Each entity, with its attributes, effects, abilities and tags"));
            toggles.Add((View.Tags, "Tags", "Each tag the entities hold, and who holds it"));
            toggles.Add((View.Timeline, "Timeline", "What the entities' abilities and effects did, over time"));
        }

        partial void BuildAbilityViews(VisualElement views)
        {
            entitiesView = BuildEntitiesView();
            tagsView = BuildTagsView();
            views.Insert(0, entitiesView);
            views.Add(tagsView);
            views.Add(BuildTimelineView());
        }

        partial void BuildEntityFilter(Toolbar toolbar)
        {
            hubMenu = new ToolbarMenu { name = "HubFilter", tooltip = "Show the entities of one kind of hub" };
            foreach (var (kind, text) in HubFilters)
                hubMenu.menu.AppendAction(text, _ =>
                {
                    hubFilter = kind;
                    ShowView();
                    RefillEntities();
                    RefillTags();
                }, _ => hubFilter == kind ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            toolbar.Add(hubMenu);
        }

        partial void BuildTimelineToolbar(Toolbar toolbar) => BuildTimelineTools(toolbar);

        partial void BuildFollowToggle(Toolbar toolbar)
        {
            followToggle = new ToolbarToggle { name = "FollowSelection", tooltip = "Show the entity whose GameObject is selected in the scene" };
            followToggle.AddToClassList("pf-debugger__follow");
            followToggle.Add(new ForgeGlyph(ForgeGlyphShape.Pointer));
            followToggle.Add(new Label("Follow Selection"));
            followToggle.SetValueWithoutNotify(followSelection);
            followToggle.RegisterValueChangedCallback(change =>
            {
                followSelection = change.newValue;
                OnSelectionChanged();
            });
            toolbar.Add(followToggle);
        }

        partial void BuildAbilityStatus() => entityStatus = StatusPart("EntityStatus");

        // The Timeline records afresh each time the game starts playing, and stops hearing as it ends
        partial void ShowAbilityState(bool live)
        {
            if (live && !timelineLive) StartTimeline();
            if (!live) StopTimeline();
            timelineLive = live;
        }

        partial void ShowAbilityViews(bool live)
        {
            ForgeStack.Show(entitiesView, view == View.Entities);
            ForgeStack.Show(tagsView, view == View.Tags);
            ForgeStack.Show(timelineView, view == View.Timeline);
            hubMenu.EnableInClassList(HiddenClass, !live || view == View.Processes || view == View.Timeline);
            ShowTimelineTools(live);
            followToggle.EnableInClassList(HiddenClass, !live || view != View.Entities);
            hubMenu.text = HubFilters.First(each => each.Kind == hubFilter).Text;
        }

        // The entities by hub
        partial void ShowAbilityStatus()
        {
            if (followed == null)
            {
                entityStatus.text = "";
                return;
            }
            int mono = hubs.Count(gas => gas.ToGASObject() != null);
            entityStatus.text = narrow
                ? CountOf(hubs.Count, "entity", "entities")
                : $"{CountOf(hubs.Count, "entity", "entities")}: {mono} Mono, {hubs.Count - mono} runtime";
        }

        partial void FitAbilityViews()
        {
            Lay(entitySplit, 0, 440);
            Lay(tagSplit, 0, 440);
            Lay(timelineSplit, 1, 380);
        }

        // The hubs among the processes, of either kind
        partial void ReadAbilityLists()
        {
            hubs.Clear();
            foreach (var pcb in processes)
            {
                if (pcb.Wrapper != null && pcb.Wrapper.TryGetProcess<IGameplayAbilitySystem>(out var gas) && Alive(gas) && !hubs.Contains(gas))
                    hubs.Add(gas);
            }
            hubs.Sort((a, b) => EditorUtility.NaturalCompare(NameOf(a), NameOf(b)));
            RefillEntities();
            RefillTags();
        }

        partial void RefreshAbilityLists()
        {
            entityList.RefreshItems();
            SampleTagTree();
        }

        partial void RefillAbilityViews()
        {
            RefillEntities();
            RefillTags();
            RefillTracks();
        }

        partial void SampleAbilityViews()
        {
            SampleEntity();
            SampleTag();
            SampleTimeline();
        }

        static partial void NameHandler(IGameplayProcessHandler handler, ref string name)
        {
            if (handler is IGameplayAbilitySystem gas) name = NameOf(gas);
        }

        // Follow Selection: a hub's GameObject selected in the scene is the entity the Entities view shows
        private void OnSelectionChanged()
        {
            if (!followSelection || !Live() || entityList == null) return;
            var body = Selection.activeGameObject;
            if (body == null) return;
            var gas = body.GetComponentInParent<GameplayAbilitySystem>();
            if (gas != null && hubs.Contains(gas)) SelectEntity(gas);
        }

        /// <summary>
        /// An entity's name: a MonoBehaviour hub's GameObject's, which tells two of one asset apart, as "Goblin Raider
        /// (2)", or a runtime hub's own.
        /// </summary>
        internal static string NameOf(IGameplayAbilitySystem gas)
        {
            if (gas == null) return "";
            var body = gas.ToGASObject();
            if (body != null) return body.name;
            var name = gas.GetName();
            return !string.IsNullOrWhiteSpace(name) ? name : "Unnamed";
        }

        // A hub of either kind that's still there: a MonoBehaviour hub's object goes with its scene
        private static bool Alive(IGameplayAbilitySystem gas) => gas is Object unity ? unity != null : gas != null;
    }
}
