using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Examples
{
    public class DemoManager : LazyMonoProcess
    {
        public static DemoManager Instance;

        public GameplayAbilitySystem DemoEnemy;
        
        public UIDocument DemoUI;
        private VisualElement Root;
        
        public Color PlayerColor = ColorUtility.TryParseHtmlString("#05FF00", out var color) ? color : Color.cyan;
        
        public Material GenMat;
        public Color ObjectColor = Color.magenta;
        
        // -----

        public GameplayAbilitySystem Player;
        public static DemoInputConduit Input;

        // public EntityIdentity EnemyIdentity;
        private GameplayAbilitySystem enemy;
        private ProcessRelay enemyRelay;

        /// <summary>
        /// Full-screen overlay container for sequence-driven UI.
        /// Sequences add/remove their VisualElements here.
        /// Add a VisualElement named "SequenceOverlay" to your UXML.
        /// </summary>
        public VisualElement SequenceOverlay { get; private set; }
        
        private ProcessRelay camLock;
        private int uiLock = 0;
        
        private VisualElement Nav;
        private VisualElement Scenarios;
        private VisualElement Sequences;
        private VisualElement Game;
        private VisualElement GameControls;

        private HashSet<VisualElement> tempOpens = new();

        private Camera cMain;
        private Vector3 cPos;
        private Vector3 defCamPos;
        private Quaternion cRot;

        private Dictionary<Button, bool> canActivate = new();

        private HashSet<ProcessRelay> activeRelays = new HashSet<ProcessRelay>();

        private List<Button> abilityButtons = new();
        
        public override void WhenInitialize(ProcessRelay relay)
        {
            Instance = this;
            DemoSequences.GenMat = GenMat;
            
            base.WhenInitialize(relay);

            Input ??= GetComponent<DemoInputConduit>() ?? gameObject.AddComponent<DemoInputConduit>();
            Player.GetComponent<Outline>().OutlineColor = PlayerColor;
            
            Root = DemoUI.rootVisualElement;

            cMain = Camera.main;
            cPos = cMain.transform.position;
            defCamPos = cPos;
            cRot = cMain.transform.rotation;

            InitDemo();
        }

        private void InitDemo()
        {
            Nav = Root.Q("Nav");
            Scenarios = Root.Q("Scenarios");
            Sequences = Root.Q("Sequences");
            Game = Root.Q("Game");
            
            // Create or find the sequence overlay
            SequenceOverlay = Game.Q("SequenceOverlay");
            if (SequenceOverlay == null)
            {
                SequenceOverlay = new VisualElement { name = "SequenceOverlay" };
                SequenceOverlay.pickingMode = PickingMode.Ignore;
                SequenceOverlay.style.position = Position.Absolute;
                SequenceOverlay.style.left = 0; SequenceOverlay.style.top = 0;
                SequenceOverlay.style.right = 0; SequenceOverlay.style.bottom = 0;
                Game.Add(SequenceOverlay);
            }
            SequenceOverlay.style.display = DisplayStyle.None;
            
            InitNav();

            InitAbilityScenarios();
            InitSequences();
            InitGameControls();

            Scenarios.style.display = DisplayStyle.None;
            Sequences.style.display = DisplayStyle.None;
        }

        void InitNav()
        {
            var scenariosBtn = Nav.Q<Button>("ScenariosButton");
            scenariosBtn.clicked += () => ToggleScenarios();
    
            var sequencesBtn = Nav.Q<Button>("SequencesButton");
            sequencesBtn.clicked += () => ToggleSequences();
    
            ConfigureButton(Nav.Q<Button>("ControlsButton"), null, closeTemps: true);
            ConfigureButton(Nav.Q<Button>("AboutButton"), null, closeTemps: true);
            ConfigureButton(Nav.Q<Button>("QuitButton"), null, closeTemps: true);
        }

        void InitGameControls()
        {
            GameControls = Root.Q("GameControls");
            var p = GameControls.Q("InGameProcesses");
            
            InitInGameProcesses();

            return;
            
            void InitInGameProcesses()
            {
                ConfigureButton(p.Q<Button>("Bob"), null, () =>
                {
                    var d = new SyncDemoData(typeof(SyncDemo_Bob), go =>
                    {
                        var t = go.AddComponent(typeof(SyncDemo_Bob)) as SyncDemo_Bob;
                    });
                    if (ToggleSyncProcessType(d)) p.Q<Button>("Bob").style.backgroundColor = (Color.green * .54f);
                    else p.Q<Button>("Bob").style.backgroundColor = Color.black * .54f;
                    return null;
                });     
                ConfigureButton(p.Q<Button>("Scale"), null, () =>
                {
                    var d = new SyncDemoData(typeof(SyncDemo_PulseScale), go =>
                    {
                        var t = go.AddComponent(typeof(SyncDemo_PulseScale)) as SyncDemo_PulseScale;
                    });
                    if (ToggleSyncProcessType(d)) p.Q<Button>("Scale").style.backgroundColor = (Color.green * .54f);
                    else p.Q<Button>("Scale").style.backgroundColor = Color.black * .54f;
                    return null;
                });  
                /*ConfigureButton(p.Q<Button>("Orbit"), null, () =>
                {
                    DemoSequences.ToggleSyncProcessType(typeof(SyncDemo_Bob));
                    return null;
                });  
                ConfigureButton(p.Q<Button>("Patrol"), null, () =>
                {
                    DemoSequences.ToggleSyncProcessType(typeof(SyncDemo_Patrol));
                    return null;
                });  
                ConfigureButton(p.Q<Button>("Chase"), null, () =>
                {
                    DemoSequences.ToggleSyncProcessType(typeof(SyncDemo_ChaseTarget));
                    return null;
                }); */ 
            }
        }
        
        public static List<SyncDemoData> primProcesses = new List<SyncDemoData>();

        private bool ToggleSyncProcessType(SyncDemoData data)
        {
            if (!data.Type.IsInstanceOfType(typeof(Component))) return false;

            for (int i = 0; i < primProcesses.Count; i++)
            {
                if (primProcesses.Any(p => p.Type == data.Type))
                {
                    primProcesses.RemoveAt(i);
                    return false;
                }
            }

            primProcesses.Add(data);
            return true;
        }

        public class SyncDemoData
        {
            public Type Type;
            public Action<GameObject> toApply;

            public SyncDemoData(Type type, Action<GameObject> toApply)
            {
                Type = type;
                this.toApply = toApply;
            }
        }
        
        private bool _sequencesOpen;
        private bool _scenariosOpen;
        private ProcessRelay _transitionRelay;

        private void ToggleSequences()
        {
            if (_transitionRelay is { ProcessActive: true }) return;
    
            foreach (var ve in tempOpens.ToArray())
            {
                if (ve == Sequences) continue;
                RemoveTemp(ve);
            }
    
            // If scenarios is open, close it first
            if (_scenariosOpen)
            {
                CloseScenarios();
            }
    
            if (!_sequencesOpen)
                OpenSequences();
            else
                CloseSequences();
        }

        private void OpenSequences()
        {
            var sequenceCamPos = defCamPos + new Vector3(-15f, 0f, 0f);
    
            var seq = TaskSequenceBuilder.Create("Open Sequences")
                .Task(async (d, t) =>
                {
                    await SequenceTaskLibrary.MoveTo(cMain.transform, sequenceCamPos, 1f, t);
                    cPos = sequenceCamPos;
                })
                .OnTerminate((_, success) =>
                {
                    if (success)
                    {
                        _sequencesOpen = true;
                        AddTemp(Sequences);
                    }
                })
                .BuildSequence();

            ProcessControl.Register(seq, this, out _transitionRelay);
            camLock = _transitionRelay;
        }

        private void CloseSequences()
        {
            var seq = TaskSequenceBuilder.Create("Close Sequences")
                .Task(async (d, t) =>
                {
                    RemoveTemp(Sequences);
                    await SequenceTaskLibrary.MoveTo(cMain.transform, defCamPos, 1f, t);
                    cPos = defCamPos;
                })
                .OnTerminate((_, _) =>
                {
                    _sequencesOpen = false;
                    camLock = null;
                })
                .BuildSequence();
    
            ProcessControl.Register(seq, this, out _transitionRelay);
            camLock = _transitionRelay;
        }
        
        
        private void ToggleScenarios()
        {
            if (_transitionRelay is { ProcessActive: true }) return;
    
            foreach (var ve in tempOpens.ToArray())
            {
                if (ve == Scenarios) continue;
                RemoveTemp(ve);
            }
    
            // If sequences is open, close it first
            if (_sequencesOpen)
            {
                CloseSequences();
            }
    
            if (!_scenariosOpen)
                OpenScenarios();
            else
                CloseScenarios();
        }
        
        private void OpenScenarios()
        {
            var scenarioCamPos = defCamPos + new Vector3(-15f, 0f, 15f);
            var enemySpawnPos = new Vector3(0f, 1.5f, 45f);
            var patrolA = new Vector3(20f, 1.5f, 45f);
            var patrolB = new Vector3(-20f, 1.5f, 45f);
            float speed = 7.5f;  // units per second
            
            var enemyPatrolSeq = TaskSequenceBuilder.Create("Demo Enemy Patrol")
                .WithRepeat(true)
                .Task(async (d, t) =>
                {
                    var duration = Vector3.Distance(enemy.transform.position, patrolA) / speed;
                    await SequenceTaskLibrary.MoveTo(enemy.transform, patrolA, duration, t);
                })
                .Task(async (d, t) =>
                {
                    var duration = Vector3.Distance(enemy.transform.position, patrolB) / speed;
                    await SequenceTaskLibrary.MoveTo(enemy.transform, patrolB, duration, t);
                })
                .BuildSequence();
    
            var seq = TaskSequenceBuilder.Create("Open Scenarios")
                // Move camera
                .Task(async (d, t) =>
                {
                    await SequenceTaskLibrary.MoveTo(cMain.transform, scenarioCamPos, 1f, t);
                    cPos = scenarioCamPos;
                })
                // Spawn enemy
                .Task(async (d, t) =>
                {
                    var obj = DemoSequences.CreatePrim();
                    obj.transform.position = enemySpawnPos;
                    obj.transform.localScale = Vector3.zero;

                    ProcessControl.Register(DemoEnemy, this, out enemyRelay);
                    enemyRelay.TryGetProcess(out enemy);

                    ProcessControl.Register(enemy.GetComponent<Healthbar>(), enemy, out var healthbarRelay);
                    healthbarRelay.Wrapper.getProcessName = "Demo Enemy Healthbar";
                    
                    /*//enemy = GameplayAbilitySystem.AddToGameObject(obj, EnemyIdentity);
            
                    if (!ProcessControl.Register(enemy, this, out enemyRelay))
                    {
                        Debug.LogError("[Demo] Failed to register enemy GAS");
                        return;
                    }*/

                    enemyRelay.Wrapper.getProcessName = "Demo Enemy";
                    await SequenceTaskLibrary.ScaleTo(enemy.transform, 3f, 0.5f, t);

                    ProcessControl.Register(enemyPatrolSeq, enemy, d, out _);
                })
                .OnTerminate((_, success) =>
                {
                    if (success)
                    {
                        _scenariosOpen = true;
                        AddTemp(Scenarios);
                    }
                })
                .BuildSequence();
    
            ProcessControl.Register(seq, this, out _transitionRelay);
            camLock = _transitionRelay;
        }
        
        
        private void CloseScenarios()
        {
            var seq = TaskSequenceBuilder.Create("Close Scenarios")
                // Shrink and destroy enemy
                .Task(async (d, t) =>
                {
                    RemoveTemp(Scenarios);
                    
                    if (enemy != null)
                    {
                        await SequenceTaskLibrary.ScaleTo(enemy.transform, 0f, 0.3f, t);
                
                        if (enemyRelay is { ProcessActive: true })
                            ProcessControl.Instance.TerminateImmediate(enemyRelay.CacheIndex);
                
                        Object.Destroy(enemy.gameObject);
                        enemy = null;
                        enemyRelay = null;
                    }
                })
                // Return camera
                .Task(async (d, t) =>
                {
                    await SequenceTaskLibrary.MoveTo(cMain.transform, defCamPos, 1f, t);
                    cPos = defCamPos;
                })
                .OnTerminate((_, _) =>
                {
                    _scenariosOpen = false;
                    camLock = null;
                })
                .BuildSequence();
    
            ProcessControl.Register(seq, this, out _transitionRelay);
            camLock = _transitionRelay;
        }
        
        void InitAbilityScenarios()
        {
            foreach (var c in Scenarios.Children().ToArray())
            {
                Scenarios.Remove(c);
            }
            
            foreach (var container in Player.AbilitySystem.GetAbilityContainers())
            {
                var button = CreateAbilityButton(container.Spec.Base);
                Scenarios.Add(button);
                ConfigureButton(button, null, () => null, onClick: () =>
                {
                    var req = Player.AbilitySystem.CreateActivationRequest(container.Index);
                    bool activated = Player.AbilitySystem.TryActivateAbility(req);

                    if (!activated) return;
                    
                    // var watcher = TaskSequenceProcess.Register(AbilityButtonWatcher(button, container));
                });
                abilityButtons.Add(button);
            }
        }
        
        void InitSequences()
        {
            ConfigureButton(Sequences.Q<Button>("BouncyBalls"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.BouncyBalls());
            });
            ConfigureButton(Sequences.Q<Button>("GTACharacterChange"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.CharacterChangeCameraEffect());
            }, holdCam: true);
            ConfigureButton(Sequences.Q<Button>("Smite"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.Smite());
            });
            ConfigureButton(Sequences.Q<Button>("Torrents"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.TorrentStorm());
            });
            ConfigureButton(Sequences.Q<Button>("MovingPlatforms"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.MovingPlatforms());
            });
            ConfigureButton(Sequences.Q<Button>("DayNightCycle"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.DayNightCycle());
            });
            ConfigureButton(Sequences.Q<Button>("ProgressBar"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.ProgressBar());
            });
            ConfigureButton(Sequences.Q<Button>("AimLab"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.AimLab());
            });
            ConfigureButton(Sequences.Q<Button>("Tendrils"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.TendrilsOfDestruction());
            });
            
            // UI
            
            ConfigureButton(Sequences.Q<Button>("SlowReveal"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.SlowReveal());
            }, blockReActivate: true, holdUI: true);
            ConfigureButton(Sequences.Q<Button>("BoringSpeech"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.BoringSpeech());
            }, blockReActivate: true, holdUI: true);
            ConfigureButton(Sequences.Q<Button>("RadialMenu"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.RadialMenu());
            }, blockReActivate: true, holdUI: true);
            ConfigureButton(Sequences.Q<Button>("Notification"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.Notification());
            }, blockReActivate: true, holdUI: true);
            ConfigureButton(Sequences.Q<Button>("LoadingScreen"), null, () =>
            {
                return RegisterDemoSequence(DemoSequences.LoadingScreen());
            }, blockReActivate: true, holdUI: true);
        }

        private Button CreateAbilityButton(Ability ability, int labelSize = 14, int borderWidth = 1)
        {
            // <ui:Button text="Fireball" name="Fireball" focusable="false" style="margin-bottom: 12px; -unity-text-align: middle-left; background-color: rgba(188, 188, 188, 0); -unity-font-style: bold; font-size: 18px; min-width: 120px;">
            //  <ui:Label text="?" name="Info" style="align-self: flex-end; max-width: 14px; min-width: 14px; min-height: 14px; max-height: 14px; -unity-text-align: middle-right; border-left-color: rgb(0, 0, 0); border-right-color: rgb(0, 0, 0); border-top-color: rgb(0, 0, 0); border-bottom-color: rgb(0, 0, 0); border-top-width: 1px; border-right-width: 1px; border-bottom-width: 1px; border-left-width: 1px; margin-top: 0; margin-right: 0; margin-bottom: 0; margin-left: 0; padding-top: 8px; padding-bottom: 8px; padding-right: 4px; padding-left: 12px;" />
            // </ui:Button>

            var button = new Button();
            button.text = ability.GetName();
            button.focusable = false;

            button.style.marginBottom = 12;
            button.style.minWidth = 120;
                
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.backgroundColor = new Color(188 / 255f, 188 / 255f, 188 / 255f, 0f);
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 18;

            var label = new Label("?");
            
            label.style.maxWidth = labelSize;
            label.style.minWidth = labelSize;
            label.style.maxHeight = labelSize;
            label.style.minHeight = labelSize;
            
            label.style.alignSelf = Align.FlexEnd;
            label.style.unityTextAlign = TextAnchor.MiddleRight;

            var bColor = new Color(0, 0, 0);
            label.style.borderBottomColor = bColor;
            label.style.borderLeftColor = bColor;
            label.style.borderRightColor = bColor;
            label.style.borderTopColor = bColor;
            label.style.borderBottomWidth = borderWidth;
            label.style.borderTopWidth = borderWidth;
            label.style.borderLeftWidth = borderWidth;
            label.style.borderRightWidth = borderWidth;

            button.Add(label);

            Attach(label, ability.GetDescription(), Root);

            return button;
        }

        private void ConfigureButton(Button button, VisualElement next, Func<ProcessRelay> action = null, bool closeTemps = false, bool holdCam = false, bool blockReActivate = false, bool holdUI = false, Action onClick = null)
        {
            bool activeButton = false;
            
            button.clicked += () =>
            {
                onClick?.Invoke();
                
                if (canActivate.TryGetValue(button, out bool able) && !able) return;
                
                // UI stuff
                if (closeTemps)
                {
                    foreach (var ve in tempOpens.ToArray())
                    {
                        if (ve == next) continue;
                        RemoveTemp(ve);
                    }
                }
                
                if (next is not null) ToggleTemp(next);
                
                // Cam lock validation
                if ((camLock?.ProcessActive ?? false) && holdCam)
                {
                    Debug.LogWarning($"[Demo] Cant start process because camLock is claimed by: {camLock.Wrapper.ProcessName}", this);
                    return;
                }
                
                // Register the sequence process
                var relay = action?.Invoke() ?? null;
                if (relay is null) return;
                
                relay.Wrapper.Data.SetPrimary(Tags.CAMERA, holdCam);
                if (holdCam) camLock = relay;
                if (holdUI) uiLock += 1;

                // Active button configure
                activeButton = true;
                canActivate[button] = !blockReActivate;

                activeRelays.Add(relay);
                
                var watcher = WatchSequence(relay);
                ProcessControl.Register(watcher, relay.Handler, out _);
            };
            
            button.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (activeButton) return;
                button.style.backgroundColor = new Color(.3f, .3f, .3f, .3f);
            });
            
            button.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                if (activeButton) return;
                button.style.backgroundColor = Color.clear;
            });

            TaskSequence WatchSequence(ProcessRelay relay)
            {
                return TaskSequenceBuilder.Create($"Process Watcher ({relay.Wrapper.ProcessName})")
                    .Task(async (d, t) =>
                    {
                        if (d.GetPrimary<bool>(Tags.CAMERA)) camLock = relay;
                        
                        button.style.backgroundColor = (Color.green * .54f);
                        await UniTask.WaitUntil(() => !relay.ProcessActive, cancellationToken: t);
                    })
                    .OnTerminate((_, _) =>
                    {
                        activeRelays.Remove(relay);
                        
                        activeButton = false;
                        button.style.backgroundColor = Color.clear;

                        canActivate[button] = true;
                        
                        if (relay == camLock || camLock is null)
                        {
                            ResetCamera();
                            camLock = null;
                        }

                        if (holdUI)
                        {
                            uiLock -= 1;
                            if (uiLock <= 0)
                            {
                                SequenceOverlay.style.display = DisplayStyle.None;
                                uiLock = 0;
                            }
                        }
                    })
                    .BuildSequence();
            }
        }

        private ProcessRelay RegisterDemoSequence(TaskSequence seq)
        {
            var data = SequenceDataPacket.SceneLocal(Player.transform);
            data.SetPrimary(Tags.DATA, Player);

            Debug.Log($"Registering demo sequence!!");
            
            ProcessControl.Register(seq, this, data, out var relay);
            // var relay = TaskSequenceProcess.Register(seq, data, this);

            return relay;
        }
        
        private ProcessRelay RegisterDemoSequence(TaskSequenceChain chain)
        {
            var data = SequenceDataPacket.SceneLocal(Player.transform);
            data.SetPrimary(Tags.DATA, Player);
            
            ProcessControl.Register(chain, this, data, out var relay);
            // var relay = TaskSequenceProcess.Register(chain, data);

            return relay;
        }
        
        private void ResetCamera()
        {
            cMain.transform.position = cPos;
            cMain.transform.rotation = cRot;
        }
        
        private void ToggleTemp(VisualElement ve)
        {
            if (tempOpens.Contains(ve)) RemoveTemp(ve);
            else AddTemp(ve);
        }
        
        private void AddTemp(VisualElement ve)
        {
            if (!tempOpens.Add(ve)) return;
            ve.style.display = DisplayStyle.Flex;
        }
        
        private void RemoveTemp(VisualElement ve)
        {
            if (!tempOpens.Remove(ve)) return;
            ve.style.display = DisplayStyle.None;
        }
        
        internal class TooltipConfig
        {
            public float FontSize = 12f;
            public Color TextColor = Color.white;
            public Color BackgroundColor = new(0.08f, 0.08f, 0.1f, 0.94f);
            public Color BorderColor = new(0.3f, 0.3f, 0.35f);
            public float BorderWidth = 1f;
            public float BorderRadius = 5f;
            public float PaddingH = 10f;
            public float PaddingV = 6f;
            public float MaxWidth = 260f;
            public float OffsetX = 14f;
            public float OffsetY = 18f;
            public float FadeInDuration = 0.08f;
            public float ShowDelay = 0.25f;
        }
        
        /// <summary>
        /// Attaches a tooltip to the source element. The tooltip is added to the provided
        /// container (typically a full-screen overlay) so it can float freely.
        /// Returns the tooltip VisualElement in case you need to remove it manually.
        /// </summary>
        internal static VisualElement Attach(VisualElement source, string text, VisualElement container,
            Action<TooltipConfig> configure = null)
        {
            var cfg = new TooltipConfig();
            configure?.Invoke(cfg);
            
            // Build tooltip
            var tooltip = new VisualElement();
            tooltip.name = "Tooltip";
            tooltip.pickingMode = PickingMode.Ignore;
            tooltip.style.position = Position.Absolute;
            tooltip.style.backgroundColor = cfg.BackgroundColor;
            tooltip.style.borderTopLeftRadius = cfg.BorderRadius;
            tooltip.style.borderTopRightRadius = cfg.BorderRadius;
            tooltip.style.borderBottomLeftRadius = cfg.BorderRadius;
            tooltip.style.borderBottomRightRadius = cfg.BorderRadius;
            tooltip.style.borderLeftWidth = cfg.BorderWidth;
            tooltip.style.borderRightWidth = cfg.BorderWidth;
            tooltip.style.borderTopWidth = cfg.BorderWidth;
            tooltip.style.borderBottomWidth = cfg.BorderWidth;
            tooltip.style.borderLeftColor = cfg.BorderColor;
            tooltip.style.borderRightColor = cfg.BorderColor;
            tooltip.style.borderTopColor = cfg.BorderColor;
            tooltip.style.borderBottomColor = cfg.BorderColor;
            tooltip.style.paddingLeft = cfg.PaddingH;
            tooltip.style.paddingRight = cfg.PaddingH;
            tooltip.style.paddingTop = cfg.PaddingV;
            tooltip.style.paddingBottom = cfg.PaddingV;
            tooltip.style.maxWidth = cfg.MaxWidth;
            tooltip.style.opacity = 0;
            tooltip.style.display = DisplayStyle.None;
            
            var label = new Label(text);
            label.pickingMode = PickingMode.Ignore;
            label.style.fontSize = cfg.FontSize;
            label.style.color = cfg.TextColor;
            label.style.whiteSpace = WhiteSpace.Normal;
            tooltip.Add(label);
            
            container.Add(tooltip);
            
            // State
            IVisualElementScheduledItem delayedShow = null;
            bool hovering = false;
            
            source.RegisterCallback<PointerEnterEvent>(evt =>
            {
                hovering = true;
                
                // Delayed show
                delayedShow = source.schedule.Execute(() =>
                {
                    if (!hovering) return;
                    tooltip.style.display = DisplayStyle.Flex;
                    tooltip.style.opacity = 1f;
                    PositionTooltip(tooltip, container, evt.position, cfg);
                }).StartingIn((long)(cfg.ShowDelay * 1000f));
            });
            
            source.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (tooltip.resolvedStyle.display == DisplayStyle.None) return;
                PositionTooltip(tooltip, container, evt.position, cfg);
            });
            
            source.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                hovering = false;
                delayedShow?.Pause();
                tooltip.style.display = DisplayStyle.None;
                tooltip.style.opacity = 0;
            });
            
            // Auto-cleanup when source is removed from panel
            source.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                tooltip.RemoveFromHierarchy();
            });
            
            return tooltip;
        }
        
        /// <summary>
        /// Updates the text of an existing tooltip (returned from Attach).
        /// </summary>
        public static void SetText(VisualElement tooltip, string text)
        {
            var label = tooltip.Q<Label>();
            if (label != null) label.text = text;
        }
        
        private static void PositionTooltip(VisualElement tooltip, VisualElement container,
            Vector2 pointerPos, TooltipConfig cfg)
        {
            // Convert pointer position to container-local coordinates
            var containerRect = container.worldBound;
            float x = pointerPos.x - containerRect.x + cfg.OffsetX;
            float y = pointerPos.y - containerRect.y + cfg.OffsetY;
            
            // Clamp to stay within container bounds
            float tooltipWidth = tooltip.resolvedStyle.width;
            float tooltipHeight = tooltip.resolvedStyle.height;
            
            if (tooltipWidth > 0 && tooltipHeight > 0)
            {
                if (x + tooltipWidth > containerRect.width)
                    x = pointerPos.x - containerRect.x - tooltipWidth - cfg.OffsetX;
                if (y + tooltipHeight > containerRect.height)
                    y = pointerPos.y - containerRect.y - tooltipHeight - cfg.OffsetY;
                
                x = Mathf.Max(0, x);
                y = Mathf.Max(0, y);
            }
            
            tooltip.style.left = x;
            tooltip.style.top = y;
        }
    }
}