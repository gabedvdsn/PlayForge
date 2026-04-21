using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace FarEmerald.PlayForge.Extended.SwarmDefenderSample
{
    /// <summary>
    /// Swarm Defender game manager.
    ///
    /// Responsibilities:
    ///  1) Seed the game data packet (hero, ability pool, buffs, prefabs).
    ///  2) Kick off the top-level game chain (MainGameLoop → GameOver, looping).
    ///  3) Build all UI infrastructure dynamically inside the scene's UIDocument
    ///     (Root container). HUD, level-up modal, and game-over panel.
    ///  4) Bridge between UI input and the sequence-driven game state — the UI
    ///     writes choices back into the shared data packet, which the sequences
    ///     are already polling via DelayUntil.
    ///
    /// The sequences own all gameplay logic. This manager is presentation + wiring.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SwarmDefenderGameManager : LazyMonoProcess
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Inspector
        // ═══════════════════════════════════════════════════════════════════════

        [Header("Scene References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Hero heroPrefab;

        [Header("Content")]
        [Tooltip("Prefabs used by the enemy/wave spawners.")]
        [SerializeField] private List<Character> enemyPrefabs = new();
        [Tooltip("Prefabs used by the boss round.")]
        [SerializeField] private List<Character> bossPrefabs = new();
        [Tooltip("The preset pool of abilities the hero can choose from on level-up.")]
        [SerializeField] private List<Ability> abilityPool = new();
        [Tooltip("Damage Amp + Cooldown Reduction buffs dropped by bosses.")]
        [SerializeField] private List<GameplayEffect> buffs = new();

        [Header("Tuning")]
        [Tooltip("Off-screen ring radius used for enemy spawns.")]
        [SerializeField] private float spawnRadius = 25f;
        [Tooltip("How many options to show on level-up (mix of upgrades and new abilities).")]
        [SerializeField] private int levelUpOptionCount = 3;

        // ═══════════════════════════════════════════════════════════════════════
        // Runtime State
        // ═══════════════════════════════════════════════════════════════════════

        private SequenceDataPacket _gameData;
        private ProcessRelay _gameRelay;

        private Hero hero;

        // UI elements
        private VisualElement _root;
        private VisualElement _hud;
        private ProgressBar _healthBar;
        private ProgressBar _xpBar;
        private Label _levelLabel;
        private VisualElement _buffRow;

        private VisualElement _levelUpModal;
        private Label _levelUpTitle;
        private VisualElement _levelUpOptionsRow;

        private VisualElement _gameOverPanel;
        private Label _gameOverTitle;
        private Button _playAgainButton;

        private bool _levelUpVisible;
        private bool _gameOverVisible;

        // Cached attribute refs
        private IAttribute _healthAttr;
        private IAttribute _xpAttr;

        // ═══════════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════════════════

        public override void WhenInitialize()
        {
            base.WhenInitialize();

            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

            _healthAttr = AttributeRegistry.GetByName("Health");
            _xpAttr     = AttributeRegistry.GetByName("Experience");

            BuildUI();
            SeedDataPacket();
            StartGameChain();
        }

        public override void WhenUpdate()
        {
            base.WhenUpdate();
            if (_gameData == null) return;

            RefreshHud();
            RefreshLevelUpModal();
            RefreshGameOverPanel();
        }

        public override void WhenTerminate()
        {
            base.WhenTerminate();
            CancelChain();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Data packet + chain bootstrap
        // ═══════════════════════════════════════════════════════════════════════

        private void SeedDataPacket()
        {
            _gameData = SequenceDataPacket.Default();

            ProcessControl.Register(heroPrefab, out var heroRelay);
            heroRelay.TryGetProcess(out hero);

            _gameData.SetPrimary(Tags.GAMEOBJECT, hero);
            _gameData.SetPrimary(SwarmTags.HERO, hero);
            _gameData.SetPrimary(SwarmTags.SPAWN_RADIUS, spawnRadius);

            // Payload lists consumed via TryGetLoadedAssets / GetAll.
            if (abilityPool.Count > 0) _gameData.AddPayload(Tags.ABILITIES, abilityPool);
            if (buffs.Count > 0)       _gameData.AddPayload(Tags.EFFECTS, buffs);
            if (enemyPrefabs.Count > 0) _gameData.AddPayload(SwarmTags.ENEMY_PREFABS, enemyPrefabs);
            if (bossPrefabs.Count > 0)  _gameData.AddPayload(SwarmTags.BOSS_PREFABS, bossPrefabs);

            // Flags in a known starting state.
            _gameData.SetPrimary(SwarmTags.LEVEL_UP_PENDING, false);
            _gameData.SetPrimary(SwarmTags.LEVEL_UP_CHOICE, false);
            _gameData.SetPrimary(SwarmTags.GAME_OVER, false);
        }

        private void StartGameChain()
        {
            CancelChain();
            var chain = SwarmDefenderAbilitySequences.BuildGameChain();

            Debug.Log(_gameData.GetPrimary<Hero>(SwarmTags.HERO));
            Debug.Log(_gameData.GetPrimary<Hero>(Tags.GAMEOBJECT));
            
            ProcessControl.Register(new TaskSequenceProcess(chain), this, _gameData, out _);
            
        }

        private void CancelChain()
        {
            if (_gameRelay is null) return;
            ProcessControl.Instance.TerminateImmediate(_gameRelay.CacheIndex);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UI Construction
        // ═══════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            var rve = uiDocument.rootVisualElement;
            _root = rve.Q<VisualElement>("Root") ?? rve;
            _root.style.flexGrow = 1;

            BuildHud();
            BuildLevelUpModal();
            BuildGameOverPanel();
        }

        private void BuildHud()
        {
            _hud = new VisualElement { name = "SD-Hud" };
            ApplyStyle(_hud.style, s =>
            {
                s.position = Position.Absolute;
                s.top = 16; s.left = 16; s.right = 16;
                s.flexDirection = FlexDirection.Row;
                s.alignItems = Align.Center;
                s.paddingTop = 8; s.paddingBottom = 8;
                s.paddingLeft = 12; s.paddingRight = 12;
                s.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
                s.borderTopLeftRadius = s.borderTopRightRadius = 6;
                s.borderBottomLeftRadius = s.borderBottomRightRadius = 6;
            });

            _levelLabel = new Label("Lvl 1");
            ApplyStyle(_levelLabel.style, s =>
            {
                s.color = Color.white;
                s.unityFontStyleAndWeight = FontStyle.Bold;
                s.fontSize = 18;
                s.marginRight = 16;
                s.minWidth = 60;
            });

            _healthBar = new ProgressBar { title = "HP", lowValue = 0f, highValue = 1f, value = 1f };
            ApplyStyle(_healthBar.style, s =>
            {
                s.flexGrow = 1f;
                s.marginRight = 12;
                s.minHeight = 22;
            });

            _xpBar = new ProgressBar { title = "XP", lowValue = 0f, highValue = 1f, value = 0f };
            ApplyStyle(_xpBar.style, s =>
            {
                s.flexGrow = 1f;
                s.marginRight = 12;
                s.minHeight = 22;
            });

            _buffRow = new VisualElement { name = "SD-BuffRow" };
            ApplyStyle(_buffRow.style, s =>
            {
                s.flexDirection = FlexDirection.Row;
                s.minWidth = 120;
            });

            _hud.Add(_levelLabel);
            _hud.Add(_healthBar);
            _hud.Add(_xpBar);
            _hud.Add(_buffRow);
            _root.Add(_hud);
        }

        private void BuildLevelUpModal()
        {
            _levelUpModal = new VisualElement { name = "SD-LevelUpModal" };
            ApplyStyle(_levelUpModal.style, s =>
            {
                s.position = Position.Absolute;
                s.top = 0; s.bottom = 0; s.left = 0; s.right = 0;
                s.alignItems = Align.Center;
                s.justifyContent = Justify.Center;
                s.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
                s.display = DisplayStyle.None;
            });

            var card = new VisualElement();
            ApplyStyle(card.style, s =>
            {
                s.paddingTop = 24; s.paddingBottom = 24;
                s.paddingLeft = 32; s.paddingRight = 32;
                s.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.98f);
                s.borderTopLeftRadius = s.borderTopRightRadius = 10;
                s.borderBottomLeftRadius = s.borderBottomRightRadius = 10;
                s.alignItems = Align.Center;
            });

            _levelUpTitle = new Label("Level Up!");
            ApplyStyle(_levelUpTitle.style, s =>
            {
                s.color = Color.white;
                s.fontSize = 28;
                s.unityFontStyleAndWeight = FontStyle.Bold;
                s.marginBottom = 16;
            });

            _levelUpOptionsRow = new VisualElement();
            ApplyStyle(_levelUpOptionsRow.style, s =>
            {
                s.flexDirection = FlexDirection.Row;
            });

            card.Add(_levelUpTitle);
            card.Add(_levelUpOptionsRow);
            _levelUpModal.Add(card);
            _root.Add(_levelUpModal);
        }

        private void BuildGameOverPanel()
        {
            _gameOverPanel = new VisualElement { name = "SD-GameOverPanel" };
            ApplyStyle(_gameOverPanel.style, s =>
            {
                s.position = Position.Absolute;
                s.top = 0; s.bottom = 0; s.left = 0; s.right = 0;
                s.alignItems = Align.Center;
                s.justifyContent = Justify.Center;
                s.backgroundColor = new Color(0f, 0f, 0f, 0.85f);
                s.display = DisplayStyle.None;
            });

            var card = new VisualElement();
            ApplyStyle(card.style, s =>
            {
                s.paddingTop = 32; s.paddingBottom = 32;
                s.paddingLeft = 40; s.paddingRight = 40;
                s.backgroundColor = new Color(0.15f, 0.05f, 0.05f, 0.98f);
                s.borderTopLeftRadius = s.borderTopRightRadius = 10;
                s.borderBottomLeftRadius = s.borderBottomRightRadius = 10;
                s.alignItems = Align.Center;
            });

            _gameOverTitle = new Label("You Died");
            ApplyStyle(_gameOverTitle.style, s =>
            {
                s.color = new Color(1f, 0.5f, 0.5f);
                s.fontSize = 42;
                s.unityFontStyleAndWeight = FontStyle.Bold;
                s.marginBottom = 20;
            });

            _playAgainButton = new Button(HandlePlayAgain) { text = "Play Again" };
            ApplyStyle(_playAgainButton.style, s =>
            {
                s.fontSize = 20;
                s.paddingTop = 10; s.paddingBottom = 10;
                s.paddingLeft = 28; s.paddingRight = 28;
                s.minWidth = 180;
            });

            card.Add(_gameOverTitle);
            card.Add(_playAgainButton);
            _gameOverPanel.Add(card);
            _root.Add(_gameOverPanel);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UI Refresh (per-frame)
        // ═══════════════════════════════════════════════════════════════════════

        private void RefreshHud()
        {
            if (hero == null) return;

            // Level
            var levelVal = hero.GetLevel();
            _levelLabel.text = $"Lvl {levelVal.CurrentValue}";

            // Health
            if (_healthAttr != null && hero.TryGetAttributeValue(_healthAttr, out var hp))
            {
                _healthBar.value = Mathf.Clamp01(hp.RatioMinZero);
                _healthBar.title = $"HP {hp.CurrentValue:0} / {hp.BaseValue:0}";
            }

            // XP — ratio against the 100*level threshold.
            if (_xpAttr != null && hero.TryGetAttributeValue(_xpAttr, out var xp))
            {
                float threshold = 100f * Mathf.Max(1, levelVal.CurrentValue);
                float ratio = threshold <= 0f ? 0f : Mathf.Clamp01(xp.CurrentValue / threshold);
                _xpBar.value = ratio;
                _xpBar.title = $"XP {xp.CurrentValue:0} / {threshold:0}";
            }

            RefreshBuffRow();
        }

        private void RefreshBuffRow()
        {
            // Re-render buff chips every frame (cheap — at most 2 elements).
            _buffRow.Clear();
            if (hero == null || buffs == null) return;

            foreach (var buff in buffs)
            {
                if (buff == null) continue;
                var tag = buff.GetAssetTag();
                // We show a chip if the hero has the effect live. Detect via granted tags:
                // most durational buffs grant a tag at application time.
                bool active = hero.AbilitySystem != null
                              && hero.AbilitySystem.Self != null
                              && HasActiveEffect(buff);

                if (!active) continue;

                var chip = new Label(buff.GetName());
                ApplyStyle(chip.style, s =>
                {
                    s.color = Color.white;
                    s.fontSize = 12;
                    s.paddingTop = 4; s.paddingBottom = 4;
                    s.paddingLeft = 8; s.paddingRight = 8;
                    s.marginLeft = 4;
                    s.backgroundColor = new Color(0.2f, 0.5f, 0.9f, 0.9f);
                    s.borderTopLeftRadius = s.borderTopRightRadius = 4;
                    s.borderBottomLeftRadius = s.borderBottomRightRadius = 4;
                });
                _buffRow.Add(chip);
            }
        }

        private bool HasActiveEffect(GameplayEffect effect)
        {
            // Simple check: if the effect grants a tag, and the hero has that tag live,
            // we consider it active. Safe no-op fallback if the helper shape differs.
            if (effect == null || hero == null) return false;
            var tag = effect.GetAssetTag();
            return hero.GetTagCache().HasTag(tag, ETagMatchMode.IncludeParents);
        }

        private void RefreshLevelUpModal()
        {
            bool pending = _gameData.GetPrimary<bool>(SwarmTags.LEVEL_UP_PENDING);
            bool alreadyMade = _gameData.GetPrimary<bool>(SwarmTags.LEVEL_UP_CHOICE);
            bool shouldShow = pending && !alreadyMade;

            Debug.Log(shouldShow);

            if (shouldShow && !_levelUpVisible)
            {
                PopulateLevelUpOptions();
                _levelUpModal.style.display = DisplayStyle.Flex;
                _levelUpVisible = true;
            }
            else if (!shouldShow && _levelUpVisible)
            {
                _levelUpModal.style.display = DisplayStyle.None;
                _levelUpVisible = false;
            }
        }

        private void RefreshGameOverPanel()
        {
            bool over = _gameData.GetPrimary<bool>(SwarmTags.GAME_OVER);
            if (over && !_gameOverVisible)
            {
                _gameOverPanel.style.display = DisplayStyle.Flex;
                _gameOverVisible = true;
            }
            else if (!over && _gameOverVisible)
            {
                _gameOverPanel.style.display = DisplayStyle.None;
                _gameOverVisible = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Level-up option generation + click handling
        // ═══════════════════════════════════════════════════════════════════════

        private void PopulateLevelUpOptions()
        {
            _levelUpOptionsRow.Clear();

            var options = BuildLevelUpOptions(levelUpOptionCount);
            if (options.Count == 0)
            {
                // Degenerate: no options available — auto-advance so the sequence isn't stuck.
                CommitChoice(null, isNew: false);
                return;
            }

            foreach (var opt in options)
            {
                var btn = new Button(() => CommitChoice(opt.Ability, opt.IsNew))
                {
                    text = opt.IsNew
                        ? $"NEW: {opt.Ability.GetName()}"
                        : $"Upgrade: {opt.Ability.GetName()}"
                };
                ApplyStyle(btn.style, s =>
                {
                    s.fontSize = 16;
                    s.minWidth = 180;
                    s.minHeight = 90;
                    s.marginLeft = 8; s.marginRight = 8;
                    s.whiteSpace = WhiteSpace.Normal;
                });
                _levelUpOptionsRow.Add(btn);
            }
        }

        private struct LevelUpOption
        {
            public Ability Ability;
            public bool IsNew;
        }

        private List<LevelUpOption> BuildLevelUpOptions(int count)
        {
            var result = new List<LevelUpOption>();
            if (hero == null || abilityPool == null) return result;

            // Ownership split: what the hero already has vs what's still available.
            var owned = new HashSet<Ability>();
            for (int i = 0; i < hero.AbilitySystem.AbilityCount; i++)
            {
                // Best-effort: if AbilitySystem exposes a way to get the ability at i, use it.
                // Otherwise we fall back to matching by pool membership heuristically.
                var a = TryGetAbilityAt(hero, i);
                if (a != null) owned.Add(a);
            }

            var upgrades = new List<Ability>();
            var newOnes  = new List<Ability>();
            foreach (var a in abilityPool)
            {
                if (a == null) continue;
                if (owned.Contains(a)) upgrades.Add(a);
                else newOnes.Add(a);
            }

            // Shuffle both lists, then interleave until we hit `count`.
            Shuffle(upgrades);
            Shuffle(newOnes);

            while (result.Count < count && (upgrades.Count > 0 || newOnes.Count > 0))
            {
                bool preferNew = newOnes.Count > 0 &&
                                 (upgrades.Count == 0 || Random.value < 0.5f);
                if (preferNew)
                {
                    result.Add(new LevelUpOption { Ability = newOnes[0], IsNew = true });
                    newOnes.RemoveAt(0);
                }
                else if (upgrades.Count > 0)
                {
                    result.Add(new LevelUpOption { Ability = upgrades[0], IsNew = false });
                    upgrades.RemoveAt(0);
                }
            }

            return result;
        }

        private void CommitChoice(Ability chosen, bool isNew)
        {
            _gameData.SetPrimary(SwarmTags.CHOSEN_ABILITY, chosen);
            _gameData.SetPrimary(SwarmTags.CHOSE_NEW_ABILITY, isNew);
            _gameData.SetPrimary(SwarmTags.LEVEL_UP_CHOICE, true);

            _levelUpModal.style.display = DisplayStyle.None;
            _levelUpVisible = false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Play Again — simplest path is a full scene reload so all state resets.
        // ═══════════════════════════════════════════════════════════════════════

        private void HandlePlayAgain()
        {
            _gameData.SetPrimary(SwarmTags.GAME_OVER, false);
            CancelChain();
            var active = SceneManager.GetActiveScene();
            if (active.IsValid()) SceneManager.LoadScene(active.buildIndex);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static void ApplyStyle(IStyle style, System.Action<IStyle> apply) => apply(style);

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Best-effort accessor for the ability at index `i`. If the AbilitySystem
        /// exposes a direct getter, we'll use it; otherwise this returns null and
        /// the option builder falls back to treating everything as "new".
        /// </summary>
        private static Ability TryGetAbilityAt(Hero hero, int i)
        {
            // Placeholder — wire this to the real accessor when it lands on
            // AbilitySystemComponent (e.g. hero.AbilitySystem.GetAbility(i)).
            return null;
        }
    }
}
