using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════
    // SEQUENCE TASK INTERFACE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Unified interface for tasks that can be executed within a TaskSequence.
    /// Supports both async execution (via Execute) and synchronous per-frame stepping (via Step).
    /// Task authors implement whichever path(s) their task will be used under.
    /// </summary>
    public interface ISequenceTask
    {
        /// <summary>
        /// Called before Execute or the first Step. Use for setup/initialization.
        /// </summary>
        void Prepare(SequenceDataPacket data);

        /// <summary>
        /// The main async execution of the task. Used when the sequence runs asynchronously.
        /// </summary>
        UniTask Execute(SequenceDataPacket data, CancellationToken token);

        /// <summary>
        /// Called each frame when the sequence runs synchronously.
        /// Returns true when the task is complete.
        /// </summary>
        bool Step(SequenceDataPacket data, float deltaTime);

        /// <summary>
        /// Called after Execute completes or after Step returns true. Use for cleanup.
        /// </summary>
        void Clean(SequenceDataPacket data);
    }

    /// <summary>
    /// Base class for async-first sequence tasks.
    /// Execute is abstract (must implement). Step defaults to instant completion (returns true).
    /// If you need your task to work in synchronous sequences, override Step.
    /// </summary>
    public abstract class SequenceTaskBase : ISequenceTask
    {
        public virtual void Prepare(SequenceDataPacket data) { }
        public abstract UniTask Execute(SequenceDataPacket data, CancellationToken token);

        /// <summary>
        /// Default: completes instantly. Override for synchronous support.
        /// </summary>
        public virtual bool Step(SequenceDataPacket data, float deltaTime) => true;
        public virtual void Clean(SequenceDataPacket data) { }
    }

    /// <summary>
    /// Base class for sync-first sequence tasks (per-frame stepping).
    /// Step is abstract (must implement). Execute bridges automatically by looping Step with per-frame yields.
    /// </summary>
    public abstract class SyncSequenceTaskBase : ISequenceTask
    {
        public virtual void Prepare(SequenceDataPacket data) { }

        /// <summary>
        /// Bridge: loops Step with per-frame yields for async execution.
        /// Override if you need custom async behavior.
        /// </summary>
        public virtual async UniTask Execute(SequenceDataPacket data, CancellationToken token)
        {
            while (!Step(data, UnityEngine.Time.deltaTime))
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        public abstract bool Step(SequenceDataPacket data, float deltaTime);
        public virtual void Clean(SequenceDataPacket data) { }
    }
    
    /// <summary>
    /// Wraps a delegate as a sequence task.
    /// </summary>
    public class DelegateSequenceTask : ISequenceTask
    {
        private readonly Func<SequenceDataPacket, CancellationToken, UniTask> _execute;
        private readonly Action<SequenceDataPacket> _prepare;
        private readonly Action<SequenceDataPacket> _clean;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTORS - SequenceDataPacket (preferred)
        // ═══════════════════════════════════════════════════════════════════════════
        
        public DelegateSequenceTask(
            Func<SequenceDataPacket, CancellationToken, UniTask> execute,
            Action<SequenceDataPacket> prepare = null,
            Action<SequenceDataPacket> clean = null)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            _execute = (data, token) => execute(data as SequenceDataPacket ?? SequenceDataPacket.SceneRoot(), token);
            _prepare = prepare != null ? d => prepare(d as SequenceDataPacket ?? SequenceDataPacket.SceneRoot()) : null;
            _clean = clean != null ? d => clean(d as SequenceDataPacket ?? SequenceDataPacket.SceneRoot()) : null;
        }
        
        /// <summary>
        /// Creates a task from a synchronous action using SequenceDataPacket.
        /// </summary>
        public DelegateSequenceTask(Action<SequenceDataPacket> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _execute = (data, token) => ExecuteSyncActionSafe(action, data as SequenceDataPacket ?? SequenceDataPacket.SceneRoot(), token);
        }
        
        private static async UniTask ExecuteSyncActionSafe(Action<SequenceDataPacket> action, SequenceDataPacket data, CancellationToken token)
        {
            // Yield first to ensure proper async context
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            
            // Execute action - any exception will propagate through the async state machine
            action(data);
        }
        
        /// <summary>
        /// Creates a task from a simple async delegate (no data packet).
        /// </summary>
        public DelegateSequenceTask(Func<CancellationToken, UniTask> execute)
        {
            _execute = (_, token) => execute(token);
        }
        
        public void Prepare(SequenceDataPacket data) => _prepare?.Invoke(data);
        public UniTask Execute(SequenceDataPacket data, CancellationToken token) => _execute(data, token);
        public bool Step(SequenceDataPacket data, float deltaTime) => true; // Async delegate tasks complete instantly in sync mode
        public void Clean(SequenceDataPacket data) => _clean?.Invoke(data);
    }
    
    /// <summary>
    /// Task that executes a synchronous action on the main thread.
    /// Forces non-async execution by only accepting Action delegates.
    /// </summary>
    public class MainThreadSequenceTask : ISequenceTask
    {
        private readonly Action<SequenceDataPacket> _action;
        private readonly Action<SequenceDataPacket> _prepare;
        private readonly Action<SequenceDataPacket> _clean;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTORS - SequenceDataPacket (preferred)
        // ═══════════════════════════════════════════════════════════════════════════
        
        public MainThreadSequenceTask(Action<SequenceDataPacket> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _action = data => action(data as SequenceDataPacket ?? SequenceDataPacket.SceneRoot());
        }
        
        public MainThreadSequenceTask(
            Action<SequenceDataPacket> action,
            Action<SequenceDataPacket> prepare,
            Action<SequenceDataPacket> clean)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _action = data => action(data as SequenceDataPacket ?? SequenceDataPacket.SceneRoot());
            _prepare = prepare != null ? d => prepare(d as SequenceDataPacket ?? SequenceDataPacket.SceneRoot()) : null;
            _clean = clean != null ? d => clean(d as SequenceDataPacket ?? SequenceDataPacket.SceneRoot()) : null;
        }
        
        public void Prepare(SequenceDataPacket data) => _prepare?.Invoke(data);

        public async UniTask Execute(SequenceDataPacket data, CancellationToken token)
        {
            await UniTask.SwitchToMainThread(token);
            _action(data);
        }

        public bool Step(SequenceDataPacket data, float deltaTime)
        {
            _action(data);
            return true;
        }

        public void Clean(SequenceDataPacket data) => _clean?.Invoke(data);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // STAGE POLICY INTERFACE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stage completes when ALL tasks complete.
    /// </summary>
    public class WhenAllStagePolicy : ISequenceStagePolicy
    {
        public static readonly WhenAllStagePolicy Instance = new();
        
        public async UniTask AwaitCompletion(
            TaskSequenceRuntime runtime,
            SequenceStage stage,
            UniTask[] taskUnits,
            CancellationTokenSource stageCts)
        {
            // Use explicit completion tracking to avoid UniTask.WhenAll edge cases
            if (taskUnits.Length == 0) return;
            
            Exception firstError = null;
            int completed = 0;
            var tcs = new UniTaskCompletionSource();
            
            foreach (var task in taskUnits)
            {
                WatchTaskCompletion(task, taskUnits.Length).Forget();
            }
            
            await tcs.Task;
            
            // If there was an error, throw it
            if (firstError != null)
            {
                throw firstError;
            }
            
            async UniTaskVoid WatchTaskCompletion(UniTask task, int totalCount)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is expected, don't treat as error
                }
                catch (Exception ex)
                {
                    // Store first error
                    if (firstError == null)
                    {
                        firstError = ex;
                    }
                }
                
                completed++;
                if (completed >= totalCount)
                {
                    tcs.TrySetResult();
                }
            }
        }
    }
    
    /// <summary>
    /// Stage completes when ANY task completes. Cancels remaining tasks.
    /// </summary>
    public class WhenAnyStagePolicy : ISequenceStagePolicy
    {
        public static readonly WhenAnyStagePolicy Instance = new();
        
        public async UniTask AwaitCompletion(
            TaskSequenceRuntime runtime,
            SequenceStage stage,
            UniTask[] taskUnits,
            CancellationTokenSource stageCts)
        {
            if (taskUnits.Length == 0) return;
            
            Exception firstError = null;
            var tcs = new UniTaskCompletionSource();
            
            foreach (var task in taskUnits)
            {
                WatchTaskCompletion(task).Forget();
            }
            
            await tcs.Task;
            stageCts.Cancel();
            
            // If the first completion was an error, throw it
            if (firstError != null)
            {
                throw firstError;
            }
            
            async UniTaskVoid WatchTaskCompletion(UniTask task)
            {
                try
                {
                    await task;
                    tcs.TrySetResult(); // First successful completion wins
                }
                catch (OperationCanceledException)
                {
                    // Ignore cancellation
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                    {
                        firstError = ex;
                    }
                    tcs.TrySetResult(); // Error also counts as completion
                }
            }
        }
    }
    
    /// <summary>
    /// Stage completes when N tasks complete. Cancels remaining tasks.
    /// </summary>
    public class WhenNStagePolicy : ISequenceStagePolicy
    {
        public readonly int RequiredCount;
        
        public WhenNStagePolicy(int requiredCount)
        {
            RequiredCount = requiredCount;
        }
        
        public async UniTask AwaitCompletion(
            TaskSequenceRuntime runtime,
            SequenceStage stage,
            UniTask[] taskUnits,
            CancellationTokenSource stageCts)
        {
            if (taskUnits.Length == 0) return;
            
            int completed = 0;
            Exception firstError = null;
            var tcs = new UniTaskCompletionSource();
            
            foreach (var task in taskUnits)
            {
                WatchTask(task).Forget();
            }
            
            await tcs.Task;
            stageCts.Cancel();
            
            // If we completed with errors, throw the first one
            if (firstError != null)
            {
                throw firstError;
            }
            
            async UniTaskVoid WatchTask(UniTask task)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    return; // Cancellation doesn't count as completion
                }
                catch (Exception ex)
                {
                    if (firstError == null)
                    {
                        firstError = ex;
                    }
                }
                
                completed++;
                if (completed >= RequiredCount)
                {
                    tcs.TrySetResult();
                }
            }
        }
    }
    
    /// <summary>
    /// Stage repeats infinitely until explicitly skipped, interrupted, or broken.
    /// Maintained stages with this policy will continue repeating.
    /// Use BreakStageRepeatInjection to exit the repeat loop gracefully.
    /// </summary>
    public class RepeatUntilSkippedPolicy : ISequenceStagePolicy
    {
        public static readonly RepeatUntilSkippedPolicy Instance = new();
        
        /// <summary>
        /// Optional delay between repetitions in seconds.
        /// </summary>
        public float? DelayBetweenRepeats { get; set; }
        
        public RepeatUntilSkippedPolicy() { }
        
        public RepeatUntilSkippedPolicy(float? delayBetweenRepeats)
        {
            DelayBetweenRepeats = delayBetweenRepeats;
        }
        
        public async UniTask AwaitCompletion(
            TaskSequenceRuntime runtime,
            SequenceStage stage,
            UniTask[] taskUnits,
            CancellationTokenSource stageCts)
        {
            var token = stageCts.Token;
            
            while (!token.IsCancellationRequested && !stage.RepeatBroken)
            {
                // Wait for all tasks to complete using safe pattern
                await WaitAllTasksSafe(taskUnits);
                
                // Check if we should break
                if (token.IsCancellationRequested || stage.RepeatBroken)
                    break;
                
                // Optional delay between repetitions
                if (DelayBetweenRepeats.HasValue && DelayBetweenRepeats.Value > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(DelayBetweenRepeats.Value), cancellationToken: token);
                }
                
                // Recreate task units for next iteration
                // Note: Prepare is NOT called again, only Execute
                taskUnits = stage.CreateExecutionUnits(runtime, stage, runtime.Data, token);
                // taskUnits = stage.CreateExecutionUnits(runtime, runtime.Data, token);
            }
        }
        
        private static async UniTask WaitAllTasksSafe(UniTask[] taskUnits)
        {
            if (taskUnits.Length == 0) return;
            
            Exception firstError = null;
            int completed = 0;
            var tcs = new UniTaskCompletionSource();
            
            foreach (var task in taskUnits)
            {
                WatchTask(task).Forget();
            }
            
            await tcs.Task;
            
            if (firstError != null)
            {
                throw firstError;
            }
            
            async UniTaskVoid WatchTask(UniTask task)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
                catch (Exception ex)
                {
                    if (firstError == null) firstError = ex;
                }
                
                completed++;
                if (completed >= taskUnits.Length)
                {
                    tcs.TrySetResult();
                }
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // INJECTION INTERFACE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// An action that can modify sequence execution at runtime.
    /// </summary>
    public interface ISequenceInjection
    {
        /// <summary>
        /// Applies the injection to the runtime.
        /// </summary>
        /// <returns>True if injection was successfully applied</returns>
        bool Apply(TaskSequenceRuntime runtime);
    }

    public class DelegateSequenceInjection : ISequenceInjection
    {
        public DelegateSequenceInjection()
        {
        }

        public Func<TaskSequenceRuntime, bool> applyFunc;
        public DelegateSequenceInjection(Func<TaskSequenceRuntime, bool> applyFunc)
        {
            this.applyFunc = applyFunc;
        }

        public bool Apply(TaskSequenceRuntime runtime)
        {
            return applyFunc?.Invoke(runtime) ?? false;
        }
    }
    
    /// <summary>
    /// Interrupts the entire sequence.
    /// </summary>
    public class InterruptSequenceInjection : ISequenceInjection
    {
        public static readonly InterruptSequenceInjection Instance = new();
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            runtime.RequestInterrupt();
            return true;
        }
    }
    
    /// <summary>
    /// Skips the current stage and moves to the next.
    /// </summary>
    public class SkipStageInjection : ISequenceInjection
    {
        public static readonly SkipStageInjection Instance = new();
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            return runtime.RequestSkipCurrentStage();
        }
    }
    
    /// <summary>
    /// Skips the current stage but maintains it running in background.
    /// </summary>
    public class SkipAndMaintainInjection : ISequenceInjection
    {
        public static readonly SkipAndMaintainInjection Instance = new();
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            return runtime.RequestSkipAndMaintain();
        }
    }
    
    /// <summary>
    /// Stops the oldest maintained stage.
    /// </summary>
    public class StopMaintainedLastInjection : ISequenceInjection
    {
        public static readonly StopMaintainedLastInjection Instance = new();
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            return runtime.RequestStopMaintainedLast();
        }
    }
    
    /// <summary>
    /// Stops the oldest maintained stage.
    /// </summary>
    public class StopMaintainedAtIndexInjection : ISequenceInjection
    {
        public int TargetMaintainedIndex;

        public StopMaintainedAtIndexInjection()
        {
        }

        public StopMaintainedAtIndexInjection(int targetMaintainedIndex)
        {
            TargetMaintainedIndex = targetMaintainedIndex;
        }

        public bool Apply(TaskSequenceRuntime runtime)
        {
            return runtime.RequestStopMaintainedAt(TargetMaintainedIndex);
        }
    }
    
    /// <summary>
    /// Stops all maintained stages.
    /// </summary>
    public class StopMaintainedAllInjection : ISequenceInjection
    {
        public static readonly StopMaintainedAllInjection Instance = new();
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            return runtime.RequestStopMaintainedAll();
        }
    }
    
    /// <summary>
    /// Jumps to a specific stage by index.
    /// </summary>
    public class JumpToStageInjection : ISequenceInjection
    {
        public int TargetStageIndex;

        public JumpToStageInjection()
        {
        }

        public JumpToStageInjection(int targetStageIndex)
        {
            TargetStageIndex = targetStageIndex;
        }
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            return runtime.RequestJumpToStage(TargetStageIndex);
        }
    }
    
    /// <summary>
    /// Breaks out of a stage's repeat loop (for WithRepeat or RepeatUntilSkippedPolicy).
    /// Does not skip the stage - just stops repeating and continues normally.
    /// </summary>
    public class BreakStageRepeatInjection : ISequenceInjection
    {
        public static readonly BreakStageRepeatInjection Instance = new();
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            return runtime.RequestBreakCurrentStageRepeat();
        }
    }
    
    /// <summary>
    /// Marker injection for chain-level interrupts.
    /// </summary>
    public class ChainInterruptInjection : ISequenceInjection
    {
        public static readonly ChainInterruptInjection Instance = new();
        
        public bool Apply(TaskSequenceRuntime runtime)
        {
            // This is handled at the chain level, not runtime level
            runtime.RequestInterrupt();
            return true;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // CONDITION FOR AUTO-INJECTION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A condition that triggers an injection when met.
    /// </summary>
    public class SequenceCondition
    {
        /// <summary>
        /// Predicate evaluated each update tick.
        /// </summary>
        public Func<SequenceDataPacket, TaskSequenceRuntime, bool> Predicate { get; }
        
        /// <summary>
        /// Injection to apply when condition is met.
        /// </summary>
        public ISequenceInjection Injection { get; }
        
        /// <summary>
        /// If true, condition only fires once then is disabled.
        /// </summary>
        public bool FireOnce { get; }
        
        /// <summary>
        /// Has this condition already fired (for FireOnce conditions).
        /// </summary>
        public bool HasFired { get; private set; }
        
        /// <summary>
        /// If true, this is a stage-local condition that respects stage AllowedInjections.
        /// </summary>
        public bool IsStageLocal { get; set; }
        
        public SequenceCondition(
            Func<SequenceDataPacket, TaskSequenceRuntime, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
        {
            Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            Injection = injection ?? throw new ArgumentNullException(nameof(injection));
            FireOnce = fireOnce;
        }
        
        /// <summary>
        /// Simplified constructor without runtime access.
        /// </summary>
        public SequenceCondition(
            Func<SequenceDataPacket, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
            : this((data, _) => predicate(data), injection, fireOnce)
        {
        }
        
        /// <summary>
        /// Checks the condition and applies injection if met.
        /// Does NOT check permissions - caller is responsible for that.
        /// </summary>
        /// <returns>True if injection was applied</returns>
        public bool CheckAndApply(SequenceDataPacket data, TaskSequenceRuntime runtime)
        {
            if (FireOnce && HasFired) return false;
            
            if (Predicate(data, runtime))
            {
                HasFired = true;
                return Injection.Apply(runtime);
            }
            
            return false;
        }
        
        /// <summary>
        /// Checks condition, respects permissions, and applies injection if allowed.
        /// </summary>
        /// <returns>True if injection was applied</returns>
        public bool CheckAndApplyWithPermission(SequenceDataPacket data, TaskSequenceRuntime runtime, SequenceStage stage = null)
        {
            if (FireOnce && HasFired) return false;
            
            if (Predicate(data, runtime))
            {
                // Check permissions
                if (!runtime.Definition.IsInjectionAllowed(Injection, stage))
                {
                    return false;
                }
                
                HasFired = true;
                return Injection.Apply(runtime);
            }
            
            return false;
        }
        
        /// <summary>
        /// Resets the fired state for reuse.
        /// </summary>
        public void Reset()
        {
            HasFired = false;
        }
        
        /// <summary>
        /// Marks this condition as fired (for external handling).
        /// </summary>
        public void MarkFired()
        {
            HasFired = true;
        }
    }
}