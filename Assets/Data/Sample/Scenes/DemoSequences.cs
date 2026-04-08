using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using static FarEmerald.PlayForge.SequenceTaskLibrary;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;


namespace FarEmerald.PlayForge.Examples
{
    public static class DemoSequences
    {
        public static Material GenMat = null;


        internal static GameObject Scale(this GameObject obj, Vector3 scale)
        {
            var _s = obj.transform.localScale;
            var s = new Vector3(_s.x * scale.x, _s.y * scale.y, _s.z * scale.z);
            obj.transform.localScale = s;
            return obj;
        }
        
        public static GameObject CreatePrim(PrimitiveType type = PrimitiveType.Cube, bool withPos = true, bool withMat = true, bool withOutline = true)
        {
            var obj = GameObject.CreatePrimitive(type);
            if (GenMat && withMat) obj.GetComponent<MeshRenderer>().material = GenMat;
            if (withOutline)
            {
                var outline = obj.AddComponent<Outline>();
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.OutlineColor = DemoManager.Instance.ObjectColor;
            }

            if (withPos)
            {
                obj.transform.position = DemoManager.Instance.Player.transform.position;
            }

            foreach (var t in DemoManager.primProcesses)
            {
                t.toApply.Invoke(obj);
            }
            
            return obj;
        }
        
        private static void SetColor(GameObject obj, Color color)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            
            var mat = renderer.material;
            mat.color = color;
        }

        
        
        #region Torrent Storm
        
        public static TaskSequence TorrentStorm(AnimationCurve c = null)
        {
            var D = Tag.GenerateAsUnique("Duration of Storm");
            var R = Tag.GenerateAsUnique("Storm Radius");
            
            var N = Tag.GenerateAsUnique("Number of Torrents");
            var tD = Tag.GenerateAsUnique("Duration of Torrent");
            var yD = Tag.GenerateAsUnique("Torrent Y Delta");
            
            var stormSequence = TaskSequenceBuilder.Create("Torrent Storm")
                .Task(d =>
                {
                    d.SetPrimary(D, 10f);
                    d.SetPrimary(R, 7.5f);
                    d.SetPrimary(N, 15);
                    d.SetPrimary(tD, 2f);
                    d.SetPrimary(yD, 15f);
                    
                    d.SetPrimary(Tags.ITERATIONS, d.GetPrimary<int>(N));
                })
                .Task(d => { })
                .Stage(s => s
                    .WithName("Torrent Storm")
                    .WithDescription("Create a storm of torrents around (0, 0, 0)")
                    .WithRepeat(true)
                    .Task(d =>
                    {
                        if (d.GetPrimary<int>(Tags.ITERATIONS) <= 0)
                        {
                            d.Inject(BreakStageRepeatInjection.Instance, true);
                            return;
                        }

                        Debug.Log($"Creating a Torrent & Torrent Process!");
                        
                        var obj = CreatePrim(PrimitiveType.Cube);
                        var rPos = Random.insideUnitCircle * d.GetPrimary<float>(R);
                        var pos = new Vector3(rPos.x, 1f, rPos.y);
                        obj.transform.position += pos - obj.transform.position;
                        
                        var data = new SequenceDataPacket(d);
                        data.SetPrimary(Tags.DATA, obj);

                        ProcessControl.Register(TorrentSequence(), DemoManager.Instance, data, out _);
                        
                        d.Decrement(Tags.ITERATIONS);
                    })
                    .Task(async (d, t) =>
                    {
                        var waitDuration = d.GetPrimary<float>(tD) / d.GetPrimary<int>(N);
                        await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: t);
                    }))
                .OnTerminate((ctx, success) =>
                {
                    Debug.Log($"Finished torrent storm! ({success})");
                })
                .BuildSequence();

            return stormSequence;

