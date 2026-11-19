using System.Collections.Generic;
using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// The horde: every enemy standing, each a Creator Lab unit on PlayForge's GAS that walks at the hero and attacks it,
    /// as the lab's units do. The horde casts each enemy's other abilities as they're ready and the hero is within their
    /// reach, keeps the enemies from piling into one, and, as each falls, drops its crystal, breaks it into its splits and
    /// counts it. A boss wave's hard ones it counts apart, to tell the game when the last falls.
    /// </summary>
    public sealed class SurvivorsHorde
    {
        /// <summary>One enemy of the horde.</summary>
        public sealed class Enemy
        {
            public LabUnit Unit;
            public SurvivorsEnemyKind Kind;
            public bool Boss;
            public float Radius;
            internal float NextThought;
        }

        private const float ThinkEvery = 0.25f;
        private const float Cell = 1.5f;

        private readonly SurvivorsGame game;
        private readonly List<Enemy> enemies = new();
        private readonly Dictionary<Vector2Int, List<Enemy>> cells = new();
        private readonly Stack<List<Enemy>> spareCells = new();
        private readonly List<Ability> casts = new();
        private float clock;

        public SurvivorsHorde(SurvivorsGame game) => this.game = game;

        /// <summary>How many enemies stand.</summary>
        public int Count => enemies.Count;

        /// <summary>Every enemy standing.</summary>
        public IReadOnlyList<Enemy> All => enemies;

        /// <summary>How many of a boss wave's hard ones still stand.</summary>
        public int BossesLeft { get; private set; }

        /// <summary>How far the nearest enemy stands from the hero, at the last step.</summary>
        public float NearestDistance { get; private set; } = float.MaxValue;

        /// <summary>A group of a kind around a point, as many as the kind comes in.</summary>
        public void SpawnGroup(SurvivorsEnemyKind kind, int level, Vector3 at, bool boss)
        {
            for (int i = 0; i < Mathf.Max(1, kind.Group); i++)
            {
                var offset = i == 0 ? Vector3.zero : new Vector3((float)game.Random.NextDouble() - 0.5f, 0f, (float)game.Random.NextDouble() - 0.5f) * 3f;
                Spawn(kind, level, at + offset, boss);
            }
        }

        /// <summary>One enemy of a kind, at a level, at a point, set on the hero.</summary>
        public Enemy Spawn(SurvivorsEnemyKind kind, int level, Vector3 at, bool boss) => Spawn(kind, kind?.Identity, level, at, boss);

        /// <summary>
        /// Enemies of an entity raised in a ring around a point, as a boss summons them: the roster's kind of that entity,
        /// at the wave's level, while the horde has room.
        /// </summary>
        public void Summon(EntityIdentity identity, int count, Vector3 around)
        {
            var roster = game.Roster;
            if (identity == null || roster == null || count <= 0) return;
            var kind = roster.Enemies.Find(each => each != null && each.Identity == identity) ?? new SurvivorsEnemyKind { Identity = identity };
            int level = roster.EnemyLevelFor(game.Wave);
            for (int i = 0; i < count && enemies.Count < roster.MaxEnemies; i++)
            {
                float angle = Mathf.PI * 2f * i / count;
                Spawn(kind, identity, level, around + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f, false);
            }
        }

        private Enemy Spawn(SurvivorsEnemyKind kind, EntityIdentity identity, int level, Vector3 at, bool boss)
        {
            var roster = game.Roster;
            if (kind == null || identity == null || roster == null) return null;
            var hero = game.Hero;
            var facing = hero ? Quaternion.LookRotation(Flat(hero.transform.position - at) + Vector3.forward * 0.001f) : Quaternion.identity;
            var unit = LabUnit.Create(new LabUnitSpec
            {
                Identity = identity, Rules = roster, Side = new LabSide("Horde", roster.EnemySide, kind.Color), Level = level,
                Color = boss ? Color.Lerp(kind.Color, new Color(1f, 0.85f, 0.2f), 0.45f) : kind.Color,
                At = at, Facing = facing, Ring = boss, Stays = true,
            });
            if (!unit) return null;
            float size = kind.Size * (boss && identity == kind.Identity ? 1.15f : 1f);
            unit.transform.localScale = Vector3.one * size;
            if (hero) unit.AttackUnit(hero);
            var enemy = new Enemy
            {
                Unit = unit, Kind = kind, Boss = boss, Radius = 0.45f * size,
                NextThought = clock + (float)game.Random.NextDouble() * ThinkEvery,
            };
            enemies.Add(enemy);
            if (boss) BossesLeft++;
            return enemy;
        }

        /// <summary>A step of the horde: the fallen dropped and taken out, the standing set on the hero and thinking.</summary>
        public void Step(float delta)
        {
            clock += delta;
            var hero = game.Hero;
            var heroAt = hero ? hero.transform.position : Vector3.zero;
            float nearest = float.MaxValue;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (!enemy.Unit)
                {
                    Forget(i, enemy);
                    continue;
                }
                if (!enemy.Unit.Alive)
                {
                    Fall(enemy);
                    Forget(i, enemy);
                    continue;
                }
                if (hero && hero.Alive && enemy.Unit.Target != hero) enemy.Unit.AttackUnit(hero);
                float distance = Flat(enemy.Unit.transform.position - heroAt).magnitude;
                if (distance < nearest) nearest = distance;
                if (clock >= enemy.NextThought)
                {
                    enemy.NextThought = clock + ThinkEvery;
                    if (hero && hero.Alive) Think(enemy, hero, distance);
                }
            }
            NearestDistance = nearest;
            Separate();
        }

        // Its abilities past its attack, each as it's ready, where the hero is within its reach
        private void Think(Enemy enemy, LabUnit hero, float distance)
        {
            casts.Clear();
            foreach (var ability in enemy.Unit.Casts()) casts.Add(ability);
            for (int i = 0; i < casts.Count; i++)
            {
                var ability = casts[i];
                if (!enemy.Unit.Ready(ability)) continue;
                float reach = enemy.Unit.Reach(ability);
                if (reach > 0f && distance > reach) continue;
                enemy.Unit.Cast(ability, enemy.Unit.DataAt(hero));
                return;
            }
        }

        // Its crystal, its splits, its count, and its body taken out
        private void Fall(Enemy enemy)
        {
            var at = enemy.Unit.transform.position;
            game.Killed();
            game.Crystals.Drop(at, enemy.Kind.Experience * (enemy.Boss ? 5 : 1));
            if (enemy.Kind.SplitsInto && enemy.Kind.Splits > 0)
            {
                int level = game.Roster.EnemyLevelFor(game.Wave);
                for (int i = 0; i < enemy.Kind.Splits; i++)
                {
                    float angle = Mathf.PI * 2f * i / enemy.Kind.Splits;
                    var split = Spawn(enemy.Kind, enemy.Kind.SplitsInto, level, at + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.8f, false);
                    if (split == null) continue;
                    // The split is smaller, and breaks no further
                    split.Unit.transform.localScale = Vector3.one * enemy.Kind.Size * 0.6f;
                    split.Radius = 0.45f * enemy.Kind.Size * 0.6f;
                    split.Kind = new SurvivorsEnemyKind { Identity = enemy.Kind.SplitsInto, Color = enemy.Kind.Color, Size = enemy.Kind.Size * 0.6f, Experience = 1 };
                }
            }
            enemy.Unit.Remove();
        }

        private void Forget(int index, Enemy enemy)
        {
            enemies.RemoveAt(index);
            if (!enemy.Boss) return;
            BossesLeft--;
            if (BossesLeft == 0) game.BossWaveDefeated();
        }

        /// <summary>Every enemy taken out, as a new run starts.</summary>
        public void Clear()
        {
            foreach (var enemy in enemies)
                if (enemy.Unit) enemy.Unit.Remove();
            enemies.Clear();
            BossesLeft = 0;
            NearestDistance = float.MaxValue;
        }

        // Keeps the enemies from piling into one: each pair nearer than their sizes allow is pushed apart, half each,
        // through a grid of cells so a horde of hundreds costs a few neighbours each
        private void Separate()
        {
            foreach (var list in cells.Values)
            {
                list.Clear();
                spareCells.Push(list);
            }
            cells.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!enemy.Unit) continue;
                var key = CellOf(enemy.Unit.transform.position);
                if (!cells.TryGetValue(key, out var list))
                {
                    list = spareCells.Count > 0 ? spareCells.Pop() : new List<Enemy>();
                    cells[key] = list;
                }
                list.Add(enemy);
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (!enemy.Unit) continue;
                var at = enemy.Unit.transform.position;
                var key = CellOf(at);
                var push = Vector3.zero;
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!cells.TryGetValue(new Vector2Int(key.x + x, key.y + z), out var near)) continue;
                    for (int j = 0; j < near.Count; j++)
                    {
                        var other = near[j];
                        if (other == enemy || !other.Unit) continue;
                        var apart = Flat(at - other.Unit.transform.position);
                        float least = enemy.Radius + other.Radius;
                        float distance = apart.magnitude;
                        if (distance >= least) continue;
                        push += distance > 0.001f ? apart / distance * (least - distance) * 0.5f : new Vector3(0.1f, 0f, 0f);
                    }
                }
                if (push != Vector3.zero) enemy.Unit.transform.position = at + Vector3.ClampMagnitude(push, 0.25f);
            }
        }

        private static Vector2Int CellOf(Vector3 at) => new(Mathf.FloorToInt(at.x / Cell), Mathf.FloorToInt(at.z / Cell));

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
