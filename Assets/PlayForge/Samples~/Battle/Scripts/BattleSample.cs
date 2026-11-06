using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Battles
{
    /// <summary>
    /// Plays battles, one after another: Blue and Red, eight fighters a side, each fighting with a mind of its own until one
    /// side stands. When the scene has no ProcessControl, it brings PlayForge up itself, as a Bootstrapper does, so the
    /// scene needs nothing else. It frames the main camera on the field.
    /// </summary>
    public sealed class BattleSample : MonoBehaviour
    {
        [Tooltip("A battle's seed, which makes its battle the same each time. 0 makes each battle a new one")]
        public int Seed;

        [Tooltip("Seconds between a battle's end and the next one's start")]
        [Min(0f)] public float SecondsBetweenBattles = 4f;

        [Tooltip("Seconds a battle may last before the next one starts, whoever stands")]
        [Min(1f)] public float LongestBattle = 150f;

        [Tooltip("Frames the main camera on the field as the scene starts")]
        public bool FrameCamera = true;

        /// <summary>The battle playing now.</summary>
        public Battle Battle { get; private set; }

        /// <summary>How many battles have ended.</summary>
        public int Fought { get; private set; }

        private BattleAssets assets, rootAssets;
        private BattleWorld world;
        private GameObject templates;
        private float overFor;

        private void Start()
        {
            BringUpPlayForge();
            if (FrameCamera) Frame(Camera.main);
            Begin();
        }

        private void Update()
        {
            if (Battle == null) return;
            Battle.Step(ForgeTime.DeltaTime);
            if (!Battle.Over && Battle.Elapsed < LongestBattle) return;

            overFor += Time.deltaTime;
            if (overFor < SecondsBetweenBattles) return;
            End();
            Fought++;
            Begin();
        }

        private void OnDestroy()
        {
            End();
            if (templates) Destroy(templates);
            rootAssets?.DestroyAll();
        }

        private void Begin()
        {
            assets = new BattleAssets();
            world = new BattleWorld(assets);
            Battle = new Battle(world, assets, Seed != 0 ? Seed : Environment.TickCount);
            overFor = 0f;
        }

        private void End()
        {
            Battle?.Dispose();
            world?.Dispose();
            Battle = null;
            world = null;
            assets = null;
        }

        // PlayForge's ProcessControl and GameRoot, brought up as a Bootstrapper brings them up, from a GameRoot made here
        private void BringUpPlayForge()
        {
            if (ProcessControl.Instance && GameRoot.Instance) return;

            // The GameRoot to clone, on an object in no scene and not active, as a prefab is
            rootAssets = new BattleAssets();
            templates = new GameObject("PlayForge Templates") { hideFlags = HideFlags.HideAndDontSave };
            templates.SetActive(false);
            var root = new GameObject("Game Root");
            root.transform.SetParent(templates.transform, false);
            var gameRoot = root.AddComponent<GameRoot>();
            gameRoot.EntityData = rootAssets.Identity("Game Root", rootAssets.Set(rootAssets.Element(rootAssets.Attribute("Unused"), 1f, 1f)));
            gameRoot.ProcessLifecycle = EProcessLifecycle.Synchronous;
            gameRoot.ProcessTiming = EProcessStepTiming.UpdateAndLate;

            // A Bootstrapper brings PlayForge up as it wakes, so it's filled in before it's switched on
            var host = new GameObject("Bootstrapper");
            host.SetActive(false);
            var bootstrapper = host.AddComponent<Bootstrapper>();
            bootstrapper.GameRootPrefab = gameRoot;
            bootstrapper.RegisterSets = new List<AttributeSet>();
            host.SetActive(true);
        }

        // The whole field in view, from above its middle and a little behind
        private static void Frame(Camera eye)
        {
            if (eye == null) return;
            eye.transform.position = new Vector3(0f, 21f, -19f);
            eye.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
            eye.fieldOfView = 50f;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = new Color(0.1f, 0.11f, 0.12f);
        }
    }
}
