using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Samples.Demo.Building
{
    public static partial class DemoBuilder
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Entities
        // ═══════════════════════════════════════════════════════════════════════════

        private static EntityIdentity HeroTemplate, Dummy;
        private static readonly List<(EntityIdentity identity, Color color, string summary)> Heroes = new();

        private static EntityIdentity NewEntity(string name, string description)
        {
            var entity = ScriptableObject.CreateInstance<EntityIdentity>();
            entity.Name = name;
            entity.Description = description;
            entity.Textures = new List<TextureItem>();
            entity.Affiliation = new List<Tag>();
            entity.AssetTag = T($"Demo.Entity.{Key(name)}");
            entity.GrantedTags = new List<Tag>();
            entity.ContextTags = new List<Tag>();
            entity.StartingLevel = 1;
            entity.MaxLevel = 10;
            entity.ActivationPolicy = EAbilityActivationPolicy.QueueActivationIfBusy;
            entity.MaxAbilitiesOperation = Count(12);
            entity.StartingAbilities = new List<Ability>();
            entity.MaxItemsOperation = Count(12);
            entity.MaxEquippedItemsOperation = Count(6);
            entity.StartingItems = new List<StartingItemContainer>();
            entity.WorkerGroup = new StandardWorkerGroup { Name = "Workers" };
            entity.LocalData = new List<DataWrapper>();
            return entity;
        }

        private static EntityIdentity Hero(string name, string description, AttributeSet set, List<Ability> kit, float range, string castOrder,
            Color color, string summary)
        {
            var hero = ChildOf(HeroTemplate);
            hero.Name = name;
            hero.Description = description;
            hero.AssetTag = T($"Demo.Entity.{Key(name)}");
            hero.AttributeSet = set;
            hero.StartingAbilities = new List<Ability>(kit) { Passive };
            hero.LocalData = new List<DataWrapper> { Local(AttackRange, range), Local(CastOrder, castOrder) };
            hero = Settle(Save($"Heroes/{name}", hero));
            set.LinkToProvider(hero);
            Dirty(set);
            Heroes.Add((hero, color, summary));
            return hero;
        }

        /// <summary>A hero's own bases for its vitals: Health, Mana and Armor, each still plus its primary times its constant.</summary>
        private static AttributeSetElement[] Vitals(float health, float mana, float armor) => new[]
        {
            Element(Health, PlusPrimary(health, Strength, 10f), EAttributeOverflowPolicy.ZeroToBase, scaleWithBase: true),
            Element(Mana, PlusPrimary(mana, Intelligence, 12f), EAttributeOverflowPolicy.ZeroToBase, scaleWithBase: true),
            Element(Armor, PlusPrimary(armor, Agility, 8f)),
        };

        private static void MakeEntities()
        {
            Heroes.Clear();
            var template = NewEntity("Hero", "The template every hero takes: levels 1 to 10, a death watcher on its Health, a melee attack range and the Hero attribute set. A hero sets its own set, abilities and Local Data.");
            template.AttributeSet = HeroSet;
            template.WorkerGroup.AnalysisWorkers.Add(new DeathWatcherAnalysisWorker
            {
                Target = Health, Threshold = 0f, Comparison = EThresholdComparison.LessThanOrEqual, TriggerOnce = true,
            });
            template.LocalData.Add(Local(AttackRange, 2.2f));
            HeroTemplate = Save("Heroes/Templates/Hero", template);

            Hero("Vanguard", "A front-line fighter that stuns and shields.",
                HeroAttributes("Vanguard", (24f, 3f), (10f, 1.5f), (12f, 1.5f), 5.5f, Vitals(250f, 80f, 60f)),
                VanguardKit, 2.4f, "Q E W", new Color(0.35f, 0.55f, 0.9f), "Strength, melee, physical. Shield Bash stuns; Rallying Cry adds Armor; Iron Skin breaks and blocks stuns.");
            Hero("Ranger", "A ranged attacker that marks and kites.",
                HeroAttributes("Ranger", (16f, 2f), (22f, 3f), (14f, 1.5f), 6f, Vitals(170f, 90f, 0f)),
                RangerKit, 9f, "Q W E", new Color(0.35f, 0.75f, 0.4f), "Agility, ranged, physical. Hunter's Mark makes physical blows hurt more; Volley hits an area; Tumble rolls away.");
            Hero("Pyromancer", "A burst caster built on stacking burns.",
                HeroAttributes("Pyromancer", (14f, 1.5f), (10f, 1.5f), (26f, 3.5f), 5.5f, Vitals(150f, 130f, 0f)),
                PyromancerKit, 8f, "W Q E", new Color(0.9f, 0.4f, 0.2f), "Intelligence, ranged, magic. Fireball and Flame Wall stack Burning, which bursts at six; Combustion ignites it.");
            Hero("Shade", "An assassin that strikes from stealth.",
                HeroAttributes("Shade", (18f, 2f), (24f, 3f), (12f, 1.5f), 6.5f, Vitals(160f, 90f, 10f)),
                ShadeKit, 2.2f, "Q W E", new Color(0.6f, 0.35f, 0.8f), "Agility, melee, pure. Shadow Step empowers its next cut; Eviscerate finishes the hurt; Smoke Veil hides it.");
            Hero("Oracle", "A support caster that drains, silences and heals.",
                HeroAttributes("Oracle", (15f, 1.5f), (12f, 1.5f), (24f, 3f), 5.5f, Vitals(160f, 140f, 10f)),
                OracleKit, 8f, "W Q E", new Color(0.35f, 0.85f, 0.85f), "Intelligence, ranged, magic. Drain Life channels and heals; Hex silences; Renewal cleanses and heals.");

            // The Shade mends while it's invisible: the demo's own tag worker, in its entity's worker group
            var shade = Heroes.First(each => each.identity.Name == "Shade").identity;
            shade.WorkerGroup.TagWorkers.Add(new WhileTaggedEffectWorker
            {
                Effect = ShadowMend,
                Requirements = new TagWorkerRequirements { TagPackets = new List<TagWorkerRequirementPacket> { new(Invisible, ERequireAvoidPolicy.Require) } },
            });
            Settle(Dirty(shade));

            var dummy = NewEntity("Training Dummy", "Stands and takes damage, and mends to full after it rests. It never dies: no death watcher is in its worker group.");
            dummy.AttributeSet = DummySet;
            dummy.MaxLevel = 1;
            Dummy = Save("Heroes/Training Dummy", dummy);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The roster
        // ═══════════════════════════════════════════════════════════════════════════

        private static DemoRoster Roster;

        private static void MakeRoster()
        {
            var roster = ScriptableObject.CreateInstance<DemoRoster>();
            roster.Heroes = Heroes.Select(each => new DemoHero { Identity = each.identity, Color = each.color, Summary = each.summary }).ToList();
            roster.TrainingDummy = Dummy;
            roster.DummyColor = new Color(0.62f, 0.56f, 0.45f);
            roster.DummyMend = DummyMend;
            roster.Items = AllItems.ToList();
            roster.Health = Health;
            roster.Mana = Mana;
            roster.MovementSpeed = MoveSpeed;
            roster.Shown = new List<Attribute> { Strength, Agility, Intelligence, Armor, MagicResist, AttackAmp, MagicAmp, Lifesteal, CooldownReduction, MoveSpeed };
            roster.PlayerSide = PlayerSide;
            roster.OpponentSide = OpponentSide;
            roster.Stunned = Stunned;
            roster.Invisible = Invisible;
            roster.Untargetable = Untargetable;
            roster.Channeling = Channeling;
            roster.AttackRange = AttackRange;
            roster.CastOrder = CastOrder;
            roster.Reach = Reach;
            roster.CastWhen = CastWhen;
            roster.Difficulty = Difficulty;
            roster.Physical = Physical;
            roster.Magic = Magic;
            roster.Pure = Pure;
            Roster = Save("Demo Roster", roster);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // UI
        // ═══════════════════════════════════════════════════════════════════════════

        private static PanelSettings Panel;
        private static StyleSheet Style;

        private static void MakeUI()
        {
            string theme = $"{Root}/UI/Demo Theme.tss", style = $"{Root}/UI/Demo.uss";
            AssetDatabase.ImportAsset(theme, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(style, ImportAssetOptions.ForceUpdate);
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(theme);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            Panel = Save("UI/Demo Panel Settings", settings);
            Style = AssetDatabase.LoadAssetAtPath<StyleSheet>(style);
            if (Panel.themeStyleSheet == null) throw new System.Exception($"No theme at {theme}");
            if (Style == null) throw new System.Exception($"No style sheet at {style}");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The scene
        // ═══════════════════════════════════════════════════════════════════════════

        private const string Bootstrapper = "Packages/com.faremerald.playforge/Runtime/Prefabs/Bootstrapper/Bootstrapper.prefab";

        private static GameObject Shape(string name, Transform parent, PrimitiveType shape, Vector3 at, Vector3 size, Color color, bool collider = false)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = at;
            var made = host.AddComponent<DemoShape>();
            made.Shape = shape;
            made.Size = size;
            made.Color = color;
            made.KeepCollider = collider;
            return host;
        }

        private static void MakeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 15f, -18f), Quaternion.LookRotation(new Vector3(0f, -15f, 11f)));
            camera.fieldOfView = 50f;
            camera.gameObject.AddComponent<DemoCamera>();

            var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault();
            if (light) light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var boot = AssetDatabase.LoadAssetAtPath<GameObject>(Bootstrapper);
            if (boot == null) throw new System.Exception($"No Bootstrapper prefab at {Bootstrapper}");
            var booted = (GameObject)PrefabUtility.InstantiatePrefab(boot, scene);
            booted.name = "PlayForge Bootstrapper";

            var arena = new GameObject("Arena").transform;
            Shape("Floor", arena, PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(44f, 0.2f, 44f), new Color(0.32f, 0.35f, 0.3f), collider: true);
            foreach (var corner in new[] { new Vector3(-9f, 0f, -9f), new Vector3(9f, 0f, -9f), new Vector3(-9f, 0f, 9f), new Vector3(9f, 0f, 9f) })
                Shape("Pillar", arena, PrimitiveType.Cylinder, corner + Vector3.up * 1.5f, new Vector3(1.4f, 1.5f, 1.4f), new Color(0.55f, 0.52f, 0.47f));

            var playerStart = new GameObject("Player Start").transform;
            playerStart.SetPositionAndRotation(new Vector3(0f, 0f, -7f), Quaternion.identity);
            var opponentStart = new GameObject("Opponent Start").transform;
            opponentStart.SetPositionAndRotation(new Vector3(0f, 0f, 7f), Quaternion.Euler(0f, 180f, 0f));

            var demo = new GameObject("Demo");
            var game = demo.AddComponent<DemoGame>();
            game.Roster = Roster;
            game.PlayerStart = playerStart;
            game.OpponentStart = opponentStart;
            game.RespawnSeconds = 5f;
            var player = demo.AddComponent<DemoPlayer>();
            player.Camera = camera;
            var document = demo.AddComponent<UIDocument>();
            document.panelSettings = Panel;
            var hud = demo.AddComponent<DemoHud>();
            hud.Style = Style;
            hud.Camera = camera;

            string path = $"{Root}/Demo.unity";
            if (!EditorSceneManager.SaveScene(scene, path)) throw new System.Exception($"The scene didn't save to {path}");
            made++;
        }
    }
}
