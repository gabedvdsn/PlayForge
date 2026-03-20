using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Examples
{
    public class DemoManager : LazyMonoProcess
    {
        public static DemoManager Instance;
        
        public UIDocument DemoUI;
        private VisualElement Root;

        public GameplayAbilitySystem Player;
        public Color PlayerColor = ColorUtility.TryParseHtmlString("#05FF00", out var color) ? color : Color.cyan;
        
        public Material GenMat;
        public Color ObjectColor = Color.magenta;
        
        // -----

        public Dictionary<string, int> buttonHighlightLocks = new();
        
            
        private VisualElement Nav;
        private VisualElement Scenarios;
        private VisualElement Sequences;
        private VisualElement Game;

        private HashSet<VisualElement> tempOpens = new();

        private Camera cMain;
        private Vector3 cPos;
        private Quaternion cRot;
        
        public override void WhenInitialize(ProcessRelay relay)
        {
            Instance = this;
            DemoSequences.GenMat = GenMat;
            
            base.WhenInitialize(relay);
            
            Root = DemoUI.rootVisualElement;

            cMain = Camera.main;
            cPos = cMain.transform.position;
            cRot = cMain.transform.rotation;

            InitDemo();
        }

        private void InitDemo()
        {
            Nav = Root.Q("Nav");
            Scenarios = Root.Q("Scenarios");
            Sequences = Root.Q("Sequences");
            Game = Root.Q("Game");
            
            InitNav();

            InitScenarios();
            InitSequences();

            Scenarios.style.display = DisplayStyle.None;
            Sequences.style.display = DisplayStyle.None;
        }

        void InitNav()
        {
            ConfigureButton(Nav.Q<Button>("ScenariosButton"), Scenarios, closeTemps: true);
            ConfigureButton(Nav.Q<Button>("SequencesButton"), Sequences, closeTemps: true);
            
            ConfigureButton(Nav.Q<Button>("ControlsButton"), null, closeTemps: true);
            ConfigureButton(Nav.Q<Button>("AboutButton"), null, closeTemps: true);
            ConfigureButton(Nav.Q<Button>("QuitButton"), null, closeTemps: true);
        }
        
        void InitScenarios()
        {
            ConfigureButton(Scenarios.Q<Button>("Fireball"), null, () =>
            {
                return null;
            });
            ConfigureButton(Scenarios.Q<Button>("Shapeshift"), null, () =>
            {
                return null;
            });
            ConfigureButton(Scenarios.Q<Button>("EmpowerAura"), null, () =>
            {
                return null;
            });
            ConfigureButton(Scenarios.Q<Button>("Grenade"), null, () =>
            {
                return null;
            });
            ConfigureButton(Scenarios.Q<Button>("SongOfHealing"), null, () =>
            {
                return null;
            });
            ConfigureButton(Scenarios.Q<Button>("ChanneledArrow"), null, () =>
            {
                return null;
            });
            ConfigureButton(Scenarios.Q<Button>("Teleport"), null, () =>
            {
                return null;
            });
        }
        
        void InitSequences()
        {
            ConfigureButton(Sequences.Q<Button>("TorrentStorm"), null, () =>
            {
                return RegisterSequence(DemoSequences.TorrentStorm2());
            });
            ConfigureButton(Sequences.Q<Button>("GTACharacterChange"), null, () =>
            {
                return RegisterSequence(DemoSequences.CharacterChangeCameraEffect());
            });
            ConfigureButton(Sequences.Q<Button>("Smite"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("MovingPlatforms"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("DayNightCycle"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("TrapTriggering"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("AimLab"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("SlowReveal"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("BoringSpeech"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("RadialMenu"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("Notification"), null, () =>
            {
                return null;
            });
            ConfigureButton(Sequences.Q<Button>("LoadingScreen"), null, () =>
            {
                return null;
            });
        }

        private void ConfigureButton(Button button, VisualElement next, Func<ProcessRelay> action = null, bool closeTemps = false)
        {
            bool activeButton = false;
            
            button.clicked += () =>
            {
                if (closeTemps)
                {
                    foreach (var ve in tempOpens.ToArray())
                    {
                        if (ve == next) continue;
                        RemoveTemp(ve);
                    }
                }

                if (next is not null) ToggleTemp(next);

                var relay = action?.Invoke() ?? null;
                if (relay is null) return;

                activeButton = true;
                var watcher = WatchSequence(relay);
                var watcherRelay = TaskSequenceProcess.Register(watcher);
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
                        button.style.backgroundColor = (Color.green * .54f);
                        await UniTask.WaitUntil(() => !relay.ProcessActive, cancellationToken: t);
                    })
                    .OnTerminate((_, _) =>
                    {
                        activeButton = false;
                        button.style.backgroundColor = Color.clear;
                        ResetCamera();
                    })
                    .BuildSequence();
            }
        }

        private ProcessRelay RegisterSequence(TaskSequence seq)
        {
            var data = SequenceDataPacket.RootDefault(Player);
            data.SetPrimary(Tags.DATA, Player);
            return TaskSequenceProcess.Register(seq, data);
        }
        
        private void ResetCamera()
        {
            cMain.transform.position = cPos;
            cMain.transform.rotation = cRot;
        }
        
        private void ToggleTemp(VisualElement ve)
        {
            Debug.Log($"Toggle temp {ve.name}. Contains: {tempOpens.Contains(ve)}");
            
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
        
    }
}
