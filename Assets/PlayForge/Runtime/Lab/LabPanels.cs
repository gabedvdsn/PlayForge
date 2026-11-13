using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// The lab's panels (LAB-5), UI Toolkit over the space: the library on the left, the selected in the middle, hidden
    /// until something is, the mode on the right, and the log at the bottom; each collapses to a tab at its edge. Over the
    /// space, each unit's bar. An asset dragged from the library and dropped on the space is the lab's to apply, cast or
    /// give (LAB-6). Neutral panels, Unity's own controls, and a type's color in its icon alone (`Docs/EDITOR_STYLE.md`).
    /// </summary>
    public class LabPanels : MonoBehaviour
    {
        private const float DragStart = 6f;
        private const float RedrawEvery = 0.25f;

        public UIDocument Document { get; private set; }

        /// <summary>The panels' root, where a test finds a panel by its name: Library, Selected, Mode, Log.</summary>
        public VisualElement Root { get; private set; }

        private CreatorLab lab;
        private PanelSettings settings;
        private VisualElement overlay;
        private VisualElement library, selected, mode, log;
        private ScrollView libraryList, selectedBody;
        private TextField search;
        private Label libraryName, ghost, hint;
        private DropdownField dummyEntity, dummySide;
        private VisualElement dummySection, battleSection;
        private SliderInt botLevel, perSide;
        private Label battleLine;
        private VisualElement unitSection;
        private Label controlLine;
        private Button takeControl, releaseControl, followUnit;
        private DropdownField teachChoice, giveChoice;
        private Slider distance, tilt, speed;
        private Button pause;
        private readonly Dictionary<LabUnit, (VisualElement root, VisualElement fill, Label line)> bars = new();
        private readonly List<ScriptableObject> entityChoices = new();
        private float nextRedraw;
        private bool dirty = true;

        // A drag from the library
        private ScriptableObject pressed;
        private Vector2 pressedAt;
        private bool dragging;

        private void Start()
        {
            lab = GetComponent<CreatorLab>();
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.hideFlags = HideFlags.DontSave;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("PlayForge Lab/Lab Theme");
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.sortingOrder = 100;
            Document = gameObject.AddComponent<UIDocument>();
            Document.panelSettings = settings;
            Build();
            if (lab != null)
            {
                lab.Changed += MarkDirty;
                if (lab.View) lab.View.PointerOverPanel = IsOverPanel;
            }
        }

        private void OnDestroy()
        {
            if (lab != null)
            {
                lab.Changed -= MarkDirty;
                if (lab.View) lab.View.PointerOverPanel = null;
            }
            if (Root != null)
            {
                Root.UnregisterCallback<PointerMoveEvent>(Moved, TrickleDown.TrickleDown);
                Root.UnregisterCallback<PointerUpEvent>(Released, TrickleDown.TrickleDown);
                Root.Clear();
                Root = null;
            }
            bars.Clear();
            if (Document) Document.panelSettings = null;
            if (settings) DestroyImmediate(settings);
        }

        private void MarkDirty() => dirty = true;

        // ═══════════════════════════════════════════════════════════════════════════
        // Building
        // ═══════════════════════════════════════════════════════════════════════════

        private void Build()
        {
            Root = Document.rootVisualElement;
            if (Root == null) return;
            Root.Clear();
            var sheet = Resources.Load<StyleSheet>("PlayForge Lab/Lab");
            if (sheet) Root.styleSheets.Add(sheet);
            Root.AddToClassList("lab-root");
            Root.pickingMode = PickingMode.Ignore;

            overlay = new VisualElement { name = "Overlay", pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("lab-overlay");
            Root.Add(overlay);

            library = Panel("Library", "lab-panel--left", "Library");
            selected = Panel("Selected", "lab-panel--middle", "Selected");
            mode = Panel("Mode", "lab-panel--right", "Lab");
            log = Panel("Log", "lab-panel--bottom", "Log");
            Collapse(log, true);

            BuildLibrary();
            BuildMode();
            selectedBody = new ScrollView { name = "SelectedBody" };
            Content(selected).Add(selectedBody);
            Content(log).Add(new Label("The journals come in phase 5 (LAB-12 to LAB-16).") { name = "LogEmpty" });

            ghost = new Label { name = "Ghost", pickingMode = PickingMode.Ignore };
            ghost.AddToClassList("lab-ghost");
            ghost.style.display = DisplayStyle.None;
            Root.Add(ghost);
            hint = new Label { name = "Hint", pickingMode = PickingMode.Ignore };
            hint.AddToClassList("lab-hint");
            hint.style.display = DisplayStyle.None;
            Root.Add(hint);

            // A drag that ends anywhere ends here, the library's row having captured the pointer
            Root.RegisterCallback<PointerMoveEvent>(Moved, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerUpEvent>(Released, TrickleDown.TrickleDown);
            Redraw();
        }

        // A panel with a header that collapses it to a tab at its edge, and its content under
        private VisualElement Panel(string panelName, string place, string title)
        {
            var panel = new VisualElement { name = panelName };
            panel.AddToClassList("lab-panel");
            panel.AddToClassList(place);
            var header = new VisualElement { name = $"{panelName}Header" };
            header.AddToClassList("lab-panel__header");
            var titleLabel = new Label(title) { name = $"{panelName}Title" };
            titleLabel.AddToClassList("lab-panel__title");
            header.Add(titleLabel);
            var toggle = new Button { name = $"{panelName}Collapse", text = "–", tooltip = "Collapse to a tab" };
            toggle.AddToClassList("lab-panel__collapse");
            toggle.clicked += () => Collapse(panel, !panel.ClassListContains("lab-panel--collapsed"));
            header.Add(toggle);
            panel.Add(header);
            var content = new VisualElement { name = $"{panelName}Content" };
            content.AddToClassList("lab-panel__content");
            panel.Add(content);
            Root.Add(panel);
            return panel;
        }

        private static VisualElement Content(VisualElement panel) => panel.Q(name: $"{panel.name}Content");

        /// <summary>Collapses a panel to the tab at its edge, or opens it.</summary>
        public void Collapse(VisualElement panel, bool collapsed)
        {
            if (panel == null) return;
            panel.EnableInClassList("lab-panel--collapsed", collapsed);
            var content = Content(panel);
            if (content != null) content.style.display = collapsed ? DisplayStyle.None : DisplayStyle.Flex;
            if (panel.Q<Button>(name: $"{panel.name}Collapse") is { } toggle)
            {
                toggle.text = collapsed ? "+" : "–";
                toggle.tooltip = collapsed ? "Open" : "Collapse to a tab";
            }
        }

        /// <summary>A panel by its name: Library, Selected, Mode or Log.</summary>
        public VisualElement PanelNamed(string panelName) => Root?.Q(name: panelName);

        private void BuildLibrary()
        {
            var content = Content(library);
            libraryName = new Label { name = "LibraryName" };
            libraryName.AddToClassList("lab-muted");
            content.Add(libraryName);
            search = new TextField { name = "Search" };
            search.AddToClassList("lab-search");
            search.RegisterValueChangedCallback(_ => DrawLibrary());
            content.Add(search);
            var refresh = new Button(() =>
            {
                lab?.Library?.Refresh();
                DrawLibrary();
                DrawModeChoices();
            }) { name = "Refresh", text = "Refresh", tooltip = "List the project's assets again" };
            content.Add(refresh);
            libraryList = new ScrollView { name = "LibraryList" };
            libraryList.AddToClassList("lab-list");
            content.Add(libraryList);
        }

        private void BuildMode()
        {
            var content = Content(mode);

            var modes = Row(content, "Modes");
            foreach (LabMode each in Enum.GetValues(typeof(LabMode)))
            {
                var kind = each;
                modes.Add(new Button(() => lab?.SetMode(kind)) { name = $"Mode{kind}", text = kind.ToString() });
            }

            // The entity and the side what's put down is of, which the dummies and the battle share
            dummyEntity = new DropdownField("Entity", new List<string>(), 0) { name = "DummyEntity" };
            content.Add(dummyEntity);
            dummySide = new DropdownField("Side", LabSides.All.Select(side => side.Name).ToList(), 1) { name = "DummySide" };
            content.Add(dummySide);

            dummySection = new VisualElement { name = "DummySection" };
            content.Add(dummySection);
            Heading(dummySection, "Test dummies");
            var dummies = Row(dummySection, "DummyButtons");
            dummies.Add(new Button(AddDummy) { name = "AddDummy", text = "Add", tooltip = "Put a dummy of the entity down, on the side" });
            dummies.Add(new Button(() => lab?.ResetDummies()) { name = "ResetDummies", text = "Reset", tooltip = "Each dummy back where it was put, at full" });
            dummies.Add(new Button(() => lab?.Clear()) { name = "ClearUnits", text = "Clear", tooltip = "Take every unit out" });

            // The author's own unit, in either mode (LAB-11)
            unitSection = new VisualElement { name = "UnitSection" };
            content.Add(unitSection);
            Heading(unitSection, "Your unit");
            var own = Row(unitSection, "UnitButtons");
            own.Add(new Button(PutDownOwn) { name = "PutDownOwn", text = "Put down",
                tooltip = "A unit of the entity, on the side, that you control: right-click to move or attack, Q W E R to cast, S to stop" });
            takeControl = new Button(() => { if (lab) lab.TakeControl(lab.SelectedUnit); }) { name = "TakeControl", text = "Take control",
                tooltip = "Control the selected unit; a bot stops fighting until you let it go" };
            own.Add(takeControl);
            releaseControl = new Button(() => lab?.ReleaseControl()) { name = "ReleaseControl", text = "Release", tooltip = "Let your unit go" };
            own.Add(releaseControl);
            followUnit = new Button(() => { if (lab) lab.Follow(lab.ControlledUnit); }) { name = "FollowUnit", text = "Follow",
                tooltip = "The camera follows your unit" };
            own.Add(followUnit);
            controlLine = new Label { name = "ControlLine" };
            controlLine.AddToClassList("lab-muted");
            unitSection.Add(controlLine);

            battleSection = new VisualElement { name = "BattleSection" };
            content.Add(battleSection);
            Heading(battleSection, "Battle");
            botLevel = new SliderInt("Level", 1, 30) { name = "BotLevel", value = 1, showInputField = true };
            battleSection.Add(botLevel);
            var bots = Row(battleSection, "BotButtons");
            bots.Add(new Button(AddBot) { name = "AddBot", text = "Add bot", tooltip = "A bot of the entity, on the side, at the level" });
            bots.Add(new Button(() => lab?.Clear()) { name = "ClearBattle", text = "Clear", tooltip = "Take every bot out" });
            perSide = new SliderInt("A side", 1, 8) { name = "PerSide", value = 4, showInputField = true };
            battleSection.Add(perSide);
            battleSection.Add(new Button(() => lab?.QuickBattle(perSide.value)) { name = "QuickBattle", text = "Quick battle",
                tooltip = "Blue against Red, so many a side, of the entity selected in the library, or at random" });
            var fight = Row(battleSection, "FightButtons");
            fight.Add(new Button(() => lab?.Play()) { name = "Play", text = "Play", tooltip = "The bots fight" });
            fight.Add(new Button(() => lab?.ResetBattle()) { name = "ResetBattle", text = "Reset", tooltip = "Every bot back where it started, at full" });
            battleLine = new Label { name = "BattleLine" };
            battleLine.AddToClassList("lab-muted");
            battleSection.Add(battleLine);

            Heading(content, "Camera");
            var view = lab != null ? lab.View : null;
            distance = new Slider("Distance", view ? view.MinDistance : 6f, view ? view.MaxDistance : 90f) { name = "CameraDistance" };
            distance.RegisterValueChangedCallback(evt => { if (lab && lab.View) lab.View.Distance = evt.newValue; });
            content.Add(distance);
            tilt = new Slider("Tilt", 20f, 89f) { name = "CameraTilt" };
            tilt.RegisterValueChangedCallback(evt => { if (lab && lab.View) lab.View.Tilt = evt.newValue; });
            content.Add(tilt);
            content.Add(new Button(() => lab?.View?.ResetView()) { name = "ResetCamera", text = "Reset view" });

            Heading(content, "Time");
            var time = Row(content, "TimeButtons");
            pause = new Button(() =>
            {
                if (lab == null) return;
                lab.Paused = !lab.Paused;
                MarkDirty();
            }) { name = "Pause", text = "Pause" };
            time.Add(pause);
            time.Add(new Button(() => lab?.StepFrame()) { name = "StepFrame", text = "Step", tooltip = "A frame of the lab's time, while paused" });
            speed = new Slider("Speed", 0.25f, 4f) { name = "Speed", value = 1f };
            speed.RegisterValueChangedCallback(evt => { if (lab) lab.Speed = evt.newValue; });
            content.Add(speed);
            DrawModeChoices();
        }

        private static VisualElement Row(VisualElement parent, string rowName)
        {
            var row = new VisualElement { name = rowName };
            row.AddToClassList("lab-row");
            parent.Add(row);
            return row;
        }

        private static void Heading(VisualElement parent, string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("lab-heading");
            parent.Add(heading);
        }

        private void AddBot()
        {
            if (lab == null || dummyEntity == null) return;
            int index = dummyEntity.index;
            if (index < 0 || index >= entityChoices.Count) return;
            if (lab.Mode != LabMode.Battle) lab.SetMode(LabMode.Battle);
            lab.AddBot(entityChoices[index], LabSides.Named(dummySide.value), level: botLevel != null ? botLevel.value : 0);
        }

        // A unit of the entity picked, on the side picked, where the camera looks, under the author's control
        private void PutDownOwn()
        {
            if (lab == null || dummyEntity == null) return;
            int index = dummyEntity.index;
            if (index < 0 || index >= entityChoices.Count) return;
            var at = lab.View ? new Vector3(lab.View.Focus.x, 0f, lab.View.Focus.z) : Vector3.zero;
            lab.PutDownOwnUnit(entityChoices[index], LabSides.Named(dummySide.value), at);
        }

        private void AddDummy()
        {
            if (lab == null || dummyEntity == null) return;
            int index = dummyEntity.index;
            if (index < 0 || index >= entityChoices.Count) return;
            var side = LabSides.Named(dummySide.value);
            int count = lab.Units.Count;
            var at = new Vector3((count % 5 - 2) * 3f, 0f, 4f + count / 5 * 3f);
            lab.AddDummy(entityChoices[index], side, at);
        }

        // The entities and sets the dummy's picker offers, as the library has them
        private void DrawModeChoices()
        {
            if (dummyEntity == null) return;
            entityChoices.Clear();
            if (lab?.Library != null)
            {
                entityChoices.AddRange(lab.Library.Assets(LabAssetKind.Entities));
                entityChoices.AddRange(lab.Library.Assets(LabAssetKind.AttributeSets));
            }
            dummyEntity.choices = entityChoices.Select(LabAssets.NameOf).ToList();
            if (dummyEntity.index < 0 && entityChoices.Count > 0) dummyEntity.index = 0;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The library
        // ═══════════════════════════════════════════════════════════════════════════

        private void DrawLibrary()
        {
            if (libraryList == null || lab == null) return;
            libraryName.text = lab.Library?.Name ?? "";
            libraryList.Clear();
            string filter = search?.value?.Trim() ?? "";
            foreach (LabAssetKind kind in Enum.GetValues(typeof(LabAssetKind)))
            {
                var assets = lab.Library?.Assets(kind) ?? Array.Empty<ScriptableObject>();
                var shown = assets.Where(asset => asset && (filter.Length == 0 ||
                    LabAssets.NameOf(asset).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                if (shown.Count == 0) continue;
                var group = new Foldout { name = $"Group{kind}", text = $"{LabAssets.Heading(kind)} ({shown.Count})", value = true };
                group.AddToClassList("lab-group");
                foreach (var asset in shown) group.Add(LibraryRow(kind, asset));
                libraryList.Add(group);
            }
        }

        private VisualElement LibraryRow(LabAssetKind kind, ScriptableObject asset)
        {
            var row = new VisualElement { name = $"Asset {LabAssets.NameOf(asset)}", userData = asset };
            row.AddToClassList("lab-asset");
            var icon = new VisualElement();
            icon.AddToClassList("lab-asset__icon");
            icon.style.backgroundColor = LabAssets.ColorOf(kind);
            row.Add(icon);
            var label = new Label(LabAssets.NameOf(asset));
            label.AddToClassList("lab-asset__name");
            row.Add(label);
            row.tooltip = Describe(asset);
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                pressed = asset;
                pressedAt = evt.position;
                dragging = false;
                row.CapturePointer(evt.pointerId);
            });
            return row;
        }

        private void Moved(PointerMoveEvent evt)
        {
            if (pressed == null) return;
            if (!dragging && ((Vector2)evt.position - pressedAt).magnitude >= DragStart)
            {
                dragging = true;
                ghost.text = LabAssets.NameOf(pressed);
                ghost.style.display = DisplayStyle.Flex;
                hint.text = HintFor(pressed);
                hint.style.display = DisplayStyle.Flex;
            }
            if (!dragging) return;
            ghost.style.left = evt.position.x + 12f;
            ghost.style.top = evt.position.y + 8f;
        }

        private void Released(PointerUpEvent evt)
        {
            if (pressed == null) return;
            var asset = pressed;
            bool wasDragging = dragging;
            pressed = null;
            dragging = false;
            ghost.style.display = DisplayStyle.None;
            hint.style.display = DisplayStyle.None;
            if (evt.target is VisualElement target && target.HasPointerCapture(evt.pointerId)) target.ReleasePointer(evt.pointerId);
            if (lab == null) return;

            if (!wasDragging)
            {
                lab.Select(asset);
                return;
            }
            var screen = ToScreen(evt.position);
            if (IsOverPanel(screen)) return;
            lab.Drop(asset, screen, LabSides.Named(dummySide?.value));
        }

        // What dropping an asset of its kind does, as the hint says while it's dragged
        private static string HintFor(ScriptableObject asset) => asset switch
        {
            GameplayEffect => "Drop on a unit: it lands from the selected unit, or the lab",
            Ability => "Drop on a unit or the ground: the selected unit, or the lab, casts it there",
            Item => "Drop on a unit to give it",
            EntityIdentity or AttributeSet => "Drop on the ground to put a dummy down",
            _ => "This kind can't be dropped",
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // Where the pointer is
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Whether a point of the screen, in pixels from its bottom left, is over one of the panels.</summary>
        public bool IsOverPanel(Vector2 screen)
        {
            var panel = Root?.panel;
            if (panel == null) return false;
            var at = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = panel.Pick(at);
            for (var each = picked; each != null; each = each.parent)
                if (each.ClassListContains("lab-panel")) return true;
            return false;
        }

        // A panel's point to the screen's: the panel is at a pixel's scale, its y down from the top
        private static Vector2 ToScreen(Vector2 panelPoint) => new(panelPoint.x, Screen.height - panelPoint.y);

        // ═══════════════════════════════════════════════════════════════════════════
        // Drawing
        // ═══════════════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (Root == null || lab == null) return;
            if (dirty)
            {
                dirty = false;
                Redraw();
            }
            else if (ForgeTime.UnscaledTime >= nextRedraw)
            {
                nextRedraw = ForgeTime.UnscaledTime + RedrawEvery;
                DrawSelected();
                DrawBattleLine();
                DrawControlLine();
            }
            DrawBars();
        }

        /// <summary>Draws every panel again from what the lab holds.</summary>
        public void Redraw()
        {
            if (Root == null || lab == null) return;
            DrawLibrary();
            DrawModeChoices();
            DrawSelected();
            if (pause != null) pause.text = lab.Paused ? "Play" : "Pause";
            if (dummySection != null) dummySection.style.display = lab.Mode == LabMode.Dummies ? DisplayStyle.Flex : DisplayStyle.None;
            if (battleSection != null) battleSection.style.display = lab.Mode == LabMode.Battle ? DisplayStyle.Flex : DisplayStyle.None;
            DrawBattleLine();
            DrawControlLine();
            if (lab.View)
            {
                distance?.SetValueWithoutNotify(lab.View.Distance);
                tilt?.SetValueWithoutNotify(lab.View.Tilt);
            }
            speed?.SetValueWithoutNotify(lab.Speed);
        }

        // Whom the author controls, and what each of the section's buttons can do now
        private void DrawControlLine()
        {
            if (controlLine == null || lab == null) return;
            var unit = lab.ControlledUnit;
            controlLine.text = unit
                ? $"You control {unit.name}: right-click to move or attack, Q W E R to cast, S to stop"
                : "No unit of your own: put one down, or take control of the one selected";
            takeControl?.SetEnabled(lab.SelectedUnit && lab.SelectedUnit != unit);
            releaseControl?.SetEnabled(unit);
            followUnit?.SetEnabled(unit);
        }

        // Who stands on each side, and who won
        private void DrawBattleLine()
        {
            if (battleLine == null || lab == null) return;
            if (lab.Ended) battleLine.text = lab.Winner.HasValue ? $"{lab.Winner.Value.Name} wins" : "No one stands";
            else if (lab.Seats.Count == 0) battleLine.text = "No bots yet";
            else
            {
                var standing = lab.Seats.Where(seat => seat.Unit && seat.Unit.Alive).GroupBy(seat => seat.Side.Name)
                    .Select(side => $"{side.Key} {side.Count()}");
                battleLine.text = (lab.Fighting ? "Fighting · " : "") + string.Join(" · ", standing);
            }
        }

        // The middle dock: the asset selected, level by level, or the unit, as it is now
        private void DrawSelected()
        {
            if (selectedBody == null || lab == null) return;
            bool any = lab.SelectedUnit || lab.SelectedAsset;
            selected.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
            if (!any) return;
            selectedBody.Clear();
            if (lab.SelectedUnit) DrawUnit(lab.SelectedUnit);
            else DrawAsset(lab.SelectedAsset);
        }

        private void DrawAsset(ScriptableObject asset)
        {
            var title = selected.Q<Label>(name: "SelectedTitle");
            if (title != null) title.text = LabAssets.NameOf(asset);
            if (LabAssets.TryKindOf(asset, out var kind)) Line(selectedBody, LabAssets.Heading(kind), "lab-muted");
            if (CreatorLab.OpenInForge != null)
                selectedBody.Add(new Button(() => CreatorLab.OpenInForge?.Invoke(asset)) { name = "OpenInForge", text = "Open in The Forge" });
            foreach (var line in LabSummary.Of(asset)) Line(selectedBody, line);
        }

        private void DrawUnit(LabUnit unit)
        {
            var title = selected.Q<Label>(name: "SelectedTitle");
            if (title != null) title.text = unit.Name;
            Line(selectedBody, $"{unit.Side.Name} · level {unit.LevelOf(unit.Identity)}{(unit.Alive ? "" : " · down")}", "lab-muted");

            var buttons = Row(selectedBody, "UnitButtons");
            buttons.Add(new Button(() => { lab.Select((LabUnit)null); }) { name = "Deselect", text = "Deselect" });
            buttons.Add(new Button(() => lab.RemoveUnit(unit)) { name = "RemoveUnit", text = "Remove" });

            Heading(selectedBody, "Attributes");
            foreach (var line in LabSummary.AttributesOf(unit)) Line(selectedBody, line);
            Heading(selectedBody, "Tags");
            var tags = unit.Gas?.GetTagCache()?.GetAppliedTags();
            Line(selectedBody, tags is { Count: > 0 } ? string.Join(", ", tags.Select(tag => tag.ToString())) : "None");
            Heading(selectedBody, "Effects");
            foreach (var line in LabSummary.EffectsOf(unit)) Line(selectedBody, line);
            Heading(selectedBody, "Abilities");
            foreach (var ability in unit.Known())
            {
                var row = Row(selectedBody, $"Ability {ability.GetName()}");
                float cooldown = unit.CooldownLeft(ability);
                row.Add(new Label($"{ability.GetName()} · level {unit.LevelOf(ability)}{(cooldown > 0f ? $" · {cooldown:0.0}s" : "")}"));
                row.Add(new Button(() => lab.SetAbilityLevel(ability, unit, unit.LevelOf(ability) - 1)) { text = "−", tooltip = "A level less" });
                row.Add(new Button(() => lab.SetAbilityLevel(ability, unit, unit.LevelOf(ability) + 1)) { text = "+", tooltip = "A level more" });
                row.Add(new Button(() => lab.Forget(ability, unit)) { text = "×", tooltip = "Take it" });
            }
            var abilities = lab.Library?.Assets(LabAssetKind.Abilities) ?? Array.Empty<ScriptableObject>();
            if (abilities.Count > 0)
            {
                var teach = Row(selectedBody, "Teach");
                teachChoice = new DropdownField(abilities.Select(LabAssets.NameOf).ToList(), 0) { name = "TeachChoice" };
                teach.Add(teachChoice);
                teach.Add(new Button(() => { if (teachChoice.index >= 0) lab.Teach(abilities[teachChoice.index] as Ability, unit); })
                    { name = "TeachAbility", text = "Teach", tooltip = "The unit learns the ability" });
            }
            Heading(selectedBody, "Items");
            foreach (var item in unit.Items())
            {
                var row = Row(selectedBody, $"Item {item.GetName()}");
                row.Add(new Label($"{item.GetName()} · level {unit.LevelOf(item)}"));
                row.Add(new Button(() => lab.TakeItem(item, unit)) { text = "×", tooltip = "Take it" });
            }
            var items = lab.Library?.Assets(LabAssetKind.Items) ?? Array.Empty<ScriptableObject>();
            if (items.Count > 0)
            {
                var give = Row(selectedBody, "Give");
                giveChoice = new DropdownField(items.Select(LabAssets.NameOf).ToList(), 0) { name = "GiveChoice" };
                give.Add(giveChoice);
                give.Add(new Button(() => { if (giveChoice.index >= 0) lab.GiveItem(items[giveChoice.index] as Item, unit); })
                    { name = "GiveItem", text = "Give", tooltip = "The unit takes the item, equipped" });
            }
            if (unit.TryGetComponent<LabDummy>(out var dummy))
            {
                Heading(selectedBody, "What it took");
                Line(selectedBody, $"{Number(dummy.DamagePerSecond)} a second over {Number(dummy.Window)} s");
                foreach (var blow in Enumerable.Reverse(dummy.History))
                    Line(selectedBody, $"{Number(blow.Amount)}{(blow.Types.Count > 0 ? " " + string.Join(", ", blow.Types.Select(t => t.ToString())) : "")}{(string.IsNullOrEmpty(blow.From) ? "" : $" · {blow.From}")}");
            }
        }

        private static void Line(VisualElement parent, string text, string kind = null)
        {
            var line = new Label(text);
            line.AddToClassList("lab-line");
            if (kind != null) line.AddToClassList(kind);
            parent.Add(line);
        }

        // Each unit's name and health over it, with a dummy's take under
        private void DrawBars()
        {
            var view = lab.View;
            var panel = Root.panel;
            if (!view || panel == null) return;
            foreach (var gone in bars.Keys.Where(unit => !unit).ToList())
            {
                bars[gone].root.RemoveFromHierarchy();
                bars.Remove(gone);
            }
            foreach (var unit in lab.Units)
            {
                if (!unit) continue;
                if (!bars.TryGetValue(unit, out var bar))
                {
                    var root = new VisualElement { name = $"Bar {unit.Name}", pickingMode = PickingMode.Ignore };
                    root.AddToClassList("lab-bar");
                    var track = new VisualElement { pickingMode = PickingMode.Ignore };
                    track.AddToClassList("lab-bar__track");
                    var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                    fill.AddToClassList("lab-bar__fill");
                    fill.style.backgroundColor = unit.Side.Color;
                    track.Add(fill);
                    var line = new Label { pickingMode = PickingMode.Ignore };
                    line.AddToClassList("lab-bar__line");
                    root.Add(line);
                    root.Add(track);
                    overlay.Add(root);
                    bars[unit] = bar = (root, fill, line);
                }
                var screen = view.Camera.WorldToScreenPoint(unit.transform.position + Vector3.up * 2.5f);
                bool show = screen.z > 0f && unit.Alive;
                bar.root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) continue;
                var at = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
                bar.root.style.left = at.x - 50f;
                bar.root.style.top = at.y - 28f;
                bar.fill.style.width = Length.Percent(unit.HealthShare * 100f);
                string take = unit.TryGetComponent<LabDummy>(out var dummy) && dummy.DamagePerSecond > 0f
                    ? $" · {Number(dummy.DamagePerSecond)}/s" : "";
                bar.line.text = unit.Name + take;
                bar.root.EnableInClassList("lab-bar--selected", unit == lab.SelectedUnit);
            }
        }

        private static string Describe(ScriptableObject asset) => asset switch
        {
            Ability ability => ability.GetDescription(),
            GameplayEffect effect => effect.GetDescription(),
            Item item => item.GetDescription(),
            EntityIdentity entity => entity.GetDescription(),
            _ => "",
        };

        internal static string Number(float value) =>
            Mathf.Abs(value - Mathf.Round(value)) < 0.05f ? Mathf.Round(value).ToString(CultureInfo.InvariantCulture) : value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
