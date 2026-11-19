using System.Collections.Generic;
using FarEmerald.PlayForge.Lab;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// The Survivors demo's screen, in UI Toolkit: the experience bar and level across the top, the wave and its time,
    /// the kills and the time survived; what the run costs, frame time and how many entities and processes stand; the
    /// hero's health and the abilities and passives it holds, each with its level and an ability's cooldown; the cards as
    /// they're up, taken by a click or a number key; and the run's end, with R to go again. Only the cards and the end
    /// keep clicks: the rest of the screen is the arena's.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SurvivorsHud : MonoBehaviour
    {
        [Tooltip("The demo's style sheet")]
        public StyleSheet Style;

        [Tooltip("The camera the hero's bar follows it through; with none, the main camera")]
        public Camera Camera;

        [Tooltip("Seconds between redraws of the words, the counts and the slots; the bars redraw every frame")]
        [Min(0.02f)] public float RedrawEvery = 0.1f;

        private UIDocument document;
        private VisualElement root;
        private VisualElement experienceFill;
        private Label levelLabel;
        private Label waveLabel;
        private Label timeLabel;
        private Label killsLabel;
        private Label costLabel;
        private Label countsLabel;
        private VisualElement healthFill;
        private Label healthLabel;
        private VisualElement heroBar;
        private VisualElement heroBarFill;
        private VisualElement abilitySlots;
        private Slot dashSlot;
        private VisualElement passiveSlots;
        private Label banner;
        private VisualElement cards;
        private Label cardsTitle;
        private VisualElement cardRow;
        private VisualElement end;
        private Label endStats;
        private readonly List<Slot> abilities = new();
        private readonly List<Slot> passives = new();
        private SurvivorsGame hooked;
        private SurvivorsDraft shown;
        private float redrawAt;
        private float frameTime = 1f / 60f;

        private sealed class Slot
        {
            public VisualElement Box;
            public Label Name;
            public Label Level;
            public VisualElement Shade;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Building
        // ═══════════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();
            Build();
        }

        private void OnDisable()
        {
            if (hooked != null) hooked.Changed -= Redraw;
            hooked = null;
        }

        private void Build()
        {
            if (document == null || document.rootVisualElement == null) return;
            root = document.rootVisualElement;
            root.Clear();
            abilities.Clear();
            passives.Clear();
            if (Style != null) root.styleSheets.Add(Style);
            root.AddToClassList("sv-root");
            root.pickingMode = PickingMode.Ignore;

            // Across the top: the experience toward the next level, and the level
            var top = Add(root, "sv-top", "Top");
            var experience = Add(top, "sv-experience", "Experience");
            experienceFill = Add(experience, "sv-experience__fill");
            levelLabel = Text(experience, "sv-experience__level", "Level 1");

            // Under it, left: the wave, its time, the kills and the time survived; right: what the run costs
            var status = Add(root, "sv-status", "Status");
            var run = Add(status, "sv-run");
            waveLabel = Text(run, "sv-run__wave", "Wave 1");
            timeLabel = Text(run, "sv-run__time", "");
            killsLabel = Text(run, "sv-run__kills", "");
            var cost = Add(status, "sv-cost", "Cost");
            costLabel = Text(cost, "sv-cost__frame", "");
            countsLabel = Text(cost, "sv-cost__counts", "");

            banner = Text(root, "sv-banner", "");
            banner.style.display = DisplayStyle.None;

            heroBar = Add(root, "sv-herobar", "HeroBar");
            heroBarFill = Add(heroBar, "sv-herobar__fill");

            // At the bottom: health, then the abilities and the passives held
            var dock = Add(root, "sv-dock", "Dock");
            var health = Add(dock, "sv-health");
            healthFill = Add(health, "sv-health__fill");
            healthLabel = Text(health, "sv-health__label", "");
            var held = Add(dock, "sv-held");
            // The dash, on its key, apart from the abilities that cast themselves
            var dash = Add(held, "sv-slots", "Dash");
            dashSlot = new Slot { Box = Add(dash, "sv-slot") };
            dashSlot.Box.AddToClassList("sv-slot--dash");
            dashSlot.Shade = Add(dashSlot.Box, "sv-slot__shade");
            dashSlot.Name = Text(dashSlot.Box, "sv-slot__name", "Dash");
            dashSlot.Level = Text(dashSlot.Box, "sv-slot__key", "Space");
            abilitySlots = Add(held, "sv-slots", "Abilities");
            passiveSlots = Add(held, "sv-slots", "Passives");
            passiveSlots.AddToClassList("sv-slots--passive");
            Text(dock, "sv-keys", "WASD or the arrows move · Space dashes · abilities cast themselves · 1–5 or a click takes a card · R starts again");

            // The cards, over everything, while they're up
            cards = Add(root, "sv-cards", "Cards");
            cards.pickingMode = PickingMode.Position;
            cardsTitle = Text(cards, "sv-cards__title", "");
            cardRow = Add(cards, "sv-cards__row");
            cards.style.display = DisplayStyle.None;

            // The run's end
            end = Add(root, "sv-end", "End");
            end.pickingMode = PickingMode.Position;
            Text(end, "sv-end__title", "You have fallen");
            endStats = Text(end, "sv-end__stats", "");
            var again = new Button(() => SurvivorsGame.Current?.StartRun()) { text = "Go again (R)" };
            again.AddToClassList("sv-button");
            end.Add(again);
            end.style.display = DisplayStyle.None;
        }

        private static VisualElement Add(VisualElement parent, string style, string name = null)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            if (name != null) element.name = name;
            element.AddToClassList(style);
            parent.Add(element);
            return element;
        }

        private static Label Text(VisualElement parent, string style, string words)
        {
            var label = new Label(words) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(style);
            parent.Add(label);
            return label;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Each frame
        // ═══════════════════════════════════════════════════════════════════════════

        private void Update()
        {
            if (root == null) Build();
            var game = SurvivorsGame.Current;
            if (game != hooked)
            {
                if (hooked != null) hooked.Changed -= Redraw;
                hooked = game;
                if (hooked != null) hooked.Changed += Redraw;
                Redraw();
            }
            frameTime = Mathf.Lerp(frameTime, Time.unscaledDeltaTime, 0.05f);
            if (game == null) return;

            Keys(game);
            Bars(game);
            if (Time.unscaledTime >= redrawAt)
            {
                redrawAt = Time.unscaledTime + RedrawEvery;
                Words(game);
                Slots(game);
            }
            bool showing = !string.IsNullOrEmpty(game.Banner) && Time.unscaledTime < game.BannerUntil;
            banner.style.display = showing ? DisplayStyle.Flex : DisplayStyle.None;
            if (showing) banner.text = game.Banner;
        }

        // A card by its number; R for another run once this one's over
        private void Keys(SurvivorsGame game)
        {
            var input = ForgeInput.Current;
            if (game.Draft != null)
            {
                for (int i = 0; i < game.Draft.Cards.Count && i < 9; i++)
                    if (input.KeyPressed(KeyCode.Alpha1 + i) || input.KeyPressed(KeyCode.Keypad1 + i))
                    {
                        game.Choose(i);
                        return;
                    }
            }
            if (game.Over && input.KeyPressed(KeyCode.R)) game.StartRun();
        }

        private void Bars(SurvivorsGame game)
        {
            float needed = Mathf.Max(1f, game.ExperienceNeeded);
            experienceFill.style.width = Length.Percent(Mathf.Clamp01(game.Experience / needed) * 100f);
            var hero = game.Hero;
            var roster = game.Roster;
            float share = 0f;
            if (hero && roster && roster.HealthAttribute)
            {
                float most = Mathf.Max(1f, hero.Base(roster.HealthAttribute));
                share = Mathf.Clamp01(hero.Value(roster.HealthAttribute) / most);
            }
            healthFill.style.width = Length.Percent(share * 100f);
            heroBarFill.style.width = Length.Percent(share * 100f);

            // The small bar over the hero, where the camera sees it
            var camera = Camera ? Camera : UnityEngine.Camera.main;
            if (!hero || !camera || root.panel == null)
            {
                heroBar.style.display = DisplayStyle.None;
                return;
            }
            var over = hero.transform.position + Vector3.up * 2.4f;
            if (camera.WorldToViewportPoint(over).z <= 0f)
            {
                heroBar.style.display = DisplayStyle.None;
                return;
            }
            var at = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, over, camera);
            heroBar.style.display = DisplayStyle.Flex;
            heroBar.style.left = at.x - 24f;
            heroBar.style.top = at.y - 4f;
        }

        private void Words(SurvivorsGame game)
        {
            levelLabel.text = $"Level {game.Level}";
            var roster = game.Roster;
            bool boss = roster && roster.IsBossWave(game.Wave);
            waveLabel.text = boss ? $"Wave {game.Wave} · boss wave" : $"Wave {game.Wave}";
            timeLabel.text = $"Next wave in {Mathf.CeilToInt(Mathf.Max(0f, game.WaveLeft))}s · survived {Clock(game.Survived)}";
            killsLabel.text = boss && game.Horde.BossesLeft > 0 ? $"{game.Kills} kills · {game.Horde.BossesLeft} hard ones left" : $"{game.Kills} kills";
            float fps = frameTime > 0f ? 1f / frameTime : 0f;
            costLabel.text = $"{fps:0} fps · {frameTime * 1000f:0.0} ms";
            var control = ProcessControl.Instance;
            countsLabel.text = control != null
                ? $"{game.Horde.Count} enemies · {control.Active} processes · {game.Crystals.Count} crystals"
                : $"{game.Horde.Count} enemies · {game.Crystals.Count} crystals";
            var hero = game.Hero;
            if (hero && roster && roster.HealthAttribute)
                healthLabel.text = $"{Mathf.CeilToInt(hero.Value(roster.HealthAttribute))} / {Mathf.CeilToInt(hero.Base(roster.HealthAttribute))}";
            endStats.text = $"Wave {game.Wave} · level {game.Level} · {game.Kills} kills · survived {Clock(game.Survived)}";
        }

        private static string Clock(float seconds)
        {
            int whole = Mathf.FloorToInt(seconds);
            return $"{whole / 60}:{whole % 60:00}";
        }

        // The abilities and passives held, each with its level, an ability shaded by its cooldown's share left
        private void Slots(SurvivorsGame game)
        {
            var hero = game.Hero;
            var roster = game.Roster;
            if (!hero || !roster) return;
            int at = 0;
            foreach (var ability in hero.Known())
            {
                if (!roster.AbilityPool.Contains(ability)) continue;
                var slot = SlotAt(abilities, abilitySlots, at++);
                slot.Name.text = ability.GetName();
                slot.Level.text = $"{hero.LevelOf(ability)}";
                float left = hero.CooldownLeft(ability);
                float whole = ability.Cooldown && hero.Gas.TryGetEffectContainer(ability.Cooldown, out var cooldown) ? cooldown.TotalDuration : 0f;
                slot.Shade.style.height = Length.Percent(whole > 0f ? Mathf.Clamp01(left / whole) * 100f : 0f);
            }
            Empty(abilities, abilitySlots, at, roster.MaxAbilities);
            var dash = roster.Dash;
            dashSlot.Box.parent.style.display = dash ? DisplayStyle.Flex : DisplayStyle.None;
            if (dash)
            {
                float left = hero.CooldownLeft(dash);
                float whole = dash.Cooldown && hero.Gas.TryGetEffectContainer(dash.Cooldown, out var dashCooldown) ? dashCooldown.TotalDuration : 0f;
                dashSlot.Shade.style.height = Length.Percent(whole > 0f ? Mathf.Clamp01(left / whole) * 100f : 0f);
            }
            at = 0;
            foreach (var item in hero.Items())
            {
                if (!roster.PassivePool.Contains(item)) continue;
                var slot = SlotAt(passives, passiveSlots, at++);
                slot.Name.text = item.GetName();
                slot.Level.text = $"{hero.LevelOf(item)}";
                slot.Shade.style.height = Length.Percent(0f);
            }
            Empty(passives, passiveSlots, at, roster.MaxPassives);
        }

        private static Slot SlotAt(List<Slot> list, VisualElement parent, int index)
        {
            while (list.Count <= index)
            {
                var box = Add(parent, "sv-slot");
                var slot = new Slot { Box = box, Shade = Add(box, "sv-slot__shade"), Name = Text(box, "sv-slot__name", ""), Level = Text(box, "sv-slot__level", "") };
                list.Add(slot);
            }
            var held = list[index];
            held.Box.RemoveFromClassList("sv-slot--empty");
            return held;
        }

        // The slots past those held, to the most the hero may hold, shown empty
        private static void Empty(List<Slot> list, VisualElement parent, int from, int most)
        {
            for (int i = from; i < most; i++)
            {
                var slot = SlotAt(list, parent, i);
                slot.Box.AddToClassList("sv-slot--empty");
                slot.Name.text = "";
                slot.Level.text = "";
                slot.Shade.style.height = Length.Percent(0f);
            }
            for (int i = most; i < list.Count; i++) list[i].Box.style.display = DisplayStyle.None;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The cards and the end, as the game changes
        // ═══════════════════════════════════════════════════════════════════════════

        private void Redraw()
        {
            if (root == null) return;
            var game = hooked;
            var draft = game ? game.Draft : null;
            if (draft != shown)
            {
                shown = draft;
                cardRow.Clear();
                if (draft != null)
                {
                    cardsTitle.text = draft.Kind == ESurvivorsDraftKind.Passive
                        ? "Boss wave defeated · take a passive" : $"Level {game.Level} · take an ability";
                    for (int i = 0; i < draft.Cards.Count; i++) cardRow.Add(Card(draft.Cards[i], i));
                }
            }
            cards.style.display = draft != null ? DisplayStyle.Flex : DisplayStyle.None;
            end.style.display = game && game.Over ? DisplayStyle.Flex : DisplayStyle.None;
            if (game) Words(game);
        }

        private VisualElement Card(SurvivorsCard card, int index)
        {
            var button = new Button(() => SurvivorsGame.Current?.Choose(index));
            button.AddToClassList("sv-card");
            if (card.Passive) button.AddToClassList("sv-card--passive");
            var key = new Label($"{index + 1}") { pickingMode = PickingMode.Ignore };
            key.AddToClassList("sv-card__key");
            button.Add(key);
            var name = new Label(card.Name) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("sv-card__name");
            button.Add(name);
            var level = new Label(card.IsNew ? "New" : $"Level {card.From} → {card.To}") { pickingMode = PickingMode.Ignore };
            level.AddToClassList(card.IsNew ? "sv-card__new" : "sv-card__level");
            button.Add(level);
            var words = new Label(card.Description) { pickingMode = PickingMode.Ignore };
            words.AddToClassList("sv-card__words");
            button.Add(words);
            return button;
        }
    }
}