            TaskSequence TorrentSequence()
            {
                return TaskSequenceBuilder.Create("Torrent")
                    .Task(async (d, t) =>
                    {
                        var torrent = d.GetPrimary<GameObject>(Tags.DATA);
                        var delta = new Vector3(0f, d.GetPrimary<float>(yD), 0f);
                        var duration = d.GetPrimary<float>(tD) * .5f;

                        var moveUp = MoveBy(torrent.transform, delta, duration, t, c);
                        var rotateUp = RotateBy(torrent.transform, delta * 40f, duration, t);
                        var upTasks = new[] { moveUp, rotateUp };
                        await UniTask.WhenAll(upTasks);
                    
                        var moveDown = MoveBy(torrent.transform, -delta, duration, t, c);
                        var rotateDown = RotateBy(torrent.transform, delta * 40f, duration, t);
                        var downTasks = new[] { moveDown, rotateDown };
                        await UniTask.WhenAll(downTasks);
                    })
                    .OnTerminate((ctx, success) =>
                    {
                        var torrent = ctx.Data.GetPrimary<GameObject>(Tags.DATA);
                        Object.Destroy(torrent);
                    })
                    .BuildSequence();
            }
        }
        
        #endregion
        
        #region Bouncy Balls
        
        public static TaskSequence BouncyBalls()
        {
            var N = Tag.GenerateAsUnique("Number of Torrents");
            
            var stormSequence = TaskSequenceBuilder.Create("Torrent Storm")
                .Task(d =>
                {
                    d.SetPrimary(N, 5);
                    
                    d.SetPrimary(Tags.DURATION, 1.5f);
                    d.SetPrimary(Tags.HEIGHT, 25f);
                    d.SetPrimary(Tags.DISTANCE, 15f);
                    
                    d.SetPrimary(Tags.ITERATIONS, d.GetPrimary<int>(N));
                })
                .Stage(s => s
                    .WithName("Torrent Storm")
                    .WithDescription("Create a storm of torrents around (0, 0, 0) bound in a random direction")
                    .WithRepeat(true)
                    .Task(d =>
                    {
                        if (d.GetPrimary<int>(Tags.ITERATIONS) <= 0)
                        {
                            d.Inject(BreakStageRepeatInjection.Instance, true);
                            return;
                        }
                        
                        var torrent = CreatePrim();
                        
                        var torrentData = new SequenceDataPacket(d);
                        torrentData.SetPrimary(Tags.ITERATIONS, 15);
                        torrentData.SetPrimary(Tags.ROTATION, new Vector2(Random.Range(0, 360), Random.Range(0, 360)));
                        torrentData.SetPrimary(Tags.DATA, torrent);

                        ProcessControl.Register(BounceSequence(), DemoManager.Instance, torrentData, out _);
                        
                        d.Decrement(Tags.ITERATIONS);
                    }))
                .BuildSequence();

            return stormSequence;
        }
        
        #endregion
        
        #region GTA V Camera Change
        
        public static TaskSequence CharacterChangeCameraEffect()
        {
            return TaskSequenceBuilder.Create("Character Change Camera Effect")
                .Task(d =>
                {
                    var obj = d.GetPrimary<GameplayAbilitySystem>(Tags.DATA);
                    Camera.main.transform.position = obj.transform.position + new Vector3(0, 10, 0);
                    Camera.main.transform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));    
                    
                    d.SetPrimary(Tags.ITERATIONS, 3);
                    d.AddPayload(Tags.POSITION, obj.transform.position);
                    d.AddPayload(Tags.TARGET_POS, new Vector3(0, 50, 0));
                    d.AddPayload(Tags.DEBUG,
                        (d.GetPrimary<Vector3>(Tags.TARGET_POS).y - d.GetPrimary<Vector3>(Tags.POSITION).y) / d.GetPrimary<int>(Tags.ITERATIONS)
                    );
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(Tags.ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(900, cancellationToken: t);
                        
                        var delta = d.GetPrimary<float>(Tags.DEBUG);
                        await MoveBy(Camera.main.transform, new Vector3(0f, delta, 0f), .2f, t);
                        
                        d.Decrement(Tags.ITERATIONS);
                    }))
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(1800, cancellationToken: t);
                    d.SetPrimary(Tags.ITERATIONS, 3);
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(Tags.ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(500, cancellationToken: t);
                        
                        var rPos = Random.insideUnitCircle * 15f;
                        var pos = d.GetPrimary<Vector3>(Tags.POSITION) + new Vector3(rPos.x, 0f, rPos.y);
                        
                        var delta = pos - Camera.main.transform.position;
                        delta.y = 0f;
                        await MoveBy(Camera.main.transform, delta, 1f, t);

                        d.Decrement(Tags.ITERATIONS);
                    })
                )
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(500, cancellationToken: t);
                    var destination = d.GetPrimary<Vector3>(Tags.TARGET_POS);
                    destination.y = Camera.main.transform.position.y;
                    await MoveTo(Camera.main.transform, destination, .5f, t);
                })
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(1800, cancellationToken: t);
                    d.SetPrimary(Tags.ITERATIONS, 3);
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(Tags.ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(900, cancellationToken: t);
                        
                        var delta = -d.GetPrimary<float>(Tags.DEBUG);
                        await MoveBy(Camera.main.transform, new Vector3(0f, delta, 0f), .2f, t);
                        
                        d.Decrement(Tags.ITERATIONS);
                    }))
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(900, cancellationToken: t);
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Smite!
        
        public static TaskSequence Smite()
        { 
            return TaskSequenceBuilder.Create("Smite!")
                .WithDescription("Select")
                .Task(d =>
                {
                    for (int _ = 0; _ < 5; _++)
                    {
                        var obj = CreatePrim().Scale(Vector3.one * 3f);

                        var data = new SequenceDataPacket(d);
                        data.SetPrimary(Tags.DATA, obj);
                        data.SetPrimary(Tags.ITERATIONS, 1);
                        data.SetPrimary(Tags.DELTA, 10f);
                        data.SetPrimary(Tags.DURATION, 1f);
                        data.SetPrimary(Tags.ROTATION, new Vector2(Random.Range(0, 360), Random.Range(0, 360)));

                        var pos = Random.insideUnitCircle * 10f;
                        pos += new Vector2(5f * Mathf.Sign(pos.x), 5f * Mathf.Sign(pos.y));
                        obj.transform.position += new Vector3(pos.x, 0f, pos.y);
                        
                        ProcessControl.Register(WaitForClick(), DemoManager.Instance, data, out var relay);
                        //TaskSequenceProcess.Register(WaitForClick(), data);
                    }
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Moving Platforms
        
        public static TaskSequence MovingPlatforms()
        { 
            return TaskSequenceBuilder.Create("Moving Platforms")
                .WithDescription("Creates moving platforms that travel along an arc path")
                .Task(d =>
                {
                    d.SetPrimary(Tags.ITERATIONS, 3);
                    d.SetPrimary(Tags.DURATION, 1.5f);
                    d.SetPrimary(Tags.HEIGHT, 15f);
                    d.SetPrimary(Tags.DISTANCE, 8f);
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .Task(async (d, t) =>
                    {
                        if (d.GetPrimary<int>(Tags.ITERATIONS) <= 0)
                        {
                            d.Inject(BreakStageRepeatInjection.Instance);
                            return;
                        }
                        
                        d.Decrement(Tags.ITERATIONS);
                    
                        var obj = CreatePrim().Scale(new Vector3(2f, .3f, 2f));
                        obj.transform.position += new Vector3(20f, 0f, -20f) - obj.transform.position;
                    
                        var platformData = new SequenceDataPacket(d);
                        platformData.SetPrimary(Tags.DATA, obj);
                        platformData.SetPrimary(Tags.TARGET_POS, new Vector3(-20f, 0f, -20f));

                        ProcessControl.Register(PlatformMovement(), DemoManager.Instance, platformData, out _);

                        await UniTask.Delay(1500, cancellationToken: t);
                    }))
                .OnTerminate((ctx, success) =>
                {
                    Debug.Log($"Moving platforms finished ({success})");
                })
                .BuildSequence();

            TaskSequence PlatformMovement()
            {
                return TaskSequenceBuilder.Create("Platform Movement")
                    .Task(async (d, t) =>
                    {
                        // Raise the platform
                        var obj = d.GetPrimary<GameObject>(Tags.DATA);
                        var targetPos = d.GetPrimary<Vector3>(Tags.TARGET_POS);
                        var delta = (targetPos - obj.transform.position) * .2f;
                        delta.y = 7f;

                        await MoveBy(obj.transform, delta, 1f, t);
                    })
                    .Task(async (d, t) =>
                    {
                        // Move platform across
                        var obj = d.GetPrimary<GameObject>(Tags.DATA);
                        var targetPos = d.GetPrimary<Vector3>(Tags.TARGET_POS);
                        var delta = (targetPos - obj.transform.position) * .6f;
                        delta.y = 0f;

                        await MoveBy(obj.transform, delta, 1.5f, t);
                    })
                    .Task(async (d, t) =>
                    {
                        // Lower the platform
                        var obj = d.GetPrimary<GameObject>(Tags.DATA);
                        var targetPos = d.GetPrimary<Vector3>(Tags.TARGET_POS);
                        var delta = (targetPos - obj.transform.position);
                        delta.y = -7f;

                        await MoveBy(obj.transform, delta, 1f, t);
                    })
                    .OnTerminate((ctx, success) =>
                    {
                        Object.Destroy(ctx.Data.GetPrimary<GameObject>(Tags.DATA));
                    })
                    .BuildSequence();
            }
        }
        
        #endregion
        
        #region Day/Night Cycle
        
        /// <summary>
        /// Simulates a day/night cycle by orbiting a "sun" sphere overhead and cycling
        /// ground tile colors from warm daylight to cool night. Repeats continuously.
        /// </summary>
        public static TaskSequence DayNightCycle()
        {
            var SUN = Tag.GenerateAsUnique("Demo.Sun");
            var TILES = Tag.GenerateAsUnique("Demo.Tiles");
            var TIME_OF_DAY = Tag.GenerateAsUnique("Demo.TimeOfDay");
            
            return TaskSequenceBuilder.Create("Day/Night Cycle")
                .Task(d =>
                {
                    // Create sun
                    var sun = CreatePrim(PrimitiveType.Sphere, withOutline: false);
                    sun.transform.localScale = Vector3.one * 2f;
                    sun.transform.position += new Vector3(0f, 20f, -15f);
                    SetColor(sun, Color.yellow);
                    d.SetPrimary(SUN, sun);
                    
                    // Create ground tiles
                    var tiles = new GameObject[25];
                    for (int i = 0; i < 25; i++)
                    {
                        int row = i / 5;
                        int col = i % 5;
                        var tile = CreatePrim(PrimitiveType.Cube, withOutline: false);
                        tile.transform.localScale = new Vector3(3.8f, 0.2f, 3.8f);
                        tile.transform.position += new Vector3((col - 2) * 4f, -0.1f, (row - 2) * 4f);
                        tiles[i] = tile;
                    }
                    d.SetPrimary(TILES, tiles);
                    d.SetPrimary(TIME_OF_DAY, 0f);
                    d.SetPrimary(Tags.ITERATIONS, 3); // Number of full cycles
                })
                // Cycle: sunrise → noon → sunset → night → repeat
                .Stage(s => s
                    .WithName("Day Cycle")
                    .WithRepeat(true)
                    .StopRepeatWhen(dd => dd.GetPrimary<int>(Tags.ITERATIONS) <= 0)
                    
                    // Sunrise → Noon
                    .Task(async (d, t) =>
                    {
                        var sun = d.GetPrimary<GameObject>(SUN);
                        var tiles = d.GetPrimary<GameObject[]>(TILES);
                        
                        await Lerp(2f, null, p =>
                        {
                            // Sun arcs from east low to overhead
                            float angle = Mathf.Lerp(-80f, 0f, p);
                            float rad = angle * Mathf.Deg2Rad;
                            sun.transform.position = new Vector3(Mathf.Sin(rad) * 20f, Mathf.Cos(rad) * 20f, 0f);
                            
                            // Dawn orange → bright white
                            var sunColor = Color.Lerp(new Color(1f, 0.5f, 0.2f), new Color(1f, 0.95f, 0.8f), p);
                            SetColor(sun, sunColor);
                            
                            // Tiles brighten
                            var tileColor = Color.Lerp(new Color(0.3f, 0.25f, 0.4f), new Color(0.6f, 0.7f, 0.5f), p);
                            foreach (var tile in tiles) SetColor(tile, tileColor);
                        }, t);
                    })
                    
                    // Noon → Sunset
                    .Task(async (d, t) =>
                    {
                        var sun = d.GetPrimary<GameObject>(SUN);
                        var tiles = d.GetPrimary<GameObject[]>(TILES);
                        
                        await Lerp(2f, null, p =>
                        {
                            float angle = Mathf.Lerp(0f, 80f, p);
                            float rad = angle * Mathf.Deg2Rad;
                            sun.transform.position = new Vector3(Mathf.Sin(rad) * 20f, Mathf.Cos(rad) * 20f, 0f);
                            
                            var sunColor = Color.Lerp(new Color(1f, 0.95f, 0.8f), new Color(1f, 0.3f, 0.1f), p);
                            SetColor(sun, sunColor);
                            
                            var tileColor = Color.Lerp(new Color(0.6f, 0.7f, 0.5f), new Color(0.4f, 0.25f, 0.2f), p);
                            foreach (var tile in tiles) SetColor(tile, tileColor);
                        }, t);
                    })
                    
                    // Night hold
                    .Task(async (d, t) =>
                    {
                        var sun = d.GetPrimary<GameObject>(SUN);
                        var tiles = d.GetPrimary<GameObject[]>(TILES);
                        
                        SetColor(sun, new Color(0.3f, 0.3f, 0.6f));
                        sun.transform.position = new Vector3(0f, -5f, 0f); // Below horizon
                        foreach (var tile in tiles) SetColor(tile, new Color(0.15f, 0.12f, 0.25f));
                        
                        await UniTask.Delay(1500, cancellationToken: t);
                        d.Decrement(Tags.ITERATIONS);
                    }))
                // Cleanup
                .OnTerminate((ctx, success) =>
                {
                    Object.Destroy(ctx.Data.GetPrimary<GameObject>(SUN));
                    var tiles = ctx.Data.GetPrimary<GameObject[]>(TILES);
                    if (tiles != null) foreach (var tile in tiles) Object.Destroy(tile);
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Trap Triggering
        
        /// <summary>
        /// A corridor of traps that trigger one by one in sequence.
        /// Each trap: idle → warning flash → activate (scale up) → hold → retract.
        /// Then the whole corridor resets and plays again.
        /// </summary>
        public static TaskSequence ProgressBar()
        {
            var PROGRESS_BAR = Tag.GenerateAsUnique("Demo.ProgressBar");
            int segmentCount = 8;
            
            return TaskSequenceBuilder.Create("Progress Bar")
                // Setup
                .Task(d =>
                {
                    var segments = new GameObject[segmentCount];
                    for (int i = 0; i < segmentCount; i++)
                    {
                        var segment = CreatePrim(PrimitiveType.Cube, withOutline: false);
                        segment.transform.localScale = new Vector3(1.5f, 0.3f, 1.5f);
                        segment.transform.position += new Vector3(0f, 0f, i * 2.5f - (segmentCount * 1.25f)) - Vector3.right * 4.5f;
                        SetColor(segment, new Color(0.4f, 0.4f, 0.4f));
                        segments[i] = segment;
                    }
                    d.SetPrimary(PROGRESS_BAR, segments);
                    d.SetPrimary(Tags.ITERATIONS, 1); // Repeat cycles
                })
                // Sequential trap activation
                .Stage(s => s
                    .WithName("Progress Wave")
                    .WithRepeat(true)
                    .StopRepeatWhen(dd => dd.GetPrimary<int>(Tags.ITERATIONS) <= 1)
                    .Task(async (d, t) =>
                    {
                        var segments = d.GetPrimary<GameObject[]>(PROGRESS_BAR);
                        
                        for (int i = 0; i < segments.Length; i++)
                        {
                            t.ThrowIfCancellationRequested();
                            var segment = segments[i];
                            
                            // Warning flash
                            SetColor(segment, new Color(1f, 0.6f, 0f));
                            await UniTask.Delay(80, cancellationToken: t);
                            
                            // Activate: spike up
                            SetColor(segment, new Color(1f, 0.15f, 0.1f));
                            await ScaleTo(segment.transform, new Vector3(1.5f, 4f, 1.5f), 0.12f, t);
                            
                            // Hold briefly
                            await UniTask.Delay(120, cancellationToken: t);
                        }
                        
                        // Hold all active
                        await UniTask.Delay(480, cancellationToken: t);
                        
                        // Retract in reverse
                        for (int i = segments.Length - 1; i >= 0; i--)
                        {
                            t.ThrowIfCancellationRequested();
                            var segment = segments[i];
                            
                            await ScaleTo(segment.transform, new Vector3(1.5f, 0.3f, 1.5f), 0.1f, t);
                            SetColor(segment, new Color(0.4f, 0.4f, 0.4f));
                        }
                        
                        await UniTask.Delay(500, cancellationToken: t);
                        d.Decrement(Tags.ITERATIONS);
                    }))
                .OnTerminate((ctx, success) =>
                {
                    var segments = ctx.Data.GetPrimary<GameObject[]>(PROGRESS_BAR);
                    if (segments != null) foreach (var segment in segments) Object.Destroy(segment);
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Aim Lab
        
        /// <summary>
        /// Spawns targets at random positions. Player clicks to destroy them.
        /// Score tracks how many were hit before the timer runs out.
        /// Demonstrates: input awaiting, timeouts, score tracking, spawning/destroying.
        /// </summary>
        public static TaskSequence AimLab()
        {
            var SCORE = Tag.GenerateAsUnique("Demo.Score");
            var ACTIVE = Tag.GenerateAsUnique("Demo.Active");
            
            return TaskSequenceBuilder.Create("Aim Lab")
                .Task(d =>
                {
                    d.SetPrimary(SCORE, 0);
                    d.SetPrimary(Tags.ITERATIONS, 10); // Targets to spawn
                    d.SetPrimary(ACTIVE, true);
                    Debug.Log("[Aim Lab] Click the targets! 10 targets, 2s each.");
                })
                // Target loop
                .Stage(s => s
                    .WithName("Target Spawning")
                    .WithRepeat(true)
                    .StopRepeatWhen(dd => dd.GetPrimary<int>(Tags.ITERATIONS) <= 1)
                    .Task(async (d, t) =>
                    {
                        // Spawn target at random position
                        var target = CreatePrim(PrimitiveType.Sphere).Scale(Vector3.one * 4f);
                        var rPos = Random.insideUnitCircle * 20f;
                        rPos = ForgeHelper.RandomPointWithinCircle(14f, 5f);
                        target.transform.position += new Vector3(rPos.x, Random.Range(1f, 5f), rPos.y);
                        SetColor(target, new Color(1f, 0.2f, 0.2f));
                        
                        // Pop in
                        var originalScale = target.transform.localScale;
                        target.transform.localScale = Vector3.zero;
                        await ScaleTo(target.transform, originalScale, 0.15f, t);
                        
                        // Race: player clicks target vs 2s timeout
                        bool hit = false;
                        var clickTask = UniTask.Create(async () =>
                        {
                            await DemoManager.Input.AwaitClickOnObject(target, t);
                            hit = true;
                        });
                        var timeoutTask = UniTask.Delay(2000, cancellationToken: t);
                        
                        await UniTask.WhenAny(clickTask, timeoutTask);
                        
                        if (hit)
                        {
                            d.Increment(SCORE);
                            int score = d.GetPrimary<int>(SCORE);
                            Debug.Log($"[Aim Lab] HIT! Score: {score}");
                            
                            // Satisfying pop
                            await PunchScale(target.transform, 0.5f, 0.1f, t);
                        }
                        else
                        {
                            Debug.Log("[Aim Lab] MISS - too slow!");
                            SetColor(target, new Color(0.3f, 0.3f, 0.3f));
                            await UniTask.Delay(200, cancellationToken: t);
                        }
                        
                        // Shrink and destroy
                        await ScaleTo(target.transform, Vector3.zero, 0.12f, t);
                        Object.Destroy(target);
                        
                        d.Decrement(Tags.ITERATIONS);
                    }))
                // Results
                .Task(d =>
                {
                    int score = d.GetPrimary<int>(SCORE);
                    Debug.Log($"[Aim Lab] Final Score: {score}/10");
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Tendrils of Destruction

        /// <summary>
        /// Creates a number of branching tendrils that spread away from the origin.
        /// Each iteration spawns numTendrils line renderers that stretch outward,
        /// then recursively branches from each endpoint for the given number of iterations.
        /// </summary>
        public static TaskSequence TendrilsOfDestruction()
        {
            int numTendrils = 12;
            float degSep = 360f / numTendrils;
            float tendrilLength = 25f;
            float tendrilDuration = 2.5f;
            int maxIterations = 3;

            return TendrilsOfDestruction(numTendrils, degSep, tendrilLength, tendrilDuration, maxIterations);
        }

        private static TaskSequence TendrilsOfDestruction(int numTendrils, float degSep,
            float tendrilLength, float tendrilDuration, int remainingIterations)
        {
            return TaskSequenceBuilder.Create("Tendrils of Destruction")
                .Task(async (d, t) =>
                {
                    if (remainingIterations <= 0) return;

                    var renderers = new List<LineRenderer>();
                    var tendrils = new UniTask[numTendrils];
                    var origin = d.GetPrimary(Tags.ORIGIN, DemoManager.Instance.Player.transform.position);
                    var endpoints = new Vector3[numTendrils];

                    // Shrink tendrils each iteration for a natural branching look
                    float lengthScale = remainingIterations / 3f;
                    //float actualLength = tendrilLength * Mathf.Max(lengthScale, 0.3f);
                    float actualLength = tendrilLength * Mathf.Max(lengthScale, 0.3f);

                    for (int i = 0; i < numTendrils; i++)
                    {
                        var obj = CreatePrim(PrimitiveType.Sphere, withPos: false).Scale(Vector3.one * .2f);
                        obj.transform.position = origin;

                        var lr = obj.AddComponent<LineRenderer>();
                        renderers.Add(lr);

                        lr.material = new Material(Shader.Find("Sprites/Default"));
                        lr.startColor = Color.blue;
                        lr.endColor = Color.red;
                        lr.startWidth = .35f * lengthScale;
                        lr.endWidth = .75f * lengthScale;

                        lr.positionCount = 2;
                        lr.SetPosition(0, origin);
                        lr.SetPosition(1, origin);

                        float angle = i * degSep * Mathf.Deg2Rad;
                        // Add random angular offset for non-root iterations
                        if (remainingIterations < 3)
                            angle += (Random.value - 0.5f) * Mathf.Deg2Rad * 30f;

                        var dir = new Vector3(
                            Mathf.Cos(angle),
                            0f,
                            Mathf.Sin(angle)
                        );
                        var target = origin + dir * actualLength;
                        endpoints[i] = target;

                        var duration = tendrilDuration + Random.value * tendrilDuration * 0.5f;
                        tendrils[i] = LerpVector3(origin, target, duration, v =>
                        {
                            if (lr) lr.SetPosition(1, v);
                        }, t);
                    }

                    // Wait for all tendrils to reach their endpoints
                    await UniTask.WhenAll(tendrils);

                    // Recursively branch from each endpoint
                    if (remainingIterations > 1)
                    {
                        var nextTendrils = new UniTask[numTendrils];

                        for (int i = 0; i < numTendrils; i++)
                        {
                            // Each child gets its own fresh data packet with its own origin
                            var childData = new SequenceDataPacket(d);
                            childData.SetPrimary(Tags.ORIGIN, endpoints[i]);

                            // Recursive call with decremented iteration count
                            var childSequence = TendrilsOfDestruction(
                                numTendrils, degSep, tendrilLength, tendrilDuration,
                                remainingIterations - 1);
                            nextTendrils[i] = childSequence.Run(childData, t);
                        }

                        await UniTask.WhenAll(nextTendrils);
                    }

                    // Clean up line renderers after children are done
                    foreach (var renderer in renderers)
                    {
                        if (renderer) Object.Destroy(renderer.gameObject);
                    }
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Slow Reveal (UI)
        
        /// <summary>
        /// A grid of VisualElements materializes in a diagonal wave pattern.
        /// Each tile scales up from zero with a bounce. Color waves ripple across.
        /// Then tiles dissolve in random order.
        /// Demonstrates: UI Toolkit animations, staggered timing, sequential choreography.
        /// </summary>
        public static TaskSequence SlowReveal()
        {
            var GRID = Tag.GenerateAsUnique("Demo.Grid");
            int rows = 7, cols = 7;
            float tileSize = 52f;
            float gap = 6f;
            
            return TaskSequenceBuilder.Create("Slow Reveal")
                // Setup
                .Task(d =>
                {
                    var overlay = DemoManager.Instance.SequenceOverlay;
                    overlay.style.display = DisplayStyle.Flex;
                    
                    // Grid container centered on screen
                    var container = new VisualElement();
                    container.name = "SlowRevealGrid";
                    container.style.position = Position.Absolute;
                    container.style.left = Length.Percent(50);
                    container.style.top = Length.Percent(50);
                    container.style.translate = new Translate(
                        -(rows * (tileSize + gap)) / 2f,
                        -(cols * (tileSize + gap)) / 2f);
                    container.pickingMode = PickingMode.Ignore;
                    overlay.Add(container);
                    
                    var grid = new VisualElement[rows * cols];
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < cols; c++)
                        {
                            var tile = new VisualElement();
                            tile.style.position = Position.Absolute;
                            tile.style.width = tileSize;
                            tile.style.height = tileSize;
                            tile.style.left = c * (tileSize + gap);
                            tile.style.top = r * (tileSize + gap);
                            tile.style.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
                            tile.style.borderTopLeftRadius = 4;
                            tile.style.borderTopRightRadius = 4;
                            tile.style.borderBottomLeftRadius = 4;
                            tile.style.borderBottomRightRadius = 4;
                            tile.style.scale = new Scale(Vector3.zero);
                            container.Add(tile);
                            grid[r * cols + c] = tile;
                        }
                    }
                    d.SetPrimary(GRID, grid);
                    d.SetPrimary(Tags.CONTAINER, container);
                })
                // Diagonal wave reveal
                .Task(async (d, t) =>
                {
                    var grid = d.GetPrimary<VisualElement[]>(GRID);
                    int maxDiag = rows + cols - 2;
                    
                    for (int diag = 0; diag <= maxDiag; diag++)
                    {
                        t.ThrowIfCancellationRequested();
                        
                        for (int r = 0; r < rows; r++)
                        {
                            int c = diag - r;
                            if (c < 0 || c >= cols) continue;
                            
                            var tile = grid[r * cols + c];
                            PunchRevealUI(tile, t).Forget();
                        }
                        
                        await UniTask.Delay(60, cancellationToken: t);
                    }
                    await UniTask.Delay(400, cancellationToken: t);
                })
                // Color wave
                .Task(async (d, t) =>
                {
                    var grid = d.GetPrimary<VisualElement[]>(GRID);
                    
                    Color[] waveColors = {
                        new Color(0.2f, 0.75f, 0.5f),
                        new Color(0.75f, 0.3f, 0.85f),
                        new Color(0.9f, 0.6f, 0.15f),
                    };
                    
                    foreach (var waveColor in waveColors)
                    {
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                t.ThrowIfCancellationRequested();
                                grid[r * cols + c].style.backgroundColor = waveColor;
                            }
                            await UniTask.Delay(70, cancellationToken: t);
                        }
                        await UniTask.Delay(250, cancellationToken: t);
                    }
                })
                // Hold
                .Task(async (d, t) => await UniTask.Delay(600, cancellationToken: t))
                // Random dissolve
                .Task(async (d, t) =>
                {
                    var grid = d.GetPrimary<VisualElement[]>(GRID);
                    var indices = new int[grid.Length];
                    for (int i = 0; i < indices.Length; i++) indices[i] = i;
                    for (int i = indices.Length - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (indices[i], indices[j]) = (indices[j], indices[i]);
                    }
                    
                    foreach (int idx in indices)
                    {
                        t.ThrowIfCancellationRequested();
                        ScaleElement(grid[idx], 1f, 0f, 0.15f, t).Forget();
                        await UniTask.Delay(30, cancellationToken: t);
                    }
                    await UniTask.Delay(300, cancellationToken: t);
                })
                .OnTerminate((ctx, success) =>
                {
                    var container = ctx.Data.GetPrimary<VisualElement>(Tags.CONTAINER);
                    container?.RemoveFromHierarchy();
                    
                })
                .BuildSequence();
            
            async UniTaskVoid PunchRevealUI(VisualElement tile, CancellationToken token)
            {
                float hue = Random.Range(0f, 1f);
                tile.style.backgroundColor = Color.HSVToRGB(hue, 0.5f, 0.75f);
                await ScaleElement(tile, 0f, 1.15f, 0.1f, token);
                await ScaleElement(tile, 1.15f, 1f, 0.06f, token);
            }
        }
        
        #endregion
        
        #region Boring Speech (UI Typewriter)
        
        /// <summary>
        /// Typewriter dialogue panel. Speaker name appears, text types out character
        /// by character with punctuation pauses. Click or Space skips to full text.
        /// Click again advances to next line. After all lines, the panel slides away.
        /// Demonstrates: TypeText, SlideElement, FadeElement, input awaiting.
        /// </summary>
        public static TaskSequence BoringSpeech()
        {
            var PANEL = Tag.GenerateAsUnique("Demo.Panel");
            var SPEAKER = Tag.GenerateAsUnique("Demo.Speaker");
            var TEXT_LABEL = Tag.GenerateAsUnique("Demo.TextLabel");
            var HINT = Tag.GenerateAsUnique("Demo.Hint");
            
            string[] speakers = { "NARRATOR", "NARRATOR", "HERO", "NARRATOR" };
            string[] lines =
            {
                "A long time ago, in a land far far away...",
                "There lived a very bored programmer who made dialogue systems for fun.",
                "That's me. I regret nothing.",
                "And so the story ends. As all boring speeches must."
            };
            
            return TaskSequenceBuilder.Create("Boring Speech")
                // Build panel
                .Task(d =>
                {
                    var overlay = DemoManager.Instance.SequenceOverlay;
                    overlay.style.display = DisplayStyle.Flex;
                    
                    var panel = new VisualElement();
                    panel.name = "DialoguePanel";
                    panel.style.position = Position.Absolute;
                    panel.style.bottom = 40;
                    panel.style.left = Length.Percent(20);
                    panel.style.right = Length.Percent(20);
                    panel.style.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);
                    panel.style.borderTopLeftRadius = 8;
                    panel.style.borderTopRightRadius = 8;
                    panel.style.borderBottomLeftRadius = 8;
                    panel.style.borderBottomRightRadius = 8;
                    panel.style.paddingLeft = 20;
                    panel.style.paddingRight = 20;
                    panel.style.paddingTop = 14;
                    panel.style.paddingBottom = 14;
                    panel.style.borderLeftWidth = 2;
                    panel.style.borderLeftColor = new Color(0.6f, 0.5f, 0.2f);
                    panel.style.translate = new Translate(0, 120);
                    panel.style.opacity = 0;
                    overlay.Add(panel);
                    
                    var speaker = new Label();
                    speaker.name = "SpeakerName";
                    speaker.style.fontSize = 13;
                    speaker.style.color = new Color(0.85f, 0.7f, 0.3f);
                    speaker.style.unityFontStyleAndWeight = FontStyle.Bold;
                    speaker.style.marginBottom = 6;
                    panel.Add(speaker);
                    
                    var textLabel = new Label();
                    textLabel.name = "DialogueText";
                    textLabel.style.fontSize = 16;
                    textLabel.style.color = Color.white;
                    textLabel.style.whiteSpace = WhiteSpace.Normal;
                    textLabel.style.minHeight = 40;
                    panel.Add(textLabel);
                    
                    var hint = new Label("[Click to continue]");
                    hint.name = "ContinueHint";
                    hint.style.fontSize = 11;
                    hint.style.color = new Color(1f, 1f, 1f, 0.3f);
                    hint.style.unityTextAlign = TextAnchor.MiddleRight;
                    hint.style.marginTop = 8;
                    hint.style.opacity = 0;
                    panel.Add(hint);
                    
                    d.SetPrimary(PANEL, panel);
                    d.SetPrimary(SPEAKER, speaker);
                    d.SetPrimary(TEXT_LABEL, textLabel);
                    d.SetPrimary(HINT, hint);
                    d.SetPrimary(Tags.ITERATIONS, 0);
                })
                // Slide panel in
                .Task(async (d, t) =>
                {
                    var panel = d.GetPrimary<VisualElement>(PANEL);
                    
                    FadeElement(panel, 0f, 1f, 0.3f, t).Forget();
                    await SlideElement(panel, new Vector2(0, 120), Vector2.zero, 0.35f, t);
                })
                // Dialogue loop
                .Stage(s => s
                    .WithName("Dialogue Lines")
                    .WithRepeat(true)
                    .StopRepeatWhen(dd => dd.GetPrimary<int>(Tags.ITERATIONS) >= lines.Length)
                    .Task(async (d, t) =>
                    {
                        if (d.GetPrimary<int>(Tags.ITERATIONS) >= lines.Length)
                        {
                            d.Inject(BreakStageRepeatInjection.Instance);
                            return;
                        }
                        
                        int idx = d.GetPrimary<int>(Tags.ITERATIONS);
                        var speaker = d.GetPrimary<Label>(SPEAKER);
                        var textLabel = d.GetPrimary<Label>(TEXT_LABEL);
                        var hint = d.GetPrimary<Label>(HINT);
                        
                        speaker.text = speakers[idx];
                        hint.style.opacity = 0;
                        
                        // Type text (skippable via click or space)
                        await TypeTextSkippable(textLabel, lines[idx], 0.035f,
                            () => DemoManager.Input.IsMouseDown() || DemoManager.Input.IsKeyDown(Key.Space),
                            t);
                        
                        // Show continue hint
                        FadeElement(hint, 0f, 1f, 0.3f, t).Forget();
                        
                        // Wait a beat then await click to advance
                        await UniTask.Delay(200, cancellationToken: t);
                        await DemoManager.Input.AwaitMouseDown(t);

                        //await UniTask.Delay(25, cancellationToken: t);
                        
                        hint.style.opacity = 0;
                        d.Increment(Tags.ITERATIONS);
                    }))
                // Slide panel out
                .Task(async (d, t) =>
                {
                    var panel = d.GetPrimary<VisualElement>(PANEL);
                    FadeElement(panel, 1f, 0f, 0.3f, t).Forget();
                    await SlideElement(panel, Vector2.zero, new Vector2(0, 120), 0.35f, t);
                })
                .OnTerminate((ctx, success) =>
                {
                    ctx.Data.GetPrimary<VisualElement>(PANEL)?.RemoveFromHierarchy();
                    
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Radial Menu (UI)
        
        /// <summary>
        /// Items fan out from center in a circle. Player clicks one.
        /// Selected item pulses, others collapse back. Choice is logged.
        /// Demonstrates: polar UI positioning, ScaleElement, input callbacks, branching.
        /// </summary>
        public static TaskSequence RadialMenu()
        {
            var ITEMS = Tag.GenerateAsUnique("Demo.Items");
            var SELECTED = Tag.GenerateAsUnique("Demo.Selected");
            int itemCount = 6;
            float radius = 110f;
            float itemSize = 54f;
            
            string[] labels = { "ATK", "DEF", "MAG", "SPD", "HP", "MP" };
            Color[] colors = {
                new Color(0.9f, 0.25f, 0.2f),
                new Color(0.3f, 0.5f, 0.9f),
                new Color(0.7f, 0.2f, 0.9f),
                new Color(0.2f, 0.9f, 0.4f),
                new Color(0.9f, 0.7f, 0.2f),
                new Color(0.2f, 0.7f, 0.9f),
            };
            
            return TaskSequenceBuilder.Create("Radial Menu")
                // Build menu
                .Task(d =>
                {
                    var overlay = DemoManager.Instance.SequenceOverlay;
                    overlay.style.display = DisplayStyle.Flex;
                    
                    var container = new VisualElement();
                    container.name = "RadialMenu";
                    container.style.position = Position.Absolute;
                    container.style.left = Length.Percent(50);
                    container.style.top = Length.Percent(50);
                    container.style.width = 0; container.style.height = 0;
                    overlay.Add(container);
                    
                    var items = new VisualElement[itemCount];
                    for (int i = 0; i < itemCount; i++)
                    {
                        var item = new VisualElement();
                        item.style.position = Position.Absolute;
                        item.style.width = itemSize;
                        item.style.height = itemSize;
                        item.style.left = -itemSize / 2f;
                        item.style.top = -itemSize / 2f;
                        item.style.translate = new Translate(0, 0);
                        item.style.backgroundColor = colors[i % colors.Length];
                        item.style.borderTopLeftRadius = itemSize / 2f;
                        item.style.borderTopRightRadius = itemSize / 2f;
                        item.style.borderBottomLeftRadius = itemSize / 2f;
                        item.style.borderBottomRightRadius = itemSize / 2f;
                        item.style.scale = new Scale(Vector3.zero);
                        item.style.alignItems = Align.Center;
                        item.style.justifyContent = Justify.Center;
                        item.pickingMode = PickingMode.Position;
                        
                        var lbl = new Label(labels[i % labels.Length]);
                        lbl.style.fontSize = 12;
                        lbl.style.color = Color.white;
                        lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                        lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                        lbl.pickingMode = PickingMode.Ignore;
                        item.Add(lbl);
                        
                        container.Add(item);
                        items[i] = item;
                    }
                    
                    d.SetPrimary(Tags.CONTAINER, container);
                    d.SetPrimary(ITEMS, items);
                    d.SetPrimary(SELECTED, -1);
                })
                // Fan out
                .Task(async (d, t) =>
                {
                    var items = d.GetPrimary<VisualElement[]>(ITEMS);
                    
                    for (int i = 0; i < items.Length; i++)
                    {
                        float angle = (360f / items.Length) * i - 90f; // Start from top
                        float rad = angle * Mathf.Deg2Rad;
                        float tx = Mathf.Cos(rad) * radius;
                        float ty = Mathf.Sin(rad) * radius;
                        
                        int idx = i; // Capture for lambda
                        ScaleElement(items[idx], 0f, 1f, 0.2f, t).Forget();
                        SlideElement(items[idx], Vector2.zero, new Vector2(tx, ty), 0.25f, t).Forget();
                        
                        await UniTask.Delay(60, cancellationToken: t);
                    }
                    await UniTask.Delay(250, cancellationToken: t);
                    
                    Debug.Log("[Radial] Click an option!");
                })
                // Await selection
                .Task(async (d, t) =>
                {
                    var items = d.GetPrimary<VisualElement[]>(ITEMS);
                    int selectedIdx = -1;
                    
                    // Register click handlers on each item
                    var tcs = new UniTaskCompletionSource<int>();
                    
                    for (int i = 0; i < items.Length; i++)
                    {
                        int idx = i;
                        items[i].RegisterCallback<ClickEvent>(evt =>
                        {
                            tcs.TrySetResult(idx);
                        });
                    }
                    
                    selectedIdx = await tcs.Task.AttachExternalCancellation(t);
                    d.SetPrimary(SELECTED, selectedIdx);
                    Debug.Log($"[Radial] Selected: {labels[selectedIdx]}");
                })
                // Animate result
                .Task(async (d, t) =>
                {
                    var items = d.GetPrimary<VisualElement[]>(ITEMS);
                    int selected = d.GetPrimary<int>(SELECTED);
                    
                    // Collapse non-selected
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (i == selected) continue;
                        FadeElement(items[i], 1f, 0f, 0.2f, t).Forget();
                        ScaleElement(items[i], 1f, 0f, 0.25f, t).Forget();
                    }
                    
                    // Pulse selected
                    if (selected >= 0)
                    {
                        await PunchScaleElement(items[selected], 0.4f, 0.3f, t);
                        await UniTask.Delay(600, cancellationToken: t);
                        await FadeElement(items[selected], 1f, 0f, 0.25f, t);
                    }
                })
                .OnTerminate((ctx, success) =>
                {
                    ctx.Data.GetPrimary<VisualElement>(Tags.CONTAINER)?.RemoveFromHierarchy();
                    
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Notification Toast (UI)
        
        /// <summary>
        /// Toast notifications slide in from the right edge, hold, slide out.
        /// Stacked vertically. Each toast is its own sub-process.
        /// Demonstrates: SlideElement, FadeElement, queued sub-processes, UI cleanup.
        /// </summary>
        public static TaskSequence Notification()
        {
            string[] messages = { "Item Acquired!", "Level Up!", "Quest Complete!", "Achievement Unlocked!", "New Area Discovered!" };
            Color[] accents = {
                new Color(0.3f, 0.7f, 0.3f),
                new Color(0.8f, 0.7f, 0.2f),
                new Color(0.3f, 0.5f, 0.9f),
                new Color(0.9f, 0.5f, 0.2f),
                new Color(0.6f, 0.3f, 0.8f),
            };
            
            return TaskSequenceBuilder.Create("Notification Queue")
                // Setup container
                .Task(d =>
                {
                    var overlay = DemoManager.Instance.SequenceOverlay;
                    overlay.style.display = DisplayStyle.Flex;
                    
                    var container = new VisualElement();
                    container.name = "ToastContainer";
                    container.style.position = Position.Absolute;
                    container.style.top = 20;
                    container.style.right = 20;
                    container.style.width = 280;
                    container.style.flexDirection = FlexDirection.Column;
                    container.pickingMode = PickingMode.Ignore;
                    overlay.Add(container);
                    
                    d.SetPrimary(Tags.CONTAINER, container);
                    d.SetPrimary(Tags.ITERATIONS, 0);
                })
                // Spawn toasts
                .Stage(s => s
                    .WithName("Toast Queue")
                    .WithRepeat(true)
                    .StopRepeatWhen(dd => dd.GetPrimary<int>(Tags.ITERATIONS) >= messages.Length)
                    .Task(async (d, t) =>
                    {
                        int idx = d.GetPrimary<int>(Tags.ITERATIONS);
                        var container = d.GetPrimary<VisualElement>(Tags.CONTAINER);
                        
                        // Create toast element
                        var toast = new VisualElement();
                        toast.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.92f);
                        toast.style.borderTopLeftRadius = 6;
                        toast.style.borderTopRightRadius = 6;
                        toast.style.borderBottomLeftRadius = 6;
                        toast.style.borderBottomRightRadius = 6;
                        toast.style.paddingLeft = 14;
                        toast.style.paddingRight = 14;
                        toast.style.paddingTop = 10;
                        toast.style.paddingBottom = 10;
                        toast.style.marginBottom = 6;
                        toast.style.borderLeftWidth = 3;
                        toast.style.borderLeftColor = accents[idx % accents.Length];
                        toast.style.translate = new Translate(300, 0);
                        toast.style.opacity = 0;
                        toast.pickingMode = PickingMode.Ignore;
                        
                        var label = new Label(messages[idx]);
                        label.style.fontSize = 13;
                        label.style.color = Color.white;
                        label.pickingMode = PickingMode.Ignore;
                        toast.Add(label);
                        
                        container.Add(toast);
                        
                        // Fire toast lifecycle as its own process
                        var toastData = new SequenceDataPacket(d);
                        toastData.SetPrimary(Tags.DATA, toast);
                        ProcessControl.Register(ToastLifecycle(), DemoManager.Instance, toastData, out _);
                        d.Increment(Tags.ITERATIONS);
                        await UniTask.Delay(700, cancellationToken: t);
                    }))
                // Wait for last toasts
                .Task(async (d, t) => await UniTask.Delay(3500, cancellationToken: t))
                .OnTerminate((ctx, success) =>
                {
                    ctx.Data.GetPrimary<VisualElement>(Tags.CONTAINER)?.RemoveFromHierarchy();
                    
                })
                .BuildSequence();
            
            TaskSequence ToastLifecycle()
            {
                return TaskSequenceBuilder.Create("Toast")
                    .Task(async (d, t) =>
                    {
                        var toast = d.GetPrimary<VisualElement>(Tags.DATA);
                        
                        // Slide in
                        FadeElement(toast, 0f, 1f, 0.2f, t).Forget();
                        await SlideElement(toast, new Vector2(300, 0), Vector2.zero, 0.25f, t);
                        
                        // Hold
                        await UniTask.Delay(2500, cancellationToken: t);
                        
                        // Slide out
                        FadeElement(toast, 1f, 0f, 0.25f, t).Forget();
                        await SlideElement(toast, Vector2.zero, new Vector2(300, 0), 0.3f, t);
                    })
                    .OnTerminate((ctx, success) =>
                    {
                        ctx.Data.GetPrimary<VisualElement>(Tags.DATA)?.RemoveFromHierarchy();
                    })
                    .BuildSequence();
            }
        }
        
        #endregion
        
        #region Loading Screen (UI)
        
        /// <summary>
        /// A progress bar fills segment by segment. A "tip" label cycles text below.
        /// Demonstrates: WidthTo, parallel maintained tasks, WhenAny completion.
        /// </summary>
        public static TaskSequence LoadingScreen()
        {
            var BAR_FILL = Tag.GenerateAsUnique("Demo.BarFill");
            var TIP_LABEL = Tag.GenerateAsUnique("Demo.TipLabel");
            var PCT_LABEL = Tag.GenerateAsUnique("Demo.PctLabel");
            
            string[] tips = {
                "Press W to move forward...",
                "Enemies are weak to fire!",
                "Remember to save often.",
                "The cake is a lie.",
                "Did you try turning it off and on?",
            };
            
            return TaskSequenceBuilder.Create("Loading Screen")
                // Build UI
                .Task(d =>
                {
                    var overlay = DemoManager.Instance.SequenceOverlay;
                    overlay.style.display = DisplayStyle.Flex;
                    
                    // Full screen dark background
                    var container = new VisualElement();
                    container.name = "LoadingScreen";
                    container.style.position = Position.Absolute;
                    container.style.left = 0; container.style.top = 0;
                    container.style.right = 0; container.style.bottom = 0;
                    container.style.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 0.95f);
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.Center;
                    overlay.Add(container);
                    
                    // "Loading..." label
                    var title = new Label("LOADING");
                    title.style.fontSize = 24;
                    title.style.color = new Color(0.7f, 0.7f, 0.8f);
                    title.style.unityFontStyleAndWeight = FontStyle.Bold;
                    title.style.marginBottom = 16;
                    title.style.letterSpacing = 6;
                    container.Add(title);
                    
                    // Bar track
                    var barTrack = new VisualElement();
                    barTrack.style.width = 400;
                    barTrack.style.height = 18;
                    barTrack.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
                    barTrack.style.borderTopLeftRadius = 9;
                    barTrack.style.borderTopRightRadius = 9;
                    barTrack.style.borderBottomLeftRadius = 9;
                    barTrack.style.borderBottomRightRadius = 9;
                    barTrack.style.borderLeftWidth = 1; barTrack.style.borderRightWidth = 1;
                    barTrack.style.borderTopWidth = 1; barTrack.style.borderBottomWidth = 1;
                    barTrack.style.borderLeftColor = new Color(0.25f, 0.25f, 0.3f);
                    barTrack.style.borderRightColor = new Color(0.25f, 0.25f, 0.3f);
                    barTrack.style.borderTopColor = new Color(0.25f, 0.25f, 0.3f);
                    barTrack.style.borderBottomColor = new Color(0.25f, 0.25f, 0.3f);
                    barTrack.style.overflow = Overflow.Hidden;
                    container.Add(barTrack);
                    
                    // Bar fill
                    var barFill = new VisualElement();
                    barFill.style.width = 0;
                    barFill.style.height = Length.Percent(100);
                    barFill.style.backgroundColor = new Color(0.25f, 0.55f, 0.9f);
                    barFill.style.borderTopLeftRadius = 9;
                    barFill.style.borderBottomLeftRadius = 9;
                    barTrack.Add(barFill);
                    
                    // Percentage
                    var pct = new Label("0%");
                    pct.style.fontSize = 13;
                    pct.style.color = new Color(0.5f, 0.5f, 0.6f);
                    pct.style.marginTop = 8;
                    container.Add(pct);
                    
                    // Tip
                    var tip = new Label(tips[0]);
                    tip.style.fontSize = 12;
                    tip.style.color = new Color(0.4f, 0.4f, 0.5f);
                    tip.style.unityFontStyleAndWeight = FontStyle.Italic;
                    tip.style.marginTop = 30;
                    container.Add(tip);
                    
                    d.SetPrimary(Tags.CONTAINER, container);
                    d.SetPrimary(BAR_FILL, barFill);
                    d.SetPrimary(PCT_LABEL, pct);
                    d.SetPrimary(TIP_LABEL, tip);
                })
                // Loading: bar fill + tip cycle in parallel
                .Stage(s => s
                    .WithName("Loading")
                    // Bar fill
                    .Task(async (d, t) =>
                    {
                        var barFill = d.GetPrimary<VisualElement>(BAR_FILL);
                        var pctLabel = d.GetPrimary<Label>(PCT_LABEL);
                        int segments = 34;
                        
                        for (int i = 0; i < segments; i++)
                        {
                            t.ThrowIfCancellationRequested();
                            
                            int delay = Random.Range(60, 300);
                            await UniTask.Delay(delay, cancellationToken: t);
                            
                            float progress = (float)(i + 1) / segments;
                            float widthPx = 400f * progress;
                            barFill.style.width = widthPx;
                            
                            // Color transitions blue → green
                            var fillColor = Color.Lerp(
                                new Color(0.25f, 0.55f, 0.9f),
                                new Color(0.3f, 0.85f, 0.45f),
                                progress);
                            barFill.style.backgroundColor = fillColor;
                            
                            pctLabel.text = $"{Mathf.RoundToInt(progress * 100)}%";
                        }
                    })
                    // Tip cycler (parallel)
                    .Task(async (d, t) =>
                    {
                        var tipLabel = d.GetPrimary<Label>(TIP_LABEL);
                        int tipIdx = 0;
                        
                        while (!t.IsCancellationRequested)
                        {
                            tipLabel.text = tips[tipIdx % tips.Length];
                            await FadeElement(tipLabel, 0f, 1f, 0.3f, t);
                            await UniTask.Delay(2000, cancellationToken: t);
                            await FadeElement(tipLabel, 1f, 0f, 0.3f, t);
                            tipIdx++;
                        }
                    })
                    .WhenAny())
                // Complete flash
                .Task(async (d, t) =>
                {
                    var barFill = d.GetPrimary<VisualElement>(BAR_FILL);
                    var pctLabel = d.GetPrimary<Label>(PCT_LABEL);
                    var tipLabel = d.GetPrimary<Label>(TIP_LABEL);
                    
                    pctLabel.text = "100%";
                    pctLabel.style.color = new Color(0.3f, 0.9f, 0.4f);
                    tipLabel.text = "Complete!";
                    tipLabel.style.opacity = 1f;
                    tipLabel.style.color = new Color(0.3f, 0.9f, 0.4f);
                    tipLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                    
                    // White flash on bar
                    barFill.style.backgroundColor = Color.white;
                    await UniTask.Delay(150, cancellationToken: t);
                    barFill.style.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
                    
                    await UniTask.Delay(1200, cancellationToken: t);
                })
                // Fade out
                .Task(async (d, t) =>
                {
                    var container = d.GetPrimary<VisualElement>(Tags.CONTAINER);
                    await FadeElement(container, 1f, 0f, 0.5f, t);
                })
                .OnTerminate((ctx, success) =>
                {
                    ctx.Data.GetPrimary<VisualElement>(Tags.CONTAINER)?.RemoveFromHierarchy();
                })
                .BuildSequence();
        }
        
        #endregion

        #region Util
        
        private static TaskSequence BounceSequence()
        {
            return TaskSequenceBuilder.Create("Bounce Sequence")
                .WithRepeat(true)
                .Task(async (d, t) =>
                {
                    var obj = d.GetPrimary<GameObject>(Tags.DATA);
                    var height = d.GetPrimary<float>(Tags.HEIGHT, 10f);
                    
                    if (obj is null || height < .2f || d.GetPrimary<int>(Tags.ITERATIONS) <= 0)
                    {
                        d.Inject(InterruptSequenceInjection.Instance);
                        return;
                    }
                    
                    var distance = d.GetPrimary<float>(Tags.DISTANCE, 10f);
                    var duration = d.GetPrimary<float>(Tags.DURATION, 2f);

                    var rot = d.GetPrimary<Vector2>(Tags.ROTATION);
                    var x = distance * Mathf.Cos(rot.x) - distance * Mathf.Sin(rot.y);
                    var z = distance * Mathf.Sin(rot.x) + distance * Mathf.Cos(rot.y);
                        
                    var destination = obj.transform.position + new Vector3(x, 0f, z);
                    
                    var arcTo = ArcTo(obj.transform, destination, duration, height, t); 
                    var rotate = RotateBy(obj.transform, new Vector3(0f, height * 250f, 0f), duration, t);
                    var punchScale = PunchScale(obj.transform, 1.5f, .35f, t);
                    
                    var torrentTask = new[] { arcTo, rotate, punchScale };
                    await UniTask.WhenAll(torrentTask);
                        
                    d.Decrement(Tags.ITERATIONS);
                })
                .Task(d =>
                {
                    d.SetPrimary(Tags.DISTANCE, d.GetPrimary<float>(Tags.DISTANCE) * .75f);
                    var height = d.GetPrimary<float>(Tags.HEIGHT);
                    d.SetPrimary(Tags.HEIGHT, height - Mathf.Max(height * .25f, .35f));
                    d.SetPrimary(Tags.DURATION, d.GetPrimary<float>(Tags.DURATION) * .75f);
                })
                .OnTerminate((ctx, success) =>
                {
                    var obj = ctx.Data.GetPrimary<GameObject>(Tags.DATA);
                    Object.Destroy(obj);
                })
                .BuildSequence();
        }
        
        private static TaskSequence WaitForClick(float timeout = 10f, ISequenceInjection timeoutInjection = null)
        {
            return TaskSequenceBuilder.Create("Waiting for click")
                .WithTimeout(timeout, timeoutInjection)
                .Task(async (d, t) =>
                {
                    var obj = d.GetPrimary<GameObject>(Tags.DATA);
                    await DemoManager.Input.AwaitClickOnObject(obj, t);
                })
                .OnTerminate((ctx, success) =>
                {
                    Debug.Log($"Wait for click terminated: {success} ({ctx.Data.GetPrimary<GameObject>(Tags.DATA)}");
                    if (!success) Object.Destroy(ctx.Data.GetPrimary<GameObject>(Tags.DATA));
                    else ProcessControl.Register(BounceSequence(), DemoManager.Instance, ctx.Data, out _);

                })
                .BuildSequence();
        }
        
        #endregion
    }
}