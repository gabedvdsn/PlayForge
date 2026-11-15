using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// The lab's bottom panel (LAB-12 to LAB-16, Decision 88), a page for each way it shows its working: the effect journal,
    /// the combat log with each hit's steps, the stops with Step Frame and Step Tick, the watches drawn as lines over the
    /// last minute, and ForgeLog's messages by system, each filtered to the selected unit where asked.
    /// </summary>
    internal sealed class LabBottomPanel
    {
        /// <summary>The pages, in their tabs' order.</summary>
        public static readonly string[] Pages = { "Journal", "Combat", "Stops", "Watches", "Log" };

        private const int MostRows = 80;

        private readonly CreatorLab lab;
        private readonly VisualElement root;
        private readonly Dictionary<string, VisualElement> pages = new();
        private readonly Dictionary<string, Button> tabs = new();
        private string shown = Pages[0];

        private ScrollView journalList, combatList, stopList, watchList, logList;
        private Toggle journalSelected, logSelected;
        private Label journalDetail, combatNote, stoppedLine, watchNote;
        private DropdownField stopKind, stopThing, watchKind, watchThing, logSystem, logLevel;
        private Toggle stopOnSelected, watchBase;
        private FloatField stopValue;
        private LabWatchChart chart;
        private int journalVersion = -1, combatWritten = -1, logWritten = -1, stopsShown = -1, watchesShown = -1;
        private LabUnit combatUnit;
        private readonly List<ScriptableObject> stopChoices = new(), watchChoices = new();
        private readonly List<Tag> stopTags = new(), watchTags = new();

        public LabBottomPanel(CreatorLab lab, VisualElement root)
        {
            this.lab = lab;
            this.root = root;
            Build();
        }

        /// <summary>The page shown now.</summary>
        public string Shown => shown;

        /// <summary>Shows a page by its name: Journal, Combat, Stops, Watches or Log.</summary>
        public void Show(string page)
        {
            if (!pages.ContainsKey(page)) return;
            shown = page;
            foreach (var pair in pages) pair.Value.style.display = pair.Key == page ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var pair in tabs) pair.Value.EnableInClassList("lab-tab--shown", pair.Key == page);
            journalVersion = combatWritten = logWritten = stopsShown = watchesShown = -1;
            Refresh();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Building
        // ═══════════════════════════════════════════════════════════════════════════

        private void Build()
        {
            var bar = new VisualElement { name = "LogTabs" };
            bar.AddToClassList("lab-row");
            bar.AddToClassList("lab-tabs");
            root.Add(bar);
            foreach (var page in Pages)
            {
                var tab = new Button(() => Show(page)) { name = $"Tab_{page}", text = page };
                tab.AddToClassList("lab-tab");
                bar.Add(tab);
                tabs[page] = tab;
                var element = new VisualElement { name = $"{page}Page" };
                element.AddToClassList("lab-page");
                root.Add(element);
                pages[page] = element;
            }
            BuildJournal(pages["Journal"]);
            BuildCombat(pages["Combat"]);
            BuildStops(pages["Stops"]);
            BuildWatches(pages["Watches"]);
            BuildLog(pages["Log"]);
            Show(Pages[0]);
        }

        private static ScrollView List(VisualElement parent, string listName)
        {
            var list = new ScrollView { name = listName };
            list.AddToClassList("lab-list");
            parent.Add(list);
            return list;
        }

        private static VisualElement Row(VisualElement parent, string rowName)
        {
            var row = new VisualElement { name = rowName };
            row.AddToClassList("lab-row");
            parent.Add(row);
            return row;
        }

        private static Label Muted(VisualElement parent, string labelName)
        {
            var label = new Label { name = labelName };
            label.AddToClassList("lab-muted");
            label.AddToClassList("lab-line");
            parent.Add(label);
            return label;
        }

        private static Label Line(string text, string tooltip = null)
        {
            var label = new Label(text) { tooltip = tooltip };
            label.AddToClassList("lab-line");
            return label;
        }

        // The journal: each entry, newest first, on the selected unit or any; a click shows its parts
        private void BuildJournal(VisualElement page)
        {
            var row = Row(page, "JournalControls");
            journalSelected = new Toggle("Selected unit") { name = "JournalSelected", value = true,
                tooltip = "Only what happened on the selected unit; off, on every unit" };
            journalSelected.RegisterValueChangedCallback(_ => journalVersion = -1);
            row.Add(journalSelected);
            row.Add(new Button(() => { lab.Showing?.Journal.Clear(); Refresh(); }) { name = "JournalClear", text = "Clear" });
            journalList = List(page, "JournalList");
            journalDetail = Muted(page, "JournalDetail");
        }

        // The combat log of the selected unit, newest first, with each hit's steps
        private void BuildCombat(VisualElement page)
        {
            combatNote = Muted(page, "CombatNote");
            combatList = List(page, "CombatList");
        }

        // The stops, Step Frame and Step Tick
        private void BuildStops(VisualElement page)
        {
            stoppedLine = Muted(page, "StoppedLine");
            var time = Row(page, "StopsTime");
            time.Add(new Button(() =>
            {
                lab.Showing?.Resume();
                lab.Paused = false;
                Refresh();
            }) { name = "StopsGo", text = "Go", tooltip = "Play on, the stops still watching" });
            time.Add(new Button(() => { lab.StepFrame(); Refresh(); }) { name = "StopsStepFrame", text = "Step Frame",
                tooltip = "A frame of the lab's time, while paused" });
            time.Add(new Button(() => { lab.Showing?.StepTick(); Refresh(); }) { name = "StopsStepTick", text = "Step Tick",
                tooltip = "On until an effect ticks on the selected unit, or on any, up to ten seconds" });

            var add = Row(page, "StopAdd");
            stopKind = new DropdownField("Stop as", Enum.GetNames(typeof(ELabStopKind)).Select(Spaced).ToList(), 0) { name = "StopKind" };
            stopKind.RegisterValueChangedCallback(_ => DrawStopThings());
            add.Add(stopKind);
            stopThing = new DropdownField(new List<string>(), 0) { name = "StopThing" };
            add.Add(stopThing);
            stopValue = new FloatField { name = "StopValue", tooltip = "The value the attribute crosses" };
            add.Add(stopValue);
            stopOnSelected = new Toggle("Selected unit") { name = "StopOnSelected", value = true,
                tooltip = "On the selected unit alone; off, on any" };
            add.Add(stopOnSelected);
            add.Add(new Button(AddStop) { name = "StopAddButton", text = "Add" });
            stopList = List(page, "StopList");
            DrawStopThings();
        }

        // The watches, and their lines
        private void BuildWatches(VisualElement page)
        {
            var add = Row(page, "WatchAdd");
            watchKind = new DropdownField("Watch", Enum.GetNames(typeof(ELabWatchKind)).Select(Spaced).ToList(), 0) { name = "WatchKind" };
            watchKind.RegisterValueChangedCallback(_ => DrawWatchThings());
            add.Add(watchKind);
            watchThing = new DropdownField(new List<string>(), 0) { name = "WatchThing" };
            add.Add(watchThing);
            watchBase = new Toggle("Base") { name = "WatchBase", tooltip = "An attribute's base, not its current value" };
            add.Add(watchBase);
            add.Add(new Button(PinWatch) { name = "WatchPin", text = "Watch", tooltip = "Pin it, on the selected unit" });
            watchNote = Muted(page, "WatchNote");
            chart = new LabWatchChart(() => lab.Showing?.Watches, () => lab.Elapsed) { name = "WatchChart" };
            page.Add(chart);
            watchList = List(page, "WatchList");
            DrawWatchThings();
        }

        // ForgeLog's messages, a system's level set here, on the selected unit or any
        private void BuildLog(VisualElement page)
        {
            var row = Row(page, "LogControls");
            logSystem = new DropdownField("System", Enum.GetNames(typeof(ForgeLog.ESystem)).ToList(), 0) { name = "LogSystem" };
            logSystem.RegisterValueChangedCallback(_ => DrawLogLevel());
            row.Add(logSystem);
            var levels = new List<string> { "As all" };
            levels.AddRange(Enum.GetNames(typeof(ForgeLog.ELevel)));
            logLevel = new DropdownField("Level", levels, 0) { name = "LogLevel",
                tooltip = "The system's own level, which the console keeps too; As all takes the Logging preference's" };
            logLevel.RegisterValueChangedCallback(evt =>
            {
                if (!Enum.TryParse(logSystem.value, out ForgeLog.ESystem system)) return;
                ForgeLog.SetLevel(system, Enum.TryParse(evt.newValue, out ForgeLog.ELevel level) ? level : null);
            });
            row.Add(logLevel);
            logSelected = new Toggle("Selected unit") { name = "LogSelected", value = false,
                tooltip = "Only the messages about the selected unit; off, every message" };
            logSelected.RegisterValueChangedCallback(_ => logWritten = -1);
            row.Add(logSelected);
            row.Add(new Button(() => { lab.Showing?.Log.Clear(); Refresh(); }) { name = "LogClear", text = "Clear" });
            logList = List(page, "LogList");
            DrawLogLevel();
        }

        // "AttributeCrosses" reads "Attribute crosses"
        private static string Spaced(string name)
        {
            var text = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i])) text.Append(' ').Append(char.ToLowerInvariant(name[i]));
                else text.Append(name[i]);
            }
            return text.ToString();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Drawing, the page shown
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Draws the page shown again, where what it shows has changed.</summary>
        public void Refresh()
        {
            if (lab == null || lab.Showing == null) return;
            switch (shown)
            {
                case "Journal": DrawJournal(); break;
                case "Combat": DrawCombat(); break;
                case "Stops": DrawStops(); break;
                case "Watches": DrawWatches(); break;
                default: DrawLog(); break;
            }
        }

        private void DrawJournal()
        {
            var journal = lab.Showing.Journal;
            int version = journal.Version * 31 + (lab.SelectedUnit ? lab.SelectedUnit.GetInstanceID() : 0);
            if (version == journalVersion) return;
            journalVersion = version;
            journalList.Clear();
            var on = journalSelected.value && lab.SelectedUnit ? lab.SelectedUnit.Gas : null;
            int shownRows = 0;
            for (int i = journal.Entries.Count - 1; i >= 0 && shownRows < MostRows; i--)
            {
                var entry = journal.Entries[i];
                if (on != null && !ReferenceEquals(entry.Entity, on)) continue;
                var row = Line($"{Time(entry.Time)}  {entry}");
                row.AddToClassList("lab-entry");
                var kept = entry;
                row.RegisterCallback<ClickEvent>(_ => journalDetail.text = Detail(kept));
                journalList.Add(row);
                shownRows++;
            }
            if (shownRows == 0) journalList.Add(Line(journalSelected.value && !lab.SelectedUnit
                ? "Select a unit to see its journal, or turn Selected unit off."
                : "Nothing yet: drop an effect on a unit."));
        }

        // What an entry holds beyond its line: its number's parts, its breakdown, or the actions its worker queued
        private static string Detail(EffectJournalEntry entry) => entry.Kind switch
        {
            EEffectJournalKind.Applied => entry.Trace != null ? entry.Trace.ToString() : entry.ToString(),
            EEffectJournalKind.Impact => string.IsNullOrEmpty(entry.Breakdown) ? entry.ToString() : $"{entry}\n{entry.Breakdown}",
            EEffectJournalKind.Worker => entry.Actions.Count == 0 ? $"{entry}\nIt queued nothing."
                : $"{entry}\n{string.Join("\n", entry.Actions.Select(action => $"  {action.Description}: {action.State.ToString().ToLowerInvariant()}"))}",
            _ => entry.ToString(),
        };

        private void DrawCombat()
        {
            var unit = lab.SelectedUnit;
            var log = unit && unit.Gas != null ? unit.Gas.GetCombatLog() : null;
            int written = log?.Written ?? -2;
            if (unit == combatUnit && written == combatWritten) return;
            combatUnit = unit;
            combatWritten = written;
            combatList.Clear();
            if (log == null)
            {
                combatNote.text = "Select a unit to see its combat log.";
                return;
            }
            combatNote.text = ForgeCombatLog.Keeps
                ? $"{unit.Name}'s combat log, newest first"
                : "The project's combat log keeps nothing: set its Keep in Project Settings → PlayForge → Combat Log.";
            var entries = log.Entries;
            for (int i = entries.Count - 1; i >= 0 && entries.Count - i <= MostRows; i--)
            {
                var entry = entries[i];
                combatList.Add(Line($"{Time(entry.Time)}  {entry}"));
                string steps = entry.Kind == ECombatLogKind.Event ? "" : entry.StepsText;
                string breakdown = entry.BreakdownText;
                if (!string.IsNullOrEmpty(breakdown)) steps = $"{breakdown}; {steps}";
                if (string.IsNullOrEmpty(steps)) continue;
                var detail = Line($"    {steps}");
                detail.AddToClassList("lab-muted");
                combatList.Add(detail);
            }
        }

        private void DrawStops()
        {
            var showing = lab.Showing;
            stoppedLine.text = showing.StoppedBy != null
                ? $"Stopped: {showing.StoppedFor}"
                : lab.Paused ? "Paused." : "Running; the lab pauses as a stop fires.";
            int count = showing.Stops.Count * 7 + (showing.StoppedBy != null ? 1 : 0);
            if (count == stopsShown) return;
            stopsShown = count;
            stopList.Clear();
            foreach (var stop in showing.Stops)
            {
                var row = Row(stopList, "Stop");
                var on = new Toggle { value = stop.Enabled, tooltip = "Watch for it" };
                var kept = stop;
                on.RegisterValueChangedCallback(evt => kept.Enabled = evt.newValue);
                row.Add(on);
                var line = Line(stop.ToString());
                if (stop == showing.StoppedBy) line.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(line);
                row.Add(new Button(() => { showing.Remove(kept); stopsShown = -1; Refresh(); }) { text = "Remove" });
            }
            if (showing.Stops.Count == 0) stopList.Add(Line("No stops: add one above."));
        }

        private void DrawWatches()
        {
            var showing = lab.Showing;
            watchNote.text = lab.SelectedUnit ? $"Pins on {lab.SelectedUnit.Name}" : "Select a unit to pin a watch on it.";
            chart.MarkDirtyRepaint();
            int count = showing.Watches.Count;
            if (count != watchesShown)
            {
                watchesShown = count;
                watchList.Clear();
                for (int i = 0; i < showing.Watches.Count; i++)
                {
                    var watch = showing.Watches[i];
                    var row = Row(watchList, "Watch");
                    var swatch = new VisualElement();
                    swatch.AddToClassList("lab-swatch");
                    swatch.style.backgroundColor = LabWatchChart.ColorOf(i);
                    row.Add(swatch);
                    row.Add(Line(watch.ToString()));
                    var value = Line("");
                    value.name = "WatchValue";
                    row.Add(value);
                    var kept = watch;
                    row.Add(new Button(() => { showing.Unpin(kept); Refresh(); }) { text = "Unpin" });
                }
                if (count == 0) watchList.Add(Line("No watches: pick one above, with a unit selected."));
            }
            var values = watchList.Query<Label>(name: "WatchValue").ToList();
            for (int i = 0; i < values.Count && i < showing.Watches.Count; i++)
                values[i].text = Number(showing.Watches[i].Latest);
        }

        private void DrawLog()
        {
            var log = lab.Showing.Log;
            int written = lab.Showing.LogWritten * 31 + (logSelected.value && lab.SelectedUnit ? lab.SelectedUnit.GetInstanceID() : 0);
            if (written == logWritten) return;
            logWritten = written;
            logList.Clear();
            var unit = logSelected.value ? lab.SelectedUnit : null;
            int shownRows = 0;
            for (int i = log.Count - 1; i >= 0 && shownRows < MostRows; i--)
            {
                var entry = log[i];
                if (unit && !LabShowing.About(entry, unit)) continue;
                var row = Line($"{Time(entry.Time)}  {entry}");
                if (entry.Level >= ForgeLog.ELevel.Warning) row.style.unityFontStyleAndWeight = FontStyle.Bold;
                logList.Add(row);
                shownRows++;
            }
            if (shownRows == 0) logList.Add(Line("No messages yet."));
        }

        private void DrawLogLevel()
        {
            if (!Enum.TryParse(logSystem.value, out ForgeLog.ESystem system)) return;
            logLevel.SetValueWithoutNotify(ForgeLog.HasOwnLevel(system) ? ForgeLog.LevelOf(system).ToString() : "As all");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Stops and watches, added
        // ═══════════════════════════════════════════════════════════════════════════

        private ELabStopKind StopKind => (ELabStopKind)Mathf.Max(0, stopKind.index);

        // A stop on an effect or an ability can take any; a tag's or an attribute's names its own
        private bool StopTakesAny => StopKind is ELabStopKind.EffectLands or ELabStopKind.EffectRefused or ELabStopKind.WorkerRuns
            or ELabStopKind.AbilityRefused;
        private ELabWatchKind WatchKind => (ELabWatchKind)Mathf.Max(0, watchKind.index);

        // What a stop can name, from the library: effects, abilities, attributes, or the tags its effects grant
        private void DrawStopThings()
        {
            Choices(StopKind switch
            {
                ELabStopKind.EffectLands or ELabStopKind.EffectRefused or ELabStopKind.WorkerRuns => LabAssetKind.Effects,
                ELabStopKind.AbilityRefused => LabAssetKind.Abilities,
                ELabStopKind.AttributeCrosses => LabAssetKind.Attributes,
                _ => (LabAssetKind?)null,
            }, StopKind is ELabStopKind.TagComes or ELabStopKind.TagGoes, StopTakesAny,
                stopThing, stopChoices, stopTags);
            stopValue.style.display = StopKind == ELabStopKind.AttributeCrosses ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void DrawWatchThings()
        {
            Choices(WatchKind switch
            {
                ELabWatchKind.Attribute => LabAssetKind.Attributes,
                ELabWatchKind.EffectStacks or ELabWatchKind.EffectTime => LabAssetKind.Effects,
                ELabWatchKind.Number => LabAssetKind.Scalers,
                _ => (LabAssetKind?)null,
            }, WatchKind == ELabWatchKind.TagCount, false, watchThing, watchChoices, watchTags);
            watchBase.style.display = WatchKind == ELabWatchKind.Attribute ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Choices(LabAssetKind? kind, bool tags, bool any, DropdownField field, List<ScriptableObject> assets, List<Tag> tagChoices)
        {
            assets.Clear();
            tagChoices.Clear();
            var names = new List<string>();
            if (any) names.Add("Any");
            if (kind.HasValue && lab.Library != null)
                foreach (var asset in lab.Library.Assets(kind.Value))
                    if (asset)
                    {
                        assets.Add(asset);
                        names.Add(asset.name);
                    }
            if (tags && lab.Library != null)
                foreach (var effect in lab.Library.Assets(LabAssetKind.Effects).OfType<GameplayEffect>())
                    foreach (var tag in effect.Tags.GrantedTags ?? new List<Tag>())
                        if (tag.IsValid && !tagChoices.Contains(tag))
                        {
                            tagChoices.Add(tag);
                            names.Add(tag.ToString());
                        }
            field.choices = names;
            field.SetValueWithoutNotify(names.Count > 0 ? names[0] : "");
            field.style.display = names.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // The choice a field holds: an asset, a tag, or nothing for Any
        private static (ScriptableObject asset, Tag tag) Picked(DropdownField field, List<ScriptableObject> assets, List<Tag> tags, bool any)
        {
            int index = field.index - (any ? 1 : 0);
            if (index < 0) return (null, default);
            if (index < assets.Count) return (assets[index], default);
            index -= assets.Count;
            return index < tags.Count ? (null, tags[index]) : (null, default);
        }

        private void AddStop()
        {
            var kind = StopKind;
            var (asset, tag) = Picked(stopThing, stopChoices, stopTags, StopTakesAny);
            lab.Showing?.Add(new LabStop
            {
                Kind = kind, OnSelected = stopOnSelected.value, Effect = asset as GameplayEffect, Ability = asset as Ability,
                Attribute = asset as Attribute, Tag = tag, Value = stopValue.value,
            });
            stopsShown = -1;
            Refresh();
        }

        private void PinWatch()
        {
            if (!lab.SelectedUnit) return;
            var (asset, tag) = Picked(watchThing, watchChoices, watchTags, false);
            lab.Showing?.Pin(new LabWatch
            {
                Kind = WatchKind, Unit = lab.SelectedUnit, Attribute = asset as Attribute, Base = watchBase.value,
                Effect = asset as GameplayEffect, Number = asset as ScalerAsset, Tag = tag,
            });
            watchesShown = -1;
            Refresh();
        }

        private static string Time(float seconds) => seconds.ToString("0.00", CultureInfo.InvariantCulture).PadLeft(7);

        private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The watches' lines over the last minute of the lab's time (LAB-15): each its own color and scaled to its own range,
    /// so a Health of hundreds and stacks of a few read side by side; the newest at the right.
    /// </summary>
    internal sealed class LabWatchChart : VisualElement
    {
        private static readonly Color[] Colors =
        {
            new(0.36f, 0.63f, 0.95f), new(0.95f, 0.55f, 0.25f), new(0.45f, 0.80f, 0.45f), new(0.86f, 0.42f, 0.70f),
            new(0.92f, 0.82f, 0.35f), new(0.55f, 0.85f, 0.88f),
        };

        private readonly Func<IReadOnlyList<LabWatch>> watches;
        private readonly Func<float> now;

        public LabWatchChart(Func<IReadOnlyList<LabWatch>> watches, Func<float> now)
        {
            this.watches = watches;
            this.now = now;
            AddToClassList("lab-chart");
            generateVisualContent += Draw;
        }

        public static Color ColorOf(int index) => Colors[index % Colors.Length];

        private void Draw(MeshGenerationContext context)
        {
            var all = watches();
            var rect = contentRect;
            if (all == null || all.Count == 0 || rect.width < 4f || rect.height < 4f) return;
            float end = now();
            float start = end - LabShowing.Window;
            var painter = context.painter2D;
            painter.lineWidth = 1.5f;
            for (int w = 0; w < all.Count; w++)
            {
                var watch = all[w];
                if (watch.Count < 2) continue;
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < watch.Count; i++)
                {
                    float value = watch[i].value;
                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                }
                if (Mathf.Approximately(min, max))
                {
                    min -= 1f;
                    max += 1f;
                }
                painter.strokeColor = ColorOf(w);
                painter.BeginPath();
                for (int i = 0; i < watch.Count; i++)
                {
                    var (time, value) = watch[i];
                    float x = rect.xMin + Mathf.InverseLerp(start, end, time) * rect.width;
                    float y = rect.yMax - Mathf.InverseLerp(min, max, value) * rect.height;
                    if (i == 0) painter.MoveTo(new Vector2(x, y));
                    else painter.LineTo(new Vector2(x, y));
                }
                painter.Stroke();
            }
        }
    }
}
