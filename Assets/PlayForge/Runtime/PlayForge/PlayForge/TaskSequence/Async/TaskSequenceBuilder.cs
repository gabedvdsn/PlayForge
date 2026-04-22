using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════
    // TASK SEQUENCE BUILDER
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Fluent builder for creating TaskSequence definitions.
    /// All delegate methods use SequenceDataPacket for direct sequence/stage control.
    /// </summary>
    public class TaskSequenceBuilder
    {
        private readonly List<SequenceStage> _stages = new();
        private readonly List<SequenceCondition> _conditions = new();
        private HashSet<Type> _allowedInjections;
        private float? _maxDurationSeconds;
        private ISequenceInjection _maxDurationInjection;
        private readonly List<SequenceTimeout> _timeouts = new();
        private EProcessStepTiming _conditionStepTiming = EProcessStepTiming.Update;  // Check conditions in Update by default
        private SequenceMetadata _metadata;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STATIC ENTRY POINTS
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static TaskSequenceBuilder Create() => new();
        public static TaskSequenceBuilder Create(string name) => new TaskSequenceBuilder().WithName(name);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // METADATA
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceBuilder WithName(string name)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.Name = name;
            return this;
        }
        
        public TaskSequenceBuilder WithDescription(string description)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.Description = description;
            return this;
        }
        
        public TaskSequenceBuilder WithErrorLogging(bool enabled)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.EnableErrorLogging = enabled;
            return this;
        }
        
        public TaskSequenceBuilder WithInjectionLogging(bool enabled)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.EnableInjectionLogging = enabled;
            return this;
        }
        
        public TaskSequenceBuilder WithRepeat(bool enabled)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.Repeat = enabled;
            return this;
        }

        public TaskSequenceBuilder WithCriticalFlag(bool isCritical)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.IsCritical = isCritical;
            return this;
        }
        
        /// <summary>
        /// Configures which injection types are allowed while the sequence is in a critical section.
        /// Implies WithCriticalFlag(true). If not set, defaults to only InterruptSequenceInjection.
        /// </summary>
        public TaskSequenceBuilder WithCriticalAllowedInjections(params Type[] injectionTypes)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.IsCritical = true;
            _metadata.CriticalAllowedInjections = new HashSet<Type>(injectionTypes);
            return this;
        }

        public TaskSequenceBuilder WithConditionCheckTiming(EProcessStepTiming timing = EProcessStepTiming.Update)
        {
            _conditionStepTiming = timing;
            return this;
        }

        /// <summary>
        /// Sets the process lifecycle for this sequence.
        /// Use Synchronous for per-frame stepping without async overhead.
        /// Default is SelfTerminating (async).
        /// </summary>
        public TaskSequenceBuilder WithLifecycle(EProcessLifecycle lifecycle, EProcessStepTiming? timing = null)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.Lifecycle = lifecycle;
            return timing.HasValue ? WithStepTiming(timing.Value) : this;
        }

        /// <summary>
        /// Sets the step timing for the process.
        /// Required for Synchronous lifecycle (determines which Unity update loop to attach to).
        /// </summary>
        public TaskSequenceBuilder WithStepTiming(EProcessStepTiming timing)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.StepTiming = timing;
            return this;
        }

        /// <summary>
        /// Registers a per-step callback for a specific timing.
        /// Called each frame during sync execution with the data packet and deltaTime.
        /// </summary>
        public TaskSequenceBuilder OnStep(EProcessStepTiming timing, Action<SequenceDataPacket, float> callback)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.OnStepCallbacks ??= new Dictionary<EProcessStepTiming, Action<SequenceDataPacket, float>>();
            _metadata.OnStepCallbacks[timing] = callback;
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EVENT CALLBACKS (Sequence-level)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Called when sequence max duration is reached, before injection is applied.
        /// </summary>
        public TaskSequenceBuilder OnMaxDuration(Action<SequenceEventContext> handler)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.OnMaxDuration = handler;
            return this;
        }
        
        /// <summary>
        /// Called when sequence max duration is reached, before injection is applied.
        /// </summary>
        public TaskSequenceBuilder OnTimeout(Action<SequenceEventContext, SequenceTimeout> handler)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.OnTimeout = handler;
            return this;
        }
        
        /// <summary>
        /// Called when an exception occurs. Return true to suppress, false to propagate.
        /// </summary>
        public TaskSequenceBuilder OnException(Func<SequenceEventContext, Exception, bool> handler)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.OnException = handler;
            return this;
        }
        
        /// <summary>
        /// Called when sequence completes (success or failure), before repeating.
        /// </summary>
        public TaskSequenceBuilder OnComplete(Action<SequenceEventContext, bool> handler)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.OnComplete = handler;
            return this;
        }
        
        /// <summary>
        /// Called when sequence terminates (after all repeats, or on failure).
        /// </summary>
        public TaskSequenceBuilder OnTerminate(Action<SequenceEventContext, bool> handler)
        {
            _metadata ??= new SequenceMetadata();
            _metadata.OnTerminate = handler;
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STAGES
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceBuilder Stage(Action<StageBuilder> configure)
        {
            var builder = new StageBuilder();
            configure(builder);
            _stages.Add(builder.Build());
            return this;
        }
        
        public TaskSequenceBuilder Stage(SequenceStage stage)
        {
            _stages.Add(stage);
            return this;
        }
        
        public TaskSequenceBuilder Branch(Action<BranchBuilder> configure)
        {
            var builder = new BranchBuilder();
            configure(builder);
            _stages.Add(builder.Build());
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SIMPLE TASKS (creates single-task stages) - SequenceDataPacket
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceBuilder Task(Func<SequenceDataPacket, CancellationToken, UniTask> task)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(task));
            _stages.Add(stage);
            return this;
        }
        
        public TaskSequenceBuilder Task(Action<SequenceDataPacket> action)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(action));
            _stages.Add(stage);
            return this;
        }
        
        public TaskSequenceBuilder Task(Func<CancellationToken, UniTask> task)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(task));
            _stages.Add(stage);
            return this;
        }
        
        public TaskSequenceBuilder Task(ISequenceTask task)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(task);
            _stages.Add(stage);
            return this;
        }
        
        /// <summary>
        /// Adds a synchronous task that executes on the main thread.
        /// Forces non-async by only accepting Action (not Func returning UniTask).
        /// </summary>
        public TaskSequenceBuilder OnMainThread(Action<SequenceDataPacket> action)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new MainThreadSequenceTask(action));
            _stages.Add(stage);
            return this;
        }
        
        public TaskSequenceBuilder Delay(float seconds)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(async token =>
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
            }));
            _stages.Add(stage);
            return this;
        }
        
        /// <summary>
        /// Delays the sequence until the predicate returns true.
        /// Polls each frame (PlayerLoopTiming.Update) and yields between checks.
        /// Respects cancellation for interrupt/skip/timeout support.
        /// </summary>
        public TaskSequenceBuilder DelayUntil(Func<SequenceDataPacket, bool> predicate, 
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(async (SequenceDataPacket data, CancellationToken token) =>
            {
                await UniTask.WaitUntil(() => predicate(data), timing, cancellationToken: token);
            }));
            _stages.Add(stage);
            return this;
        }
        
        /// <summary>
        /// Delays the sequence until the predicate returns true.
        /// Simplified overload for conditions that don't need the data packet.
        /// </summary>
        public TaskSequenceBuilder DelayUntil(Func<bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(async (CancellationToken token) =>
            {
                await UniTask.WaitUntil(predicate, timing, cancellationToken: token);
            }));
            _stages.Add(stage);
            return this;
        }
        
        /// <summary>
        /// Delays the sequence while the predicate returns true (inverse of DelayUntil).
        /// Continues once the predicate returns false.
        /// </summary>
        public TaskSequenceBuilder DelayWhile(Func<SequenceDataPacket, bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(async (SequenceDataPacket data, CancellationToken token) =>
            {
                await UniTask.WaitWhile(() => predicate(data), timing, cancellationToken: token);
            }));
            _stages.Add(stage);
            return this;
        }
        
        /// <summary>
        /// Delays the sequence while the predicate returns true (inverse of DelayUntil).
        /// Simplified overload for conditions that don't need the data packet.
        /// </summary>
        public TaskSequenceBuilder DelayWhile(Func<bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSequenceTask(async (CancellationToken token) =>
            {
                await UniTask.WaitWhile(predicate, timing, cancellationToken: token);
            }));
            _stages.Add(stage);
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONDITIONS (AUTO-INJECTION) - GLOBAL
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceBuilder InjectWhen(
            Func<SequenceDataPacket, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
        {
            _conditions.Add(new SequenceCondition(predicate, injection, fireOnce));
            return this;
        }
        
        public TaskSequenceBuilder InjectWhen(
            Func<SequenceDataPacket, TaskSequenceRuntime, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
        {
            _conditions.Add(new SequenceCondition(predicate, injection, fireOnce));
            return this;
        }
        
        public TaskSequenceBuilder InterruptWhen(Func<SequenceDataPacket, bool> predicate, bool fireOnce = true)
        {
            return InjectWhen(predicate, InterruptSequenceInjection.Instance, fireOnce);
        }
        
        public TaskSequenceBuilder SkipStageWhen(Func<SequenceDataPacket, bool> predicate, bool fireOnce = false)
        {
            return InjectWhen(predicate, SkipStageInjection.Instance, fireOnce);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ALLOWED INJECTIONS - GLOBAL
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceBuilder AllowInjections(params Type[] injectionTypes)
        {
            _allowedInjections = new HashSet<Type>(injectionTypes);
            return this;
        }
        
        public TaskSequenceBuilder AllowOnlyInterrupt()
        {
            return AllowInjections(typeof(InterruptSequenceInjection));
        }
        
        public TaskSequenceBuilder DisallowInjections()
        {
            _allowedInjections = new HashSet<Type>();
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // MAX DURATION - GLOBAL
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceBuilder WithMaxDuration(float seconds, ISequenceInjection onTimeout = null)
        {
            _maxDurationSeconds = seconds;
            if (onTimeout is not null) _maxDurationInjection = onTimeout;
            return this;
        }
        
        /// <summary>
        /// Timeout
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="onTimeout"></param>
        /// <returns></returns>
        public TaskSequenceBuilder WithTimeout(float seconds, ISequenceInjection onTimeout = null)
        {
            _timeouts.Add(new SequenceTimeout()
            {
                Seconds = seconds, Injection = onTimeout ?? SkipStageInjection.Instance
            });
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SYNC TASKS (creates single-task stages with sync-first delegates)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adds a sync-first step task as a single-task stage.
        /// The task steps each frame and returns true when complete.
        /// </summary>
        public TaskSequenceBuilder SyncTask(Func<SequenceDataPacket, float, bool> step)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSyncTask(step));
            _stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Adds a sync-first step task (no data packet) as a single-task stage.
        /// </summary>
        public TaskSequenceBuilder SyncTask(Func<float, bool> step)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSyncTask(step));
            _stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Adds a sync-first ISequenceTask as a single-task stage.
        /// Useful for SyncSequenceTaskLibrary tasks.
        /// </summary>
        public TaskSequenceBuilder SyncTask(ISequenceTask task)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(task);
            _stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Adds a one-shot synchronous action as a single-task stage (completes in one frame).
        /// </summary>
        public TaskSequenceBuilder Do(Action<SequenceDataPacket> action)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSyncTask(action));
            _stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Adds a sync wait-for-condition stage.
        /// </summary>
        public TaskSequenceBuilder WaitUntil(Func<SequenceDataPacket, bool> predicate)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSyncTask((data, _) => predicate(data)));
            _stages.Add(stage);
            return this;
        }

        /// <summary>
        /// Adds a sync wait-for-condition stage (no data packet).
        /// </summary>
        public TaskSequenceBuilder WaitUntil(Func<bool> predicate)
        {
            var stage = new SequenceStage();
            stage.Tasks.Add(new DelegateSyncTask(_ => predicate()));
            _stages.Add(stage);
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // BUILD
        // ═══════════════════════════════════════════════════════════════════════════

        public TaskSequenceDefinition Build()
        {
            return new TaskSequenceDefinition(
                new List<SequenceStage>(_stages),
                new List<SequenceCondition>(_conditions),
                _allowedInjections,
                _maxDurationSeconds,
                _maxDurationInjection,
                _timeouts,
                _conditionStepTiming,
                _metadata
            );
        }
        
        public TaskSequence BuildSequence()
        {
            return new TaskSequence(Build());
        }

        /// <summary>
        /// Builds a SyncedTaskSequence for per-frame stepping.
        /// Converts SequenceStages into SyncSequenceStages using the unified ISequenceTask.Step path.
        /// </summary>
        public SyncedTaskSequence BuildSyncRunner()
        {
            var syncStages = new List<SyncSequenceStage>();

            foreach (var stage in _stages)
            {
                var policy = stage.Policy is WhenAnyStagePolicy
                    ? ESyncStagePolicy.WhenAny
                    : ESyncStagePolicy.WhenAll;

                var syncStage = new SyncSequenceStage(
                    new List<ISequenceTask>(stage.Tasks),
                    stage.Metadata?.Name,
                    policy,
                    stage.Repeat,
                    stage.MaxDurationSeconds,
                    stage.Metadata?.IsCritical ?? false
                );
                syncStages.Add(syncStage);
            }

            var name = _metadata?.Name ?? "SyncSequence";
            var repeat = _metadata?.Repeat ?? false;

            return new SyncedTaskSequence(syncStages, name, repeat);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // STAGE BUILDER
    // ═══════════════════════════════════════════════════════════════════════════
    
    public class StageBuilder
    {
        private readonly SequenceStage _stage = new();
        private SequenceStageMetadata _metadata;

        public bool IsCritical => _metadata?.IsCritical ?? false;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // METADATA
        // ═══════════════════════════════════════════════════════════════════════════
        
        public StageBuilder WithName(string name)
        {
            _metadata ??= new SequenceStageMetadata();
            _metadata.Name = name;
            return this;
        }
        
        public StageBuilder WithDescription(string description)
        {
            _metadata ??= new SequenceStageMetadata();
            _metadata.Description = description;
            return this;
        }
        
        public StageBuilder WithErrorLogging(bool enabled)
        {
            _metadata ??= new SequenceStageMetadata();
            _metadata.EnableErrorLogging = enabled;
            return this;
        }
        
        public StageBuilder WithInjectionLogging(bool enabled)
        {
            _metadata ??= new SequenceStageMetadata();
            _metadata.EnableInjectionLogging = enabled;
            return this;
        }

        public StageBuilder WithCriticalFlag(bool isCritical)
        {
            _metadata ??= new SequenceStageMetadata();
            _metadata.IsCritical = isCritical;
            return this;
        }
        
        /// <summary>
        /// Configures which injection types are allowed while this critical stage is active.
        /// Implies WithCriticalFlag(true). If not set, defaults to only InterruptSequenceInjection.
        /// Overrides sequence-level CriticalAllowedInjections for this stage.
        /// </summary>
        public StageBuilder WithCriticalAllowedInjections(params Type[] injectionTypes)
        {
            _metadata ??= new SequenceStageMetadata();
            _metadata.IsCritical = true;
            _metadata.CriticalAllowedInjections = new HashSet<Type>(injectionTypes);
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EVENT CALLBACKS (Stage-level)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Called when this stage's timeout is triggered, before injection is applied.
        /// </summary>
        public StageBuilder OnTimeout(Action<SequenceEventContext, SequenceTimeout> handler)
        {
            _stage.OnTimeout = handler;
            return this;
        }
        
        /// <summary>
        /// Called when this stage's timeout is triggered, before injection is applied.
        /// </summary>
        public StageBuilder OnTimeout(Action<SequenceEventContext> handler)
        {
            _stage.OnTimeout = (ctx, to) =>
            {
                handler.Invoke(ctx);
            };
            return this;
        }
        
        /// <summary>
        /// Called when this stage's timeout is triggered, before injection is applied.
        /// </summary>
        public StageBuilder OnTimeout(Action handler)
        {
            _stage.OnTimeout = (_, _) =>
            {
                handler.Invoke();
            };
            return this;
        }
        
        /// <summary>
        /// Called when this stage's max duration is reached, before injection is applied.
        /// </summary>
        public StageBuilder OnMaxDuration(Action<SequenceEventContext> handler)
        {
            _stage.OnMaxDuration = handler;
            return this;
        }
        
        /// <summary>
        /// Called when this stage's timeout is triggered, before injection is applied.
        /// </summary>
        public StageBuilder OnMaxDuration(Action handler)
        {
            _stage.OnMaxDuration = _ =>
            {
                handler.Invoke();
            };
            return this;
        }
        
        /// <summary>
        /// Called when an exception occurs in this stage.
        /// Return true to suppress (skip stage), false to propagate (break sequence).
        /// </summary>
        public StageBuilder OnException(Func<SequenceEventContext, Exception, bool> handler)
        {
            _stage.OnException = handler;
            return this;
        }
        
        /// <summary>
        /// Called when a repeat iteration completes (for WithRepeat or RepeatUntilSkipped).
        /// </summary>
        public StageBuilder OnRepeat(Action<SequenceEventContext> handler)
        {
            _stage.OnRepeat = handler;
            return this;
        }
        
        /// <summary>
        /// Called when a repeat iteration completes (for WithRepeat or RepeatUntilSkipped).
        /// </summary>
        public StageBuilder OnRepeat(Action handler)
        {
            _stage.OnRepeat = _ => handler();
            return this;
        }
        
        /// <summary>
        /// Called when sequence terminates (after all repeats, or on failure).
        /// </summary>
        public StageBuilder OnTerminate(Action<SequenceEventContext, bool> handler)
        {
            _stage.OnTerminate = handler;
            return this;
        }
        
        /// <summary>
        /// Called when sequence terminates (after all repeats, or on failure).
        /// </summary>
        public StageBuilder OnTerminate(Action handler)
        {
            _stage.OnTerminate = (_, _) => handler();
            return this;
        }

        public StageBuilder OnInit(Action<SequenceDataPacket> handler)
        {
            _stage.OnInit = handler;
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TASKS - SequenceDataPacket
        // ═══════════════════════════════════════════════════════════════════════════
        
        public StageBuilder Task(Func<SequenceDataPacket, CancellationToken, UniTask> task)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(task));
            return this;
        }
        
        public StageBuilder Task(Func<CancellationToken, UniTask> task)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(task));
            return this;
        }
        
        public StageBuilder Task(Action<SequenceDataPacket> action)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(action));
            return this;
        }
        
        public StageBuilder Task(ISequenceTask task)
        {
            _stage.Tasks.Add(task);
            return this;
        }
        
        /// <summary>
        /// Adds a synchronous task that executes on the main thread.
        /// Forces non-async by only accepting Action (not Func returning UniTask).
        /// </summary>
        public StageBuilder OnMainThread(Action<SequenceDataPacket> action)
        {
            _stage.Tasks.Add(new MainThreadSequenceTask(action));
            return this;
        }
        
        public StageBuilder Delay(float seconds)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(async token =>
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token);
            }));
            return this;
        }
        
        /// <summary>
        /// Adds a task that delays until the predicate returns true.
        /// Polls each frame (PlayerLoopTiming.Update) and yields between checks.
        /// Behaves like Delay() - adds an inline task to this stage.
        /// </summary>
        public StageBuilder DelayUntil(Func<SequenceDataPacket, bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(async (SequenceDataPacket data, CancellationToken token) =>
            {
                await UniTask.WaitUntil(() => predicate(data), timing, cancellationToken: token);
            }));
            return this;
        }
        
        /// <summary>
        /// Adds a task that delays until the predicate returns true.
        /// Simplified overload for conditions that don't need the data packet.
        /// </summary>
        public StageBuilder DelayUntil(Func<bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(async (CancellationToken token) =>
            {
                await UniTask.WaitUntil(predicate, timing, cancellationToken: token);
            }));
            return this;
        }
        
        /// <summary>
        /// Adds a task that delays while the predicate returns true (inverse of DelayUntil).
        /// Continues once the predicate returns false.
        /// </summary>
        public StageBuilder DelayWhile(Func<SequenceDataPacket, bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(async (SequenceDataPacket data, CancellationToken token) =>
            {
                await UniTask.WaitWhile(() => predicate(data), timing, cancellationToken: token);
            }));
            return this;
        }
        
        /// <summary>
        /// Adds a task that delays while the predicate returns true (inverse of DelayUntil).
        /// Simplified overload for conditions that don't need the data packet.
        /// </summary>
        public StageBuilder DelayWhile(Func<bool> predicate,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            _stage.Tasks.Add(new DelegateSequenceTask(async (CancellationToken token) =>
            {
                await UniTask.WaitWhile(predicate, timing, cancellationToken: token);
            }));
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SYNC TASKS (sync-first delegates for stage-level)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adds a sync-first step task to this stage.
        /// </summary>
        public StageBuilder SyncTask(Func<SequenceDataPacket, float, bool> step)
        {
            _stage.Tasks.Add(new DelegateSyncTask(step));
            return this;
        }

        /// <summary>
        /// Adds a sync-first step task (no data packet) to this stage.
        /// </summary>
        public StageBuilder SyncTask(Func<float, bool> step)
        {
            _stage.Tasks.Add(new DelegateSyncTask(step));
            return this;
        }

        /// <summary>
        /// Adds a one-shot synchronous action (completes in one frame).
        /// </summary>
        public StageBuilder Do(Action<SequenceDataPacket> action)
        {
            _stage.Tasks.Add(new DelegateSyncTask(action));
            return this;
        }

        /// <summary>
        /// Adds a sync wait-for-condition task.
        /// </summary>
        public StageBuilder WaitUntil(Func<SequenceDataPacket, bool> predicate)
        {
            _stage.Tasks.Add(new DelegateSyncTask((data, _) => predicate(data)));
            return this;
        }

        /// <summary>
        /// Adds a sync wait-for-condition task (no data packet).
        /// </summary>
        public StageBuilder WaitUntil(Func<bool> predicate)
        {
            _stage.Tasks.Add(new DelegateSyncTask(_ => predicate()));
            return this;
        }

        /// <summary>
        /// Adds a sync delay task that waits for the given number of seconds.
        /// </summary>
        public StageBuilder SyncDelay(float seconds)
        {
            float elapsed = 0f;
            _stage.Tasks.Add(new DelegateSyncTask(
                step: (_, dt) => { elapsed += dt; return elapsed >= seconds; },
                prepare: _ => { elapsed = 0f; }
            ));
            return this;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SUB-STAGES
        // ═══════════════════════════════════════════════════════════════════════════
        
        public StageBuilder SubStage(Action<StageBuilder> configure)
        {
            var builder = new StageBuilder();
            configure(builder);
            _stage.SubStages.Add(builder.Build());
            return this;
        }
        
        public StageBuilder SubStage(SequenceStage subStage)
        {
            _stage.SubStages.Add(subStage);
            return this;
        }
        
        public StageBuilder Branch(Action<BranchBuilder> configure)
        {
            var builder = new BranchBuilder();
            configure(builder);
            _stage.SubStages.Add(builder.Build());
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE POLICY
        // ═══════════════════════════════════════════════════════════════════════════
        
        public StageBuilder WhenAll()
        {
            _stage.Policy = WhenAllStagePolicy.Instance;
            return this;
        }
        
        public StageBuilder WhenAny()
        {
            _stage.Policy = WhenAnyStagePolicy.Instance;
            return this;
        }
        
        public StageBuilder WhenN(int count)
        {
            _stage.Policy = new WhenNStagePolicy(count);
            return this;
        }
        
        public StageBuilder WithPolicy(ISequenceStagePolicy policy)
        {
            _stage.Policy = policy;
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // REPEAT
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Enables repeating this stage until it is broken, skipped, or interrupted.
        /// Works similar to sequence-level repeat but for a single stage.
        /// Use BreakStageRepeatInjection or data.BreakStageRepeat() to exit.
        /// </summary>
        public StageBuilder WithRepeat(bool enabled)
        {
            _stage.Repeat = enabled;
            return this;
        }
        
        /// <summary>
        /// Sets the policy to RepeatUntilSkippedPolicy.
        /// Stage will repeat infinitely until skipped, interrupted, or broken.
        /// </summary>
        public StageBuilder WithRepeatAndDelay(float? delayBetweenRepeats = null)
        {
            _stage.Policy = delayBetweenRepeats > 0 
                ? new RepeatUntilSkippedPolicy(delayBetweenRepeats)
                : RepeatUntilSkippedPolicy.Instance;
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TIMEOUT
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Adds a timeout to this stage. Injection defaults to SkipStageInjection.
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="onTimeout"></param>
        /// <returns></returns>
        public StageBuilder WithTimeout(float seconds, ISequenceInjection onTimeout = null)
        {
            _stage.Timeouts.Add(new SequenceTimeout()
            {
                Seconds = seconds,
                Injection = onTimeout ?? SkipStageInjection.Instance
            });
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONDITIONS (AUTO-INJECTION) - STAGE LOCAL
        // ═══════════════════════════════════════════════════════════════════════════
        
        public StageBuilder InjectWhen(
            Func<SequenceDataPacket, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
        {
            var condition = new SequenceCondition(predicate, injection, fireOnce);
            condition.IsStageLocal = true;
            _stage.Conditions.Add(condition);
            return this;
        }
        
        public StageBuilder InjectWhen(
            Func<SequenceDataPacket, TaskSequenceRuntime, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
        {
            var condition = new SequenceCondition(predicate, injection, fireOnce);
            condition.IsStageLocal = true;
            _stage.Conditions.Add(condition);
            return this;
        }
        
        public StageBuilder InterruptWhen(Func<SequenceDataPacket, bool> predicate, bool fireOnce = true)
        {
            return InjectWhen(predicate, InterruptSequenceInjection.Instance, fireOnce);
        }
        
        public StageBuilder SkipWhen(Func<SequenceDataPacket, bool> predicate, bool fireOnce = true)
        {
            return InjectWhen(predicate, SkipStageInjection.Instance, fireOnce);
        }
        
        /// <summary>
        /// Breaks the stage repeat loop when condition is met.
        /// </summary>
        public StageBuilder StopRepeatWhen(Func<SequenceDataPacket, bool> predicate, bool fireOnce = true)
        {
            return InjectWhen(predicate, BreakStageRepeatInjection.Instance, fireOnce);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ALLOWED INJECTIONS - STAGE LOCAL
        // ═══════════════════════════════════════════════════════════════════════════
        
        public StageBuilder AllowInjections(params Type[] injectionTypes)
        {
            _stage.AllowedInjections = new HashSet<Type>(injectionTypes);
            return this;
        }
        
        public StageBuilder AllowOnlyInterrupt()
        {
            return AllowInjections(typeof(InterruptSequenceInjection));
        }
        
        public StageBuilder DisallowInjections()
        {
            _stage.AllowedInjections = new HashSet<Type>();
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // MAX DURATION - STAGE LOCAL
        // ═══════════════════════════════════════════════════════════════════════════
        
        public StageBuilder WithMaxDuration(float seconds, ISequenceInjection onTimeout = null)
        {
            _stage.MaxDurationSeconds = seconds;
            _stage.MaxDurationInjection = onTimeout ?? SkipStageInjection.Instance;
            return this;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // BUILD
        // ═══════════════════════════════════════════════════════════════════════════
        
        internal SequenceStage Build()
        {
            _stage.Metadata = _metadata;
            return _stage;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // BRANCH BUILDER
    // ═══════════════════════════════════════════════════════════════════════════
    
    public class BranchBuilder
    {
        private readonly BranchStage _branch = new();
        
        public BranchBuilder If(Func<SequenceDataPacket, bool> condition, Action<StageBuilder> stage)
        {
            var stageBuilder = new StageBuilder();
            stage(stageBuilder);
            _branch.Branches.Add(new BranchCase(condition, stageBuilder.Build()));
            return this;
        }
        
        public BranchBuilder If(Func<SequenceDataPacket, bool> condition, SequenceStage stage)
        {
            _branch.Branches.Add(new BranchCase(condition, stage));
            return this;
        }
        
        public BranchBuilder Default(Action<StageBuilder> stage)
        {
            var stageBuilder = new StageBuilder();
            stage(stageBuilder);
            _branch.DefaultBranch = stageBuilder.Build();
            return this;
        }
        
        public BranchBuilder Default(SequenceStage stage)
        {
            _branch.DefaultBranch = stage;
            return this;
        }
        
        internal BranchStage Build()
        {
            return _branch;
        }
    }
}