using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static FarEmerald.PlayForge.SequenceTaskLibrary;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;


namespace FarEmerald.PlayForge.Examples
{
    public static class DemoSequences
    {
        private static readonly Tag ITERATIONS = Tag.Generate("Sequence.Iterations");

        public static Material GenMat = null;

        private static GameObject CreatePrim(PrimitiveType type = PrimitiveType.Cube, bool withMat = true, bool withOutline = true)
        {
            var obj = GameObject.CreatePrimitive(type);
            if (GenMat && withMat) obj.GetComponent<MeshRenderer>().material = GenMat;
            if (withOutline)
            {
                var outline = obj.AddComponent<Outline>();
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.OutlineColor = Color.magenta;
            }

            obj.AddComponent<TestWhyBrok>();
            return obj;
        }
        
        #region Torrent Storm
        public static TaskSequence TorrentStorm(AnimationCurve c = null)
        {
            /*
             * Torrent storm around a certain point within a radius
             * N torrents over D duration
             * Each torrent starts its own torrent sequence separate from storm sequence
             */
            var D = Tag.Generate("Duration of Storm");
            var R = Tag.Generate("Storm Radius");
            
            var N = Tag.Generate("Number of Torrents");
            var tD = Tag.Generate("Duration of Torrent");
            var yD = Tag.Generate("Torrent Y Delta");
            
            var stormSequence = TaskSequenceBuilder.Create("Torrent Storm")
                .Task(d =>
                {
                    // Setup our parameters
                    d.SetPrimary(D, 10f);
                    d.SetPrimary(R, 7.5f);
                    d.SetPrimary(N, 15);
                    d.SetPrimary(tD, 2f);
                    d.SetPrimary(yD, 15f);
                    
                    d.SetPrimary(ITERATIONS, d.GetPrimary<int>(N));
                })
                .Task(d =>
                {
                    // Setup camera in good spot
                    /*var cPos = new Vector3(0, 30, 0);
                    Camera.main.transform.position = cPos;
                    Camera.main.transform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));*/
                })
                .Stage(s => s
                    .WithName("Torrent Storm")
                    .WithDescription("Create a storm of torrents around (0, 0, 0)")
                    .WithRepeat(true)
                    //.StopRepeatWhen(d => d.GetPrimary(ITERATIONS, 0) <= 0)
                    .Task(d =>
                    {
                        if (d.GetPrimary<int>(ITERATIONS) <= 0)
                        {
                            d.Inject(BreakStageRepeatInjection.Instance, true);
                            return;
                        }
                        
                        // Create the torrent
                        var torrent = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        var rPos = Random.insideUnitCircle * d.GetPrimary<float>(R);
                        var pos = new Vector3(rPos.x, 1f, rPos.y);
                        torrent.transform.position = pos;
                        
                        var torrentData = new SequenceDataPacket(d);
                        torrentData.SetPrimary(Tags.DATA, torrent);

                        TaskSequenceProcess.Register(TorrentSequence(), torrentData);

                        d.Decrement(ITERATIONS);
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
        public static TaskSequence TorrentStorm2()
        {
            /*
             * Torrent storm around a certain point within a radius
             * N torrents over D duration
             * Each torrent starts its own torrent sequence separate from storm sequence
             */
            var D = Tag.Generate("Duration of Storm");
            var R = Tag.Generate("Storm Radius");
            
            var N = Tag.Generate("Number of Torrents");
            var tD = Tag.Generate("Duration of Torrent");
            var yD = Tag.Generate("Torrent Y Delta");
            
            var stormSequence = TaskSequenceBuilder.Create("Torrent Storm")
                .Task(d =>
                {
                    // Setup our parameters
                    d.SetPrimary(D, 10f);
                    d.SetPrimary(R, 5.5f);
                    d.SetPrimary(N, 1);
                    d.SetPrimary(tD, 2f);
                    d.SetPrimary(yD, 15f);
                    
                    d.SetPrimary(ITERATIONS, d.GetPrimary<int>(N));
                })
                .Task(d =>
                {
                    // Setup camera in good spot
                    /*var cPos = new Vector3(0, 30, 0);
                    Camera.main.transform.position = cPos;
                    Camera.main.transform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));*/
                })
                .Stage(s => s
                    .WithName("Torrent Storm")
                    .WithDescription("Create a storm of torrents around (0, 0, 0) bound in a random direction")
                    .WithRepeat(true)
                    //.StopRepeatWhen(d => d.GetPrimary(ITERATIONS, 0) <= 0)
                    .Task(d =>
                    {
                        if (d.GetPrimary<int>(ITERATIONS) <= 0)
                        {
                            d.Inject(BreakStageRepeatInjection.Instance, true);
                            return;
                        }
                        
                        // Create the torrent
                        var torrent = CreatePrim();
                        var rPos = Random.insideUnitCircle * d.GetPrimary<float>(R);
                        var pos = new Vector3(rPos.x, 1f, rPos.y);
                        torrent.transform.position = pos;
                        
                        var torrentData = new SequenceDataPacket(d);
                        torrentData.SetPrimary(Tags.DATA, torrent);

                        TaskSequenceProcess.Register(TorrentSequence(), torrentData);

                        d.Decrement(ITERATIONS);
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
                    .Task(d =>
                    {
                        d.SetPrimary(ITERATIONS, 5);
                        d.SetPrimary(Tags.ROTATION, new Vector2(Random.Range(0, 360), Random.Range(0, 360)));
                    })
                    .Stage(s => s
                        .WithRepeat(true)
                        .StopRepeatWhen(d =>
                        {
                            return d.GetPrimary<int>(ITERATIONS) <= 0;
                        })
                        .Task(async (d, t) =>
                        {
                            var torrent = d.GetPrimary<GameObject>(Tags.DATA);
                            var delta = d.GetPrimary<float>(yD);
                            var duration = d.GetPrimary<float>(tD);

                            var rot = d.GetPrimary<Vector2>(Tags.ROTATION);
                            var x = delta * Mathf.Cos(rot.x) - delta * Mathf.Sin(rot.y);
                            var z = delta * Mathf.Sin(rot.x) + delta * Mathf.Cos(rot.y);
                            
                            var destination = torrent.transform.position + new Vector3(x, 0f, z);
                        
                            var arcTo = ArcTo(torrent.transform, destination, duration, delta, t); 
                            var rotate = RotateBy(torrent.transform, new Vector3(0f, delta * 50f, 0f), duration, t);
                        
                            var torrentTask = new[] { arcTo, rotate };
                            await UniTask.WhenAll(torrentTask);
                            
                            d.Decrement(ITERATIONS);
                            //Debug.Log($"Finished A");
                        })
                        .Task(d =>
                        {
                            d.SetPrimary(yD, d.GetPrimary<float>(yD) * .75f);
                            d.SetPrimary(tD, d.GetPrimary<float>(tD) * .75f);
                        }))
                    .OnTerminate((ctx, success) =>
                    {
                        //Debug.Log("Starting B");
                        var torrent = ctx.Data.GetPrimary<GameObject>(Tags.DATA);
                        Object.Destroy(torrent);
                    })
                    .BuildSequence();
            }
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
                    
                    d.SetPrimary(ITERATIONS, 3);
                    d.AddPayload(Tags.POSITION, obj.transform.position);
                    d.AddPayload(Tags.TARGET_POS, new Vector3(0, 50, 0));
                    d.AddPayload(Tags.DEBUG,
                        (d.GetPrimary<Vector3>(Tags.TARGET_POS).y - d.GetPrimary<Vector3>(Tags.POSITION).y) / d.GetPrimary<int>(ITERATIONS)
                    );
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(900, cancellationToken: t);
                        
                        var delta = d.GetPrimary<float>(Tags.DEBUG);
                        await MoveBy(Camera.main.transform, new Vector3(0f, delta, 0f), .2f, t);
                        
                        d.Decrement(ITERATIONS);
                    }))
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(1800, cancellationToken: t);
                    d.SetPrimary(ITERATIONS, 3);
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(500, cancellationToken: t);
                        
                        var rPos = Random.insideUnitCircle * 15f;
                        var pos = d.GetPrimary<Vector3>(Tags.POSITION) + new Vector3(rPos.x, 0f, rPos.y);
                        
                        var delta = pos - Camera.main.transform.position;
                        delta.y = 0f;
                        await MoveBy(Camera.main.transform, delta, 1f, t);

                        d.Decrement(ITERATIONS);
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
                    d.SetPrimary(ITERATIONS, 3);
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(900, cancellationToken: t);
                        
                        var delta = -d.GetPrimary<float>(Tags.DEBUG);
                        await MoveBy(Camera.main.transform, new Vector3(0f, delta, 0f), .2f, t);
                        
                        d.Decrement(ITERATIONS);
                    }))
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(900, cancellationToken: t);
                })
                .BuildSequence();
        }
        
        #endregion
        
        #region Dealing Cards
        
        public static TaskSequence DealingCards()
        { 
            return TaskSequenceBuilder.Create("Smite!")
                .WithDescription("Waits to receive a target location froom input manager.")
                .Task(d =>
                {
                    var obj = d.GetPrimary<GameplayAbilitySystem>(Tags.DATA);
                    Camera.main.transform.position = obj.transform.position + new Vector3(0, 10, 0);
                    Camera.main.transform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));    
                    
                    d.SetPrimary(ITERATIONS, 3);
                    d.AddPayload(Tags.POSITION, obj.transform.position);
                    d.AddPayload(Tags.TARGET_POS, new Vector3(0, 50, 0));
                    d.AddPayload(Tags.DEBUG,
                        (d.GetPrimary<Vector3>(Tags.TARGET_POS).y - d.GetPrimary<Vector3>(Tags.POSITION).y) / d.GetPrimary<int>(ITERATIONS)
                    );
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(900, cancellationToken: t);
                        
                        var delta = d.GetPrimary<float>(Tags.DEBUG);
                        await MoveBy(Camera.main.transform, new Vector3(0f, delta, 0f), .2f, t);
                        
                        d.Decrement(ITERATIONS);
                    }))
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(1800, cancellationToken: t);
                    d.SetPrimary(ITERATIONS, 3);
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(500, cancellationToken: t);
                        
                        var rPos = Random.insideUnitCircle * 15f;
                        var pos = d.GetPrimary<Vector3>(Tags.POSITION) + new Vector3(rPos.x, 0f, rPos.y);
                        
                        var delta = pos - Camera.main.transform.position;
                        delta.y = 0f;
                        await MoveBy(Camera.main.transform, delta, 1f, t);

                        d.Decrement(ITERATIONS);
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
                    d.SetPrimary(ITERATIONS, 3);
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 1, true)
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(900, cancellationToken: t);
                        
                        var delta = -d.GetPrimary<float>(Tags.DEBUG);
                        await MoveBy(Camera.main.transform, new Vector3(0f, delta, 0f), .2f, t);
                        
                        d.Decrement(ITERATIONS);
                    }))
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(900, cancellationToken: t);
                })
                .BuildSequence();
        }
        
        #endregion
    }
}
