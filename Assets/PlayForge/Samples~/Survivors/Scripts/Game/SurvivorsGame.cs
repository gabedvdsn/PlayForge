using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// The Survivors demo's game: the hero, the waves of the horde, the crystals they drop, the experience that levels
    /// the hero, and the cards each level and each boss wave's end offer. A wave lasts its roster's seconds and sends its
    /// enemies all through them, more and harder each wave, until hundreds stand; every so many waves a boss wave sends
    /// its hard ones besides, and killing them all offers the passives. The hero and the enemies are the Creator Lab's
    /// units on PlayForge's GAS, each a <see cref="GameplayAbilitySystem"/>, so the demo is PlayForge carrying a horde.
    /// The HUD reads it; while cards are up or the run is over, time stands.
    /// </summary>
    public class SurvivorsGame : MonoBehaviour
    {
        public static SurvivorsGame Current { get; private set; }

        public SurvivorsRoster Roster;

        [Tooltip("A fixed seed for the waves and the cards; 0 for a new one each run")]
        public int Seed;

        [Tooltip("The floor's size, a side")]
        [Min(20f)] public float ArenaSize = 160f;

        [Tooltip("The floor's color")]
        public Color FloorColor = new(0.16f, 0.2f, 0.18f);

        public LabUnit Hero { get; private set; }
        public SurvivorsHorde Horde { get; private set; }
        public SurvivorsCrystals Crystals { get; private set; }

        /// <summary>The wave the run is on, from 1.</summary>
        public int Wave { get; private set; }

        /// <summary>Seconds left of the wave.</summary>
        public float WaveLeft { get; private set; }

        /// <summary>The hero's level, from 1.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>The experience toward the hero's next level.</summary>
        public float Experience { get; private set; }

        /// <summary>The experience the hero's next level takes.</summary>
        public float ExperienceNeeded => Roster ? Roster.ExperienceFor(Level) : 1f;

        public int Kills { get; private set; }

        /// <summary>Seconds of play the run has lasted.</summary>
        public float Survived { get; private set; }

        /// <summary>Whether the hero has fallen, which ends the run.</summary>
        public bool Over { get; private set; }

        /// <summary>The cards up now, or null with none.</summary>
        public SurvivorsDraft Draft { get; private set; }

        /// <summary>The words across the screen a moment, as a wave starts.</summary>
        public string Banner { get; private set; }

        /// <summary>Until when, in unscaled seconds, the banner shows.</summary>
        public float BannerUntil { get; private set; }

        /// <summary>Raised as the cards come or go, a wave starts or the run ends, for the HUD to redraw.</summary>
        public event Action Changed;

        /// <summary>The run's random numbers, its waves' and its cards'.</summary>
        public System.Random Random { get; private set; }

        private readonly Queue<ESurvivorsDraftKind> waiting = new();
        private float spawnBudget;
        private float regenerated;

        private void Awake()
        {
            Current = this;
            Horde = new SurvivorsHorde(this);
            Crystals = new SurvivorsCrystals(this);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            MakeArena();
            StartRun();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // A run
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A new run: the hero at the middle, level 1, and the first wave.</summary>
        public void StartRun()
        {
            Time.timeScale = 1f;
            Horde.Clear();
            Crystals.Clear();
            waiting.Clear();
            if (Hero) Hero.Remove();
            Random = new System.Random(Seed != 0 ? Seed : Environment.TickCount);
            Level = 1;
            Experience = 0f;
            Kills = 0;
            Survived = 0f;
            Wave = 0;
            Over = false;
            Draft = null;
            spawnBudget = 0f;
            Hero = SpawnHero();
            NextWave();
        }

        private LabUnit SpawnHero()
        {
            if (!Roster || !Roster.Hero) return null;
            var hero = LabUnit.Create(new LabUnitSpec
            {
                Identity = Roster.Hero, Rules = Roster, Side = new LabSide("Hero", Roster.HeroSide, Roster.HeroColor),
                Level = 1, Color = Roster.HeroColor, At = Vector3.zero, Ring = true, Stays = true,
            });
            if (!hero) return null;
            var player = hero.gameObject.AddComponent<SurvivorsHero>();
            player.Unit = hero;
            return hero;
        }

        private void Update()
        {
            if (Over || Draft != null || !Roster) return;
            float delta = Time.deltaTime;
            if (delta <= 0f) return;

            if (!Hero || !Hero.Alive)
            {
                EndRun();
                return;
            }
            Survived += delta;
            WaveLeft -= delta;
            if (WaveLeft <= 0f) NextWave();

            // The wave's enemies come all through its time, a few at once, while the horde has room
            spawnBudget += Roster.CountFor(Wave) / Roster.WaveSeconds * delta;
            while (spawnBudget >= 1f && Horde.Count < Roster.MaxEnemies)
            {
                var kind = PickKind();
                if (kind == null) break;
                spawnBudget -= kind.Group;
                Horde.SpawnGroup(kind, Roster.EnemyLevelFor(Wave), SpawnPoint(), boss: false);
            }

            Horde.Step(delta);
            Crystals.Step(delta);
            Regenerate(delta);

            if (waiting.Count > 0) OpenDraft(waiting.Dequeue());
        }

        /// <summary>The next wave: its banner, and a boss wave's hard ones at once.</summary>
        private void NextWave()
        {
            Wave++;
            WaveLeft = Roster.WaveSeconds;
            var boss = Roster.BossWaveFor(Wave);
            if (boss != null)
            {
                foreach (var entry in boss.Units)
                    for (int i = 0; i < entry.Count; i++)
                        Horde.Spawn(entry.Kind, Roster.EnemyLevelFor(Wave) + entry.ExtraLevels, SpawnPoint(), boss: true);
                Show($"Wave {Wave}: {boss.Name}");
            }
            else Show($"Wave {Wave}");
        }

        /// <summary>A kind the wave may send, by weight, among those come by now.</summary>
        private SurvivorsEnemyKind PickKind()
        {
            float total = 0f;
            foreach (var kind in Roster.Enemies)
                if (kind?.Identity && kind.FirstWave <= Wave) total += kind.Weight;
            if (total <= 0f) return null;
            float pick = (float)Random.NextDouble() * total;
            foreach (var kind in Roster.Enemies)
            {
                if (!kind?.Identity || kind.FirstWave > Wave) continue;
                pick -= kind.Weight;
                if (pick <= 0f) return kind;
            }
            return null;
        }

        /// <summary>A place past what the camera sees, around the hero, within the arena.</summary>
        public Vector3 SpawnPoint()
        {
            float angle = (float)Random.NextDouble() * Mathf.PI * 2f;
            float distance = Roster.SpawnDistance * (1f + (float)Random.NextDouble() * 0.25f);
            var centre = Hero ? Hero.transform.position : Vector3.zero;
            var at = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            float half = ArenaSize / 2f - 2f;
            return new Vector3(Mathf.Clamp(at.x, -half, half), 0f, Mathf.Clamp(at.z, -half, half));
        }

        // The hero's Health Regeneration, a second's worth each second
        private void Regenerate(float delta)
        {
            regenerated += delta;
            if (regenerated < 1f || !Roster.HealthRegeneration || !Roster.HealthAttribute) return;
            regenerated -= 1f;
            float regen = Hero.Value(Roster.HealthRegeneration);
            float missing = Hero.Base(Roster.HealthAttribute) - Hero.Value(Roster.HealthAttribute);
            if (regen <= 0f || missing <= 0f) return;
            Hero.Gas.TryModifyAttribute(Roster.HealthAttribute,
                SourcedModifiedAttributeValue.GenerateSimple(Hero.Gas, Roster.HealthAttribute, Mathf.Min(regen, missing), 0f));
        }

        private void EndRun()
        {
            Over = true;
            Time.timeScale = 0f;
            Show("You have fallen");
            Changed?.Invoke();
        }

        private void Show(string words)
        {
            Banner = words;
            BannerUntil = Time.unscaledTime + 2.5f;
            Changed?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What the horde reports
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>An enemy fell: one more kill.</summary>
        internal void Killed() => Kills++;

        /// <summary>A boss wave's last hard one fell: the passives' cards come.</summary>
        internal void BossWaveDefeated()
        {
            Show("Boss wave defeated");
            waiting.Enqueue(ESurvivorsDraftKind.Passive);
        }

        /// <summary>The hero picked up experience; each level it reaches brings a level-up's cards.</summary>
        public void Gain(float experience)
        {
            if (Over || !Hero) return;
            Experience += experience;
            while (Experience >= ExperienceNeeded)
            {
                Experience -= ExperienceNeeded;
                Level++;
                Hero.Gas.SetLevel(Roster.Hero.GetAssetTag(), new IntValuePair(Level, Mathf.Max(Level, Roster.Hero.MaxLevel)));
                waiting.Enqueue(ESurvivorsDraftKind.Ability);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Cards
        // ═══════════════════════════════════════════════════════════════════════════

        private void OpenDraft(ESurvivorsDraftKind kind)
        {
            var draft = SurvivorsDraft.For(kind, this);
            if (draft == null)
            {
                // Nothing left to offer: a heal in its place
                if (Roster.HealthAttribute)
                    Hero.Gas.TryModifyAttribute(Roster.HealthAttribute,
                        SourcedModifiedAttributeValue.GenerateSimple(Hero.Gas, Roster.HealthAttribute, Hero.Base(Roster.HealthAttribute) * 0.3f, 0f));
                return;
            }
            Draft = draft;
            Time.timeScale = 0f;
            Changed?.Invoke();
        }

        /// <summary>Takes a card of those up: the ability or passive it offers, new or a level more.</summary>
        public void Choose(int index)
        {
            if (Draft == null || index < 0 || index >= Draft.Cards.Count) return;
            Draft.Apply(index, this);
            Draft = null;
            Time.timeScale = Over ? 0f : 1f;
            Changed?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The arena
        // ═══════════════════════════════════════════════════════════════════════════

        // A floor, made as the scene plays in the pipeline's own default material, with a grid of darker lines on it
        private void MakeArena()
        {
            SurvivorsShape.Create("Floor", transform, PrimitiveType.Cube, new Vector3(ArenaSize, 0.2f, ArenaSize), FloorColor,
                new Vector3(0f, -0.1f, 0f));
            var line = FloorColor * 0.75f;
            line.a = 1f;
            for (int i = -(int)(ArenaSize / 2f); i <= (int)(ArenaSize / 2f); i += 8)
            {
                Mark(new Vector3(i, 0.01f, 0f), new Vector3(0.08f, 0.02f, ArenaSize), line);
                Mark(new Vector3(0f, 0.01f, i), new Vector3(ArenaSize, 0.02f, 0.08f), line);
            }
        }

        private void Mark(Vector3 at, Vector3 size, Color color)
        {
            var mark = SurvivorsShape.Create("Line", transform, PrimitiveType.Cube, size, color);
            mark.transform.localPosition = at;
        }
    }
}
