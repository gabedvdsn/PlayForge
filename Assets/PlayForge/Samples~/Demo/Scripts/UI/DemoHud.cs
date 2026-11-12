using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The demo's screen, in UI Toolkit: the panel on the left that picks who fights and gives items and levels, the HUD
    /// at the bottom that reads the player's hero, bars over each unit, and the words that float up as damage lands and
    /// cues fire. Everything it shows it reads from the GAS: attributes, their item bonus by retention group, effects by
    /// their visibility, cooldowns, levels.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DemoHud : MonoBehaviour
    {
        public static DemoHud Current { get; private set; }

        [Tooltip("The demo's style sheet")]
        public StyleSheet Style;

        [Tooltip("The camera the bars and words follow units through; with none, the main camera")]
        public Camera Camera;

        private UIDocument document;
        private VisualElement root;
        private VisualElement panel;
        private VisualElement overlay;
        private VisualElement youSection;
        private VisualElement foeSection;
        private VisualElement shopSection;
        private VisualElement slots;
        private VisualElement attributes;
        private VisualElement buffs;
        private Label heroLine;
        private Label dummyLine;
        private Label castLine;
        private Bar health;
        private Bar mana;

        private readonly Dictionary<DemoUnit, Bar> unitBars = new();
        private readonly List<Floater> floaters = new();
        private readonly HashSet<IGameplayAbilitySystem> heard = new();
        private DemoGame hooked;

        // ═══════════════════════════════════════════════════════════════════════════
        // Building
        // ═══════════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            Current = this;
            document = GetComponent<UIDocument>();
            Build();
        }

        // The screen, once the document has its root, which it may not have until its own OnEnable has run
        private void Build()
        {
            if (document == null || document.rootVisualElement == null) return;
            root = document.rootVisualElement;
            root.Clear();
            unitBars.Clear();
            floaters.Clear();
            if (Style != null) root.styleSheets.Add(Style);
            root.AddToClassList("demo-root");
            root.pickingMode = PickingMode.Ignore;

            overlay = new VisualElement { name = "Overlay", pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("demo-overlay");
            root.Add(overlay);

            panel = new ScrollView { name = "Panel" };
            panel.AddToClassList("demo-panel");
            root.Add(panel);
            var title = new Label("PlayForge Demo");
            title.AddToClassList("demo-title");
            panel.Add(title);
            youSection = Section(panel, "You");
            foeSection = Section(panel, "Opponent");
            shopSection = Section(panel, "Items");
            var fight = Section(panel, "Fight");
            fight.Add(Button("Reset", "Both back where they started, at full health", () => DemoGame.Current?.ResetFight()));
            var help = new Label("Right-click to move or attack. Q, W, E cast; 1–6 use items; S stops; Esc cancels. A targeted cast waits for a left-click.");
            help.AddToClassList("demo-help");
            fight.Add(help);

            var hud = new VisualElement { name = "Hud" };
            hud.AddToClassList("demo-hud");
            root.Add(hud);
            heroLine = new Label { name = "Hero" };
            heroLine.AddToClassList("demo-hud__hero");
            hud.Add(heroLine);
            health = new Bar(hud, "demo-bar--health");
            mana = new Bar(hud, "demo-bar--mana");
            castLine = new Label { name = "Casting" };
            castLine.AddToClassList("demo-hud__cast");
            hud.Add(castLine);
            slots = new VisualElement { name = "Slots" };
            slots.AddToClassList("demo-slots");
            hud.Add(slots);
            var lower = new VisualElement();
            lower.AddToClassList("demo-hud__lower");
            hud.Add(lower);
            attributes = new VisualElement { name = "Attributes" };
            attributes.AddToClassList("demo-attributes");
            lower.Add(attributes);
            buffs = new VisualElement { name = "Effects" };
            buffs.AddToClassList("demo-buffs");
            lower.Add(buffs);
        }

        private void OnDisable()
        {
            if (hooked != null) hooked.Changed -= Redraw;
            hooked = null;
            root = null;
            if (Current == this) Current = null;
        }

        private void Update()
        {
            if (root == null || root != document.rootVisualElement)
            {
                Build();
                if (root == null) return;
                hooked = null;
            }

            var game = DemoGame.Current;
            if (game != hooked)
            {
                if (hooked != null) hooked.Changed -= Redraw;
                hooked = game;
                if (hooked != null) hooked.Changed += Redraw;
                Redraw();
            }
            if (game == null) return;

            Listen(game.Player);
            Listen(game.Opponent);
            ShowHud(game.Player);
            ShowDummy();
            ShowUnitBars(game);
            ShowFloaters();
        }

        private static VisualElement Section(VisualElement parent, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("demo-section");
            var heading = new Label(title);
            heading.AddToClassList("demo-section__title");
            section.Add(heading);
            var body = new VisualElement();
            body.AddToClassList("demo-section__body");
            section.Add(body);
            parent.Add(section);
            return body;
        }

        private static Button Button(string text, string tip, System.Action click)
        {
            var button = new Button(click) { text = text, tooltip = tip };
            button.AddToClassList("demo-button");
            return button;
        }

        private static VisualElement Row(string label, params VisualElement[] parts)
        {
            var row = new VisualElement();
            row.AddToClassList("demo-row");
            var name = new Label(label);
            name.AddToClassList("demo-row__label");
            row.Add(name);
            foreach (var part in parts) row.Add(part);
            return row;
        }

        // A level with − and + beside it
        private static VisualElement Stepper(string label, int level, int most, System.Action<int> set)
        {
            var value = new Label($"{level}/{most}");
            value.AddToClassList("demo-row__value");
            var down = Button("−", $"{label} down a level", () => set(level - 1));
            var up = Button("+", $"{label} up a level", () => set(level + 1));
            down.SetEnabled(level > 1);
            up.SetEnabled(level < most);
            return Row(label, value, down, up);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The panel
        // ═══════════════════════════════════════════════════════════════════════════

        private void Redraw()
        {
            var game = DemoGame.Current;
            if (game == null || youSection == null) return;
            DrawSeat(youSection, game, game.PlayerSeat, true);
            DrawSeat(foeSection, game, game.OpponentSeat, false);
            DrawShop(game);
        }

        private void DrawSeat(VisualElement body, DemoGame game, DemoSeat seat, bool isPlayer)
        {
            body.Clear();
            var roster = game.Roster;
            var heroNames = new List<string>();
            foreach (var hero in roster.Heroes) heroNames.Add(hero.Identity != null ? hero.Identity.GetName() : "?");

            if (!isPlayer)
            {
                var kinds = new List<string> { "An AI Hero", "The Training Dummy" };
                var kind = new DropdownField(kinds, seat.IsDummy ? 1 : 0) { tooltip = "What you fight" };
                kind.AddToClassList("demo-field");
                kind.RegisterValueChangedCallback(change => game.SetOpponent(change.newValue == kinds[1] ? DemoOpponent.TrainingDummy : DemoOpponent.AIHero));
                body.Add(Row("Fights as", kind));
                if (seat.IsDummy)
                {
                    dummyLine = new Label();
                    dummyLine.AddToClassList("demo-row__value");
                    body.Add(Row("Taken", dummyLine));
                    DrawItems(body, game, seat);
                    return;
                }
                var levels = new List<string> { "Easy", "Normal", "Hard" };
                var difficulty = new DropdownField(levels, (int)game.Difficulty) { tooltip = "How hard the AI hits, which it hands its abilities by Set By Caller" };
                difficulty.AddToClassList("demo-field");
                difficulty.RegisterValueChangedCallback(change => game.SetDifficulty((DemoDifficulty)levels.IndexOf(change.newValue)));
                body.Add(Row("Difficulty", difficulty));
            }

            var pick = new DropdownField(heroNames, Mathf.Clamp(seat.HeroIndex, 0, heroNames.Count - 1)) { tooltip = "The hero this side plays" };
            pick.AddToClassList("demo-field");
            pick.RegisterValueChangedCallback(change => game.SetHero(seat, heroNames.IndexOf(change.newValue)));
            body.Add(Row("Hero", pick));

            var identity = game.IdentityOf(seat);
            if (identity == null) return;
            if (seat.HeroIndex < roster.Heroes.Count && !string.IsNullOrEmpty(roster.Heroes[seat.HeroIndex].Summary))
            {
                var summary = new Label(roster.Heroes[seat.HeroIndex].Summary);
                summary.AddToClassList("demo-help");
                body.Add(summary);
            }
            body.Add(Stepper("Level", seat.Level, Mathf.Max(1, identity.MaxLevel), level => game.SetLevel(seat, level)));
            string[] keys = { "Attack", "Q", "W", "E" };
            for (int slot = 0; slot < identity.StartingAbilities.Count; slot++)
            {
                var ability = identity.StartingAbilities[slot];
                if (ability == null) continue;
                string key = slot < keys.Length ? keys[slot] : "";
                body.Add(Stepper($"{key} {ability.GetName()}", seat.LevelOf(ability), Mathf.Max(1, ability.MaxLevel),
                    level => game.SetAbilityLevel(seat, ability, level)));
            }
            DrawItems(body, game, seat);
        }

        private static void DrawItems(VisualElement body, DemoGame game, DemoSeat seat)
        {
            if (seat.Items.Count == 0) return;
            var heading = new Label("Items");
            heading.AddToClassList("demo-subtitle");
            body.Add(heading);
            for (int i = 0; i < seat.Items.Count; i++)
            {
                var held = seat.Items[i];
                if (held.Item == null) continue;
                var item = held.Item;
                string name = held.Count > 1 ? $"{i + 1}. {item.GetName()} ×{held.Count}" : $"{i + 1}. {item.GetName()}";
                var row = Stepper(name, held.Level, Mathf.Max(1, item.MaxLevel), level => game.SetItemLevel(seat, item, level));
                row.Add(Button("Remove", $"Take {item.GetName()} back", () => game.Take(seat, item)));
                body.Add(row);
            }
        }

        private void DrawShop(DemoGame game)
        {
            shopSection.Clear();
            foreach (var item in game.Roster.Items)
            {
                if (item == null) continue;
                var row = Row(item.GetName(),
                    Button("You", $"Give {item.GetName()} to your hero", () => game.Give(game.PlayerSeat, item)),
                    Button("Foe", $"Give {item.GetName()} to the opponent", () => game.Give(game.OpponentSeat, item)));
                row.tooltip = item.GetDescription();
                shopSection.Add(row);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The HUD
        // ═══════════════════════════════════════════════════════════════════════════

        private void ShowHud(DemoUnit me)
        {
            if (me == null || !me.Alive)
            {
                var seat = DemoGame.Current != null ? DemoGame.Current.PlayerSeat : null;
                heroLine.text = seat != null && seat.DownAt >= 0f
                    ? $"Down. Back in {Mathf.Max(0f, DemoGame.Current.RespawnSeconds - (Time.time - seat.DownAt)):0.0} s"
                    : "";
                return;
            }
            var roster = me.Roster;
            heroLine.text = $"{me.Name}   Level {me.LevelOf(me.Identity)}";
            health.Show(me.Value(roster.Health), me.Base(roster.Health));
            mana.Show(me.Value(roster.Mana), me.Base(roster.Mana));
            ShowCasting(me);
            ShowSlots(me);
            ShowAttributes(me);
            ShowEffects(me);
        }

        private void ShowCasting(DemoUnit me)
        {
            string casting = null;
            for (int slot = 1; slot <= 3 && casting == null; slot++)
                if (me.Casting(me.AbilityAt(slot))) casting = me.AbilityAt(slot).GetName();
            for (int i = 0; i < me.Seat.Items.Count && casting == null; i++)
                if (me.Casting(me.ItemAbilityAt(i))) casting = me.ItemAbilityAt(i).GetName();
            castLine.text = casting != null ? $"{casting}: left-click its target" : "";
        }

        private void ShowSlots(DemoUnit me)
        {
            var wanted = new List<(string key, Ability ability)>();
            string[] keys = { "Q", "W", "E" };
            for (int slot = 1; slot <= 3; slot++) wanted.Add((keys[slot - 1], me.AbilityAt(slot)));
            for (int i = 0; i < me.Seat.Items.Count && i < 6; i++)
                if (me.ItemAbilityAt(i) != null) wanted.Add(((i + 1).ToString(CultureInfo.InvariantCulture), me.ItemAbilityAt(i)));

            while (slots.childCount < wanted.Count) slots.Add(new Slot().Root);
            while (slots.childCount > wanted.Count) slots.RemoveAt(slots.childCount - 1);
            for (int i = 0; i < wanted.Count; i++)
                ((Slot)slots[i].userData).Show(wanted[i].key, wanted[i].ability, me);
        }

        private void ShowAttributes(DemoUnit me)
        {
            var roster = me.Roster;
            while (attributes.childCount < roster.Shown.Count)
            {
                var line = new Label();
                line.AddToClassList("demo-attribute");
                attributes.Add(line);
            }
            var system = me.Gas.GetAttributeSystem();
            for (int i = 0; i < roster.Shown.Count; i++)
            {
                var attribute = roster.Shown[i];
                var line = (Label)attributes[i];
                if (attribute == null) continue;
                float value = me.Value(attribute);
                // The part items give, as the Bonus retention group holds it
                float bonus = system != null && system.TryGetCachedValue(attribute, out CachedAttributeValue cached)
                    ? cached.GetGroupValue(Tags.ADDITIONAL).CurrentValue : 0f;
                string shown = Number(value);
                if (Mathf.Abs(bonus) > 0.0001f) shown += $" (+{Number(bonus)})";
                line.text = $"{attribute.GetAbbreviation()}  {shown}";
                line.tooltip = attribute.GetName();
            }
        }

        private void ShowEffects(DemoUnit me)
        {
            var shown = new List<(GameplayEffect effect, int stacks, float left)>();
            var shelf = me.Gas.GetEffectSystem()?.Shelf;
            if (shelf != null)
            {
                foreach (var container in shelf)
                {
                    var effect = container?.Spec?.Base;
                    // An effect marked Not Visible stays off the bar, as an item's bonus or a cooldown
                    if (effect == null || effect.Definition.Visibility == Tags.NOT_VISIBLE) continue;
                    int stacks = container is AbstractStackingEffectContainer stacking ? stacking.Stacks : 1;
                    int at = shown.FindIndex(each => each.effect == effect);
                    if (at >= 0) shown[at] = (effect, shown[at].stacks + stacks, Mathf.Max(shown[at].left, container.DurationRemaining));
                    else shown.Add((effect, stacks, container.DurationRemaining));
                }
            }
            while (buffs.childCount < shown.Count)
            {
                var chip = new Label();
                chip.AddToClassList("demo-effect");
                buffs.Add(chip);
            }
            while (buffs.childCount > shown.Count) buffs.RemoveAt(buffs.childCount - 1);
            for (int i = 0; i < shown.Count; i++)
            {
                var (effect, stacks, left) = shown[i];
                var chip = (Label)buffs[i];
                string time = left > 0f && left < 999f ? $" {left:0.0}s" : "";
                chip.text = stacks > 1 ? $"{effect.GetName()} ×{stacks}{time}" : $"{effect.GetName()}{time}";
                chip.tooltip = effect.GetDescription();
                chip.style.backgroundImage = effect.GetDefaultIcon() is { } icon ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
            }
        }

        private void ShowDummy()
        {
            var dummy = DemoDummy.Current;
            if (dummyLine == null || dummy == null) return;
            dummyLine.text = $"{Number(dummy.DamagePerSecond)}/s, last {Number(dummy.LastBlow)}";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Over the units
        // ═══════════════════════════════════════════════════════════════════════════

        private void ShowUnitBars(DemoGame game)
        {
            foreach (var unit in new[] { game.Player, game.Opponent })
            {
                if (unit == null) continue;
                if (!unitBars.TryGetValue(unit, out var bar))
                {
                    bar = new Bar(overlay, "demo-bar--unit", unit.Name);
                    unitBars[unit] = bar;
                }
            }
            var gone = new List<DemoUnit>();
            foreach (var pair in unitBars)
            {
                var unit = pair.Key;
                if (unit == null || !unit.Alive)
                {
                    pair.Value.Root.RemoveFromHierarchy();
                    gone.Add(unit);
                    continue;
                }
                pair.Value.Show(unit.Value(unit.Roster.Health), unit.Base(unit.Roster.Health));
                bool hidden = unit.Has(unit.Roster.Invisible) && !unit.IsPlayer;
                pair.Value.Root.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;
                Place(pair.Value.Root, unit.transform.position + Vector3.up * 2.6f);
            }
            foreach (var unit in gone) unitBars.Remove(unit);
        }

        // Damage and healing, as each unit's Health takes it, float up from it
        private void Listen(DemoUnit unit)
        {
            if (unit == null || unit.Gas == null || heard.Contains(unit.Gas)) return;
            heard.Add(unit.Gas);
            var roster = unit.Roster;
            unit.Gas.GetCallbacks().OnImpactReceived += impact =>
            {
                if (unit == null || impact.Attribute == null || !impact.Attribute.Equals(roster.Health)) return;
                float amount = impact.RealImpact.CurrentValue;
                if (Mathf.Abs(amount) < 0.5f) return;
                var kind = KindOf(roster, impact);
                var color = amount > 0f ? new Color(0.45f, 0.95f, 0.5f)
                    : kind == roster.Magic ? new Color(0.55f, 0.7f, 1f)
                    : kind == roster.Pure ? new Color(1f, 0.95f, 0.6f)
                    : new Color(1f, 0.55f, 0.45f);
                Float(unit.transform.position + Vector3.up * 2.1f, Number(Mathf.Abs(amount)), color);
            };
        }

        /// <summary>Floats a word up from a point in the world, as a cue or a blow does.</summary>
        public void Float(Vector3 world, string text, Color color)
        {
            if (overlay == null) return;
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("demo-float");
            label.style.color = color;
            overlay.Add(label);
            var jitter = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.2f, 0.2f));
            floaters.Add(new Floater { Label = label, World = world + jitter, Born = Time.time });
        }

        private void ShowFloaters()
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var floater = floaters[i];
                float age = Time.time - floater.Born;
                if (age > 1.2f)
                {
                    floater.Label.RemoveFromHierarchy();
                    floaters.RemoveAt(i);
                    continue;
                }
                floater.Label.style.opacity = 1f - Mathf.Clamp01((age - 0.6f) / 0.6f);
                Place(floater.Label, floater.World + Vector3.up * age * 1.2f);
            }
        }

        // An element centered over a point in the world
        private void Place(VisualElement element, Vector3 world)
        {
            var eye = Camera != null ? Camera : Camera.main;
            if (eye == null || root.panel == null) return;
            if (Vector3.Dot(eye.transform.forward, world - eye.transform.position) <= 0f)
            {
                element.style.left = -9999f;
                return;
            }
            var at = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, eye);
            float width = element.resolvedStyle.width;
            element.style.left = at.x - (float.IsNaN(width) ? 0f : width / 2f);
            element.style.top = at.y;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // For the rest of the demo
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Whether the pointer is over the panel or the HUD, where a right-click isn't an order.</summary>
        public static bool PointerOverPanel(Vector2 screen)
        {
            var hud = Current;
            if (hud == null || hud.root?.panel == null) return false;
            var at = RuntimePanelUtils.ScreenToPanel(hud.root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = hud.root.panel.Pick(at);
            return picked != null && picked != hud.root && !hud.overlay.Contains(picked);
        }

        /// <summary>The damage type a blow carried, by its impact types: physical, magic or pure.</summary>
        public static Tag KindOf(DemoRoster roster, ImpactData impact) => KindOf(roster, impact.SourcedModifier.Carrier?.GetImpactTypes());

        /// <summary>The damage type of a blow's impact types: physical, magic or pure.</summary>
        public static Tag KindOf(DemoRoster roster, List<Tag> types)
        {
            if (types == null || roster == null) return default;
            if (types.Contains(roster.Magic)) return roster.Magic;
            if (types.Contains(roster.Pure)) return roster.Pure;
            if (types.Contains(roster.Physical)) return roster.Physical;
            return default;
        }

        private static string Number(float value) =>
            Mathf.Abs(value) < 10f && Mathf.Abs(value - Mathf.Round(value)) > 0.001f
                ? value.ToString("0.##", CultureInfo.InvariantCulture)
                : Mathf.Round(value).ToString(CultureInfo.InvariantCulture);

        // ═══════════════════════════════════════════════════════════════════════════
        // Parts
        // ═══════════════════════════════════════════════════════════════════════════

        private sealed class Bar
        {
            public readonly VisualElement Root;
            private readonly VisualElement fill;
            private readonly Label text;

            public Bar(VisualElement parent, string kind, string title = null)
            {
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.AddToClassList("demo-bar");
                Root.AddToClassList(kind);
                if (title != null)
                {
                    var name = new Label(title) { pickingMode = PickingMode.Ignore };
                    name.AddToClassList("demo-bar__title");
                    Root.Add(name);
                }
                var track = new VisualElement { pickingMode = PickingMode.Ignore };
                track.AddToClassList("demo-bar__track");
                fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("demo-bar__fill");
                track.Add(fill);
                text = new Label { pickingMode = PickingMode.Ignore };
                text.AddToClassList("demo-bar__text");
                track.Add(text);
                Root.Add(track);
                parent.Add(Root);
            }

            public void Show(float value, float most)
            {
                float share = most > 0f ? Mathf.Clamp01(value / most) : 0f;
                fill.style.width = Length.Percent(share * 100f);
                text.text = $"{Mathf.CeilToInt(Mathf.Max(0f, value))} / {Mathf.CeilToInt(most)}";
            }
        }

        private sealed class Slot
        {
            public readonly VisualElement Root;
            private readonly Label key;
            private readonly Label name;
            private readonly Label cooldown;

            public Slot()
            {
                Root = new VisualElement { userData = this };
                Root.AddToClassList("demo-slot");
                key = new Label();
                key.AddToClassList("demo-slot__key");
                name = new Label();
                name.AddToClassList("demo-slot__name");
                cooldown = new Label();
                cooldown.AddToClassList("demo-slot__cooldown");
                Root.Add(key);
                Root.Add(name);
                Root.Add(cooldown);
            }

            public void Show(string key, Ability ability, DemoUnit me)
            {
                this.key.text = key;
                name.text = ability != null ? ability.GetName() : "";
                Root.tooltip = ability != null ? $"{ability.GetName()}, level {me.LevelOf(ability)}: {ability.GetDescription()}" : "";
                Root.style.backgroundImage = ability != null && ability.GetDefaultIcon() is { } icon ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
                float left = me.CooldownLeft(ability);
                cooldown.text = left > 0f ? left.ToString("0.0", CultureInfo.InvariantCulture) : "";
                Root.EnableInClassList("demo-slot--cooling", left > 0f);
                Root.EnableInClassList("demo-slot--casting", me.Casting(ability));
            }
        }

        private sealed class Floater
        {
            public Label Label;
            public Vector3 World;
            public float Born;
        }
    }
}
