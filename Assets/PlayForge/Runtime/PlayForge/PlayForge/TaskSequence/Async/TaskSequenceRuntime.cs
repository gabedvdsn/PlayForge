using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Manages the runtime execution state of a TaskSequence.
    /// Created per-run, handles stage progression, injections, and cancellation.
    /// </summary>
    public class TaskSequenceRuntime
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>The sequence definition being executed.</summary>
        public TaskSequenceDefinition Definition { get; }
        
        /// <summary>The data packet flowing through the sequence.</summary>
        public SequenceDataPacket Data { get; private set; }
        
        /// <summary>Current top-level stage index (-1 = not started).</summary>
        public int StageIndex { get; private set; } = -1;
        
        /// <summary>The currently active stage (null if not running).</summary>
        public SequenceStage CurrentStage { get; private set; }
        
        /// <summary>True while the sequence is actively running.</summary>
        public bool IsRunning { get; private set; }
        
        /// <summary>True if the sequence completed successfully.</summary>
        public bool CompletedSuccessfully { get; private set; }
        
        /// <summary>Exception that caused the sequence to fail, if any.</summary>
        public Exception FailureException { get; private set; }
        
        /// <summary>Time elapsed since sequence started.</summary>
        public float ElapsedTime { get; private set; }

        /// <summary>
        /// True if the sequence is currently inside a critical section.
        /// When true, injections are restricted to only those allowed by the critical policy.
        /// </summary>
        public bool IsCriticalSection => (Definition.Metadata?.IsCritical ?? false) || _criticalFlagLocks > 0;
        
        // Cancellation management
        private CancellationTokenSource _sequenceCts;
        private readonly Dictionary<int, CancellationTokenSource> _stageSources = new();
        private UniTaskCompletionSource _nextStageSignal;
        private int _maintainedStages;
        
        // Jump request
        private int? _jumpToStageRequest;
        
        // Stage error storage (safer than TrySetException which can cause issues)
        private Exception _pendingStageError;
        
        // Critical section tracking (incremented/decremented at stage push/pop level)
        private int _criticalFlagLocks;
        private bool _criticalExitFired;
        private int _lastCriticalStageIndex;

        /// <summary>
        /// Fired once when the last critical section in this sequence exits.
        /// For whole-sequence-critical sequences, fires when the sequence terminates.
        /// For stage-level critical sequences, fires when the last critical stage pops.
        /// </summary>
        public Action OnCriticalSectionExited { get; set; }
        
        // Tasks being cleaned (for error handling)
        private readonly List<ISequenceTask> _activeTasks = new();
        private readonly object _activeTasksLock = new();
        
        // Active stages stack for condition checking (supports nested stages)
        private readonly Stack<SequenceStage> _activeStageStack = new();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceRuntime(TaskSequenceDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════

        public async UniTask ExecuteTaskUnit(ISequenceTask task, SequenceDataPacket data, CancellationToken token)
        {
            try
            {
                await task.Execute(data, token);
            }
            catch (OperationCanceledException)
            {
                throw; // Propagate cancellation
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EXECUTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Runs the sequence to completion.
        /// </summary>
        public async UniTask Run(ProcessDataPacket data, CancellationToken externalToken)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("Sequence is already running");
            }
            
            // Set runtime reference on SequenceDataPacket
            if (data is SequenceDataPacket seqData) Data = seqData;
            else throw new InvalidOperationException(nameof(data));
            
            seqData.Runtime = this;
            
            // Use loop for repeat - await in finally blocks can have issues with UniTask
            while (true)
            {
                Reset();
                IsRunning = true;
                CompletedSuccessfully = false;
                FailureException = null;
                ElapsedTime = 0f;
                
                _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                var token = _sequenceCts.Token;
                
                try
                {
                    // Start max duration timeout if configured
                    if (Definition.HasMaxDuration)
                    {
                        StartMaxDurationTimeout(token).Forget();
                    }
                    
                    // Start max duration timeout if configured
                    if (Definition.HasTimeout)
                    {
                        foreach (var timeout in Definition.Timeouts)
                        {
                            StartTimeout(timeout, token).Forget();
                        }
                    }
                    
                    // Execute stages sequentially
                    await ExecuteStagesSequentially(token);
                    
                    // Wait for maintained stages to complete
                    await UniTask.WaitUntil(() => _maintainedStages <= 0, cancellationToken: token);
                    
                    CompletedSuccessfully = true;
                }
                catch (OperationCanceledException)
                {
                    // Sequence was cancelled or interrupted
                    CompletedSuccessfully = false;
                }
                catch (Exception ex)
                {
                    FailureException = ex;
                    
                    // Try sequence-level exception handler
                    var ctx = new SequenceEventContext(Data, this, CurrentStage);
                    bool suppressed = false;
                    
                    try
                    {
                        suppressed = Definition.Metadata?.OnException?.Invoke(ctx, ex) ?? false;
                    }
                    catch (Exception handlerEx)
                    {
                        Debug.LogError($"[TaskSequence] Exception in OnException handler: {handlerEx}");
                    }
                    
                    if (suppressed)
                    {
                        // Suppressed = treat as successful (enables repeat for retry patterns)
                        CompletedSuccessfully = true;
                        FailureException = null;
                    }
                    else
                    {
                        CompletedSuccessfully = false;
                        if (Definition.Metadata?.EnableErrorLogging ?? true)
                        {
                            Debug.LogError($"[TaskSequence] {Definition.Metadata?.Name ?? "Unnamed"} ({Data.Path}) failed: {ex}");
                        }
                    }
                }
                finally
                {
                    CleanupAllActiveTasks();
                    Cleanup();
                    IsRunning = false;

                    // For whole-sequence-critical sequences, fire once when the sequence exits.
                    // Stage-level critical sequences fire earlier via PopActiveStage.
                    if (!_criticalExitFired && (Definition.Metadata?.IsCritical ?? false))
                    {
                        _criticalExitFired = true;
                        OnCriticalSectionExited?.Invoke();
                    }
                }
                
                // Invoke OnComplete callback (outside finally to avoid issues)
                try
                {
                    var ctx = new SequenceEventContext(Data, this);
                    Definition.Metadata?.OnComplete?.Invoke(ctx, CompletedSuccessfully);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TaskSequence] Exception in OnComplete handler: {ex}");
                }
                
                // Check repeat condition - loop continues if repeat is enabled and successful
                bool shouldRepeat = (Definition.Metadata?.Repeat ?? false) && CompletedSuccessfully;
                if (!shouldRepeat)
                {
                    Data.InUse = false;
                    Data.Runtime = null;

                    try
                    {
                        var ctx = new SequenceEventContext(Data, this);
                        Definition.Metadata?.OnTerminate?.Invoke(ctx, CompletedSuccessfully);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TaskSequence] Exception in OnTerminate handler: {ex}");
                    }
                    
                    return;
                }
                
                // Continue loop for repeat
            }
        }
        
        private async UniTask ExecuteStagesSequentially(CancellationToken token)
        {
            while (true)
            {
                StageIndex++;
                
                // Handle jump requests
                if (_jumpToStageRequest.HasValue)
                {
                    StageIndex = _jumpToStageRequest.Value;
                    _jumpToStageRequest = null;
                }
                
                if (StageIndex >= Definition.Stages.Count)
                {
                    break;
                }
                
                token.ThrowIfCancellationRequested();
                
                var stage = Definition.Stages[StageIndex];
                CurrentStage = stage;
                
                try
                {
                    _nextStageSignal = new UniTaskCompletionSource();
                    _pendingStageError = null; // Reset error
                    
                    // Prepare all tasks in this stage
                    PrepareStage(stage);
                    
                    // Start stage execution (fire and forget, we wait on signal)
                    ExecuteStageAsync(stage, StageIndex, token).Forget();
                    
                    // Wait for stage to signal completion (or skip)
                    await _nextStageSignal.Task.AttachExternalCancellation(token);
                    
                    // Check if stage had an error that wasn't suppressed
                    if (_pendingStageError != null)
                    {
                        throw _pendingStageError;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    // Clean all tasks in this stage
                    CleanStage(stage);
                    CurrentStage = null;
                }
            }
        }
        
        private async UniTask ExecuteStageAsync(SequenceStage stage, int stageIndex, CancellationToken parentToken)
        {
            var stageCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            _stageSources[stageIndex] = stageCts;
            var stageToken = stageCts.Token;
            
            // Push stage onto active stack for condition checking + critical tracking
            PushActiveStage(stage);
            
            // Reset repeat state
            stage.ResetRepeatState();
            
            bool cancelled = false;
            Exception error = null;
            
            try
            {
                // Start stage max duration timeout if configured
                if (stage.HasMaxDuration)
                {
                    StartStageMaxDurationTimeout(stage, stageIndex, stageToken).Forget();
                }
                
                // Start timeout if configured
                if (stage.HasTimeouts)
                {
                    foreach (var timeout in stage.Timeouts)
                    {
                        StartStageTimeout(stage, timeout, stageIndex, stageToken).Forget();
                    }
                }
                
                // Execute the stage (handles repeat internally if needed)
                await ExecuteStageWithRepeat(stage, stageToken);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                error = ex;
                
                // Try stage-level exception handler if present
                if (stage.OnException != null)
                {
                    var ctx = new SequenceEventContext(Data, this, stage);
                    bool suppressed = false;
                    
                    try
                    {
                        suppressed = stage.OnException.Invoke(ctx, ex);
                    }
                    catch (Exception handlerEx)
                    {
                        Debug.LogError($"[TaskSequence] Exception in stage OnException handler: {handlerEx}");
                    }
                    
                    if (suppressed)
                    {
                        // Stage exception was suppressed - don't propagate
                        error = null;
                    }
                    else if (stage.Metadata?.EnableErrorLogging ?? true)
                    {
                        // Stage handler chose not to suppress - log at stage level
                        Debug.LogError($"[TaskSequence] Stage {stageIndex} ({stage.Metadata?.Name ?? "unnamed"}) error: {ex}");
                    }
                }
                // If no stage handler, let exception propagate silently to sequence level
            }
            finally
            {
                var ctx = new SequenceEventContext(Data, this, stage);
                stage.OnTerminate?.Invoke(ctx, !cancelled && error is null);

                // Pop stage from active stack (also decrements critical locks if applicable)
                PopActiveStage(stage);
                
                _stageSources.Remove(stageIndex);
                
                // Signal completion
                if (stageIndex == StageIndex)
                {
                    // Store error for main loop to check (safer than TrySetException)
                    if (error != null)
                    {
                        _pendingStageError = error;
                    }
                    // Always signal completion - error is checked after await
                    _nextStageSignal?.TrySetResult();
                }
                else
                {
                    _maintainedStages--;
                    // For maintained stages with errors, we need to cancel the sequence
                    if (error != null)
                    {
                        _sequenceCts?.Cancel();
                    }
                }
                
                stageCts.Dispose();
            }
        }
        
        /// <summary>
        /// Executes a stage with repeat support.
        /// </summary>
        private async UniTask ExecuteStageWithRepeat(SequenceStage stage, CancellationToken token)
        {
            // If stage has Repeat flag, we loop here
            if (stage.Repeat)
            {
                while (!token.IsCancellationRequested && !stage.RepeatBroken)
                {
                    await ExecuteStageInternal(stage, Data, token);
                    
                    if (token.IsCancellationRequested || stage.RepeatBroken)
                        break;
                    
                    // Invoke repeat iteration callback
                    try
                    {
                        var ctx = new SequenceEventContext(Data, this, stage);
                        stage.OnRepeat?.Invoke(ctx);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TaskSequence] Exception in OnRepeatIteration handler: {ex}");
                    }
                    
                    stage.RepeatCount++;
                }
            }
            else
            {
                // Normal single execution (policy may handle its own repeat like RepeatUntilSkippedPolicy)
                await ExecuteStageInternal(stage, Data, token);
            }
        }
        
        /// <summary>
        /// Executes a stage (used for both top-level and sub-stages).
        /// </summary>
        internal async UniTask ExecuteStageInternal(SequenceStage stage, SequenceDataPacket data, CancellationToken token)
        {
            // Handle branch stages
            if (stage is BranchStage branchStage)
            {
                var selectedBranch = branchStage.Evaluate(data);
                if (selectedBranch != null)
                {
                    PushActiveStage(selectedBranch);
                    PrepareStage(selectedBranch);
                    try
                    {
                        await ExecuteStageInternal(selectedBranch, data, token);
                    }
                    finally
                    {
                        CleanStage(selectedBranch);
                        PopActiveStage(selectedBranch);
                    }
                }
                return;
            }
            
            // No content = instant completion
            if (!stage.HasContent)
            {
                return;
            }
            
            // Create policy CTS FIRST so tasks use this token
            using var policyCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var policyToken = policyCts.Token;
            
            // Create execution units using the policy token
            var units = stage.CreateExecutionUnits(this, stage, data, policyToken);
            
            if (units.Length == 0)
            {
                return;
            }
            
            // Use stage policy to determine completion
            var policy = stage.Policy ?? WhenAllStagePolicy.Instance;
            
            await policy.AwaitCompletion(this, stage, units, policyCts);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ACTIVE STAGE STACK (with critical section tracking)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void PushActiveStage(SequenceStage stage)
        {
            lock (_activeStageStack)
            {
                _activeStageStack.Push(stage);
                
                if (stage.Metadata?.IsCritical ?? false)
                {
                    _criticalFlagLocks++;
                }
            }
        }
        
        private void PopActiveStage(SequenceStage stage)
        {
            lock (_activeStageStack)
            {
                if (_activeStageStack.Count > 0 && _activeStageStack.Peek() == stage)
                {
                    _activeStageStack.Pop();

                    if (stage.Metadata?.IsCritical ?? false)
                    {
                        _criticalFlagLocks = Math.Max(0, _criticalFlagLocks - 1);

                        // Fire once when the last critical stage-level lock is released
                        // and we've reached or passed the last known critical stage index.
                        if (!_criticalExitFired &&
                            _criticalFlagLocks == 0 &&
                            _lastCriticalStageIndex >= 0 &&
                            StageIndex >= _lastCriticalStageIndex)
                        {
                            _criticalExitFired = true;
                            OnCriticalSectionExited?.Invoke();
                        }
                    }
                }
            }
        }
        
        private SequenceStage[] GetActiveStages()
        {
            lock (_activeStageStack)
            {
                return _activeStageStack.ToArray();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CRITICAL SECTION ENFORCEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Checks whether a given injection is allowed during the current critical section.
        /// If not in a critical section, all injections pass this check.
        /// 
        /// Resolution order:
        ///   1. Not in critical section → allow
        ///   2. Active critical stage has CriticalAllowedInjections → use that
        ///   3. Sequence-level CriticalAllowedInjections → use that
        ///   4. Default → only InterruptSequenceInjection is allowed
        /// </summary>
        private bool IsCriticalInjectionAllowed(ISequenceInjection injection)
        {
            if (!IsCriticalSection) return true;
            
            var injectionType = injection.GetType();
            
            // Check active critical stages (innermost first) for stage-level override
            var activeStages = GetActiveStages();
            foreach (var stage in activeStages)
            {
                if (!(stage.Metadata?.IsCritical ?? false)) continue;
                
                var stageAllowed = stage.Metadata.CriticalAllowedInjections;
                if (stageAllowed != null)
                {
                    return stageAllowed.Contains(injectionType);
                }
            }
            
            // Fall back to sequence-level critical policy
            var seqAllowed = Definition.Metadata?.CriticalAllowedInjections;
            if (seqAllowed != null)
            {
                return seqAllowed.Contains(injectionType);
            }
            
            // Default: only interrupt is allowed during critical sections
            return injection is InterruptSequenceInjection;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PREPARE / CLEAN
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void PrepareStage(SequenceStage stage)
        {
            foreach (var task in stage.Tasks)
            {
                task.Prepare(Data);
                RegisterActiveTask(task);
            }
            
            foreach (var subStage in stage.SubStages)
            {
                PrepareStage(subStage);
            }
        }
        
        private void CleanStage(SequenceStage stage)
        {
            foreach (var task in stage.Tasks)
            {
                try
                {
                    task.Clean(Data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TaskSequence] Exception in task Clean: {ex}");
                }
                UnregisterActiveTask(task);
            }
            
            foreach (var subStage in stage.SubStages)
            {
                CleanStage(subStage);
            }
        }
        
        private void RegisterActiveTask(ISequenceTask task)
        {
            lock (_activeTasksLock)
            {
                _activeTasks.Add(task);
            }
        }
        
        private void UnregisterActiveTask(ISequenceTask task)
        {
            lock (_activeTasksLock)
            {
                _activeTasks.Remove(task);
            }
        }
        
        private void CleanupAllActiveTasks()
        {
            lock (_activeTasksLock)
            {
                foreach (var task in _activeTasks)
                {
                    try
                    {
                        task.Clean(Data);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TaskSequence] Exception in cleanup task Clean: {ex}");
                    }
                }
                _activeTasks.Clear();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TIMERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private async UniTaskVoid StartMaxDurationTimeout(CancellationToken token)
        {
            try
            {
                var startTime = Time.time;
                while (true)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    ElapsedTime = Time.time - startTime;
                    
                    if (ElapsedTime >= Definition.MaxDurationSeconds.Value)
                    {
                        // Invoke callback before injection
                        var ctx = new SequenceEventContext(Data, this, CurrentStage);
                        try
                        {
                            Definition.Metadata?.OnMaxDuration?.Invoke(ctx);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[TaskSequence] Exception in OnMaxDuration handler: {ex}");
                        }
                        
                        LogInjection(Definition.MaxDurationInjection, "Sequence MaxDuration");
                        Definition.MaxDurationInjection?.Apply(this);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }
        
        private async UniTaskVoid StartTimeout(SequenceTimeout timeout, CancellationToken token)
        {
            try
            {
                var startTime = Time.time;
                while (true)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                    ElapsedTime = Time.time - startTime;
                    
                    if (ElapsedTime >= timeout.Seconds)
                    {
                        // Invoke callback before injection
                        var ctx = new SequenceEventContext(Data, this, CurrentStage);
                        try
                        {
                            Definition.Metadata?.OnTimeout?.Invoke(ctx, timeout);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[TaskSequence] Exception in OnTimeout handler: {ex}");
                        }
                        
                        LogInjection(timeout.Injection, "Sequence Timeout");
                        timeout.Injection?.Apply(this);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
        }
        
        private async UniTaskVoid StartStageMaxDurationTimeout(SequenceStage stage, int stageIndex, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(stage.MaxDurationSeconds.Value), cancellationToken: token);
                
                if (_stageSources.ContainsKey(stageIndex))
                {
                    // Invoke callback before injection
                    var ctx = new SequenceEventContext(Data, this, stage);
                    try
                    {
                        stage.OnMaxDuration?.Invoke(ctx);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TaskSequence] Exception in stage OnMaxDuration handler: {ex}");
                    }
                    
                    LogInjection(stage.MaxDurationInjection, $"Stage {stageIndex} MaxDuration", stage);
                    stage.MaxDurationInjection?.Apply(this);
                }
            }
            catch (OperationCanceledException) { }
        }
        
        private async UniTaskVoid StartStageTimeout(SequenceStage stage, SequenceTimeout timeout, int stageIndex, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(timeout.Seconds), cancellationToken: token);
                
                if (_stageSources.ContainsKey(stageIndex))
                {
                    // Invoke callback before injection
                    var ctx = new SequenceEventContext(Data, this, stage);
                    try
                    {
                        stage.OnTimeout?.Invoke(ctx, timeout);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[TaskSequence] Exception in OnTimeout handler: {ex}");
                    }
                    
                    LogInjection(timeout.Injection, $"Stage {stageIndex} Timeout", stage);
                    timeout.Injection?.Apply(this);
                }
            }
            catch (OperationCanceledException) { }
        }
        
        public bool RequestStartStageTimeout(SequenceTimeout timeout)
        {
            if (CurrentStage is null) return false;

            CurrentStage.Timeouts.Add(timeout);
            
            StartStageTimeout(CurrentStage, timeout, StageIndex, _stageSources[StageIndex].Token).Forget();

            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONDITION CHECKING
        // ═══════════════════════════════════════════════════════════════════════════
        
        public void CheckConditions()
        {
            if (!IsRunning || Data == null) return;
            
            var seqData = Data;
            
            // Check global conditions (these bypass stage-local permissions)
            foreach (var condition in Definition.Conditions)
            {
                // Skip if critical section blocks this injection type
                if (!IsCriticalInjectionAllowed(condition.Injection)) continue;
                
                if (condition.CheckAndApply(seqData, this))
                {
                    LogInjection(condition.Injection, "Global Condition");
                }
            }
            
            // Check stage-local conditions (these respect stage-local permissions)
            var activeStages = GetActiveStages();
            foreach (var stage in activeStages)
            {
                if (stage.HasConditions)
                {
                    foreach (var condition in stage.Conditions)
                    {
                        // Skip if critical section blocks this injection type
                        if (!IsCriticalInjectionAllowed(condition.Injection)) continue;
                        
                        // Use CheckAndApplyWithPermission to respect stage-local AllowedInjections
                        if (condition.CheckAndApplyWithPermission(seqData, this, stage))
                        {
                            LogInjection(condition.Injection, "Stage Condition", stage);
                        }
                    }
                }
            }
        }

        public void CheckConditions(IReadOnlyList<SequenceCondition> conditions)
        {
            if (Data is null) return;
            
            var seqData = Data;
            
            foreach (var condition in conditions)
            {
                // Skip if critical section blocks this injection type
                if (!IsCriticalInjectionAllowed(condition.Injection)) continue;
                
                if (condition.CheckAndApply(seqData, this))
                {
                    LogInjection(condition.Injection, "External Condition");
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // INJECTION REQUESTS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Injects into the sequence. Checks global permissions, stage-local permissions,
        /// and critical section restrictions.
        /// </summary>
        public bool Inject(ISequenceInjection injection, bool overridePermission = false)
        {
            if (injection == null) return false;
            
            if (!overridePermission)
            {
                // Standard injection permission check
                if (!Definition.IsInjectionAllowed(injection, CurrentStage))
                    return false;
                
                // Critical section check
                if (!IsCriticalInjectionAllowed(injection))
                    return false;
            }
            
            LogInjection(injection, "Manual");
            return injection.Apply(this);
        }
        
        /// <summary>
        /// Injects a stage-local injection. Only checks stage-local permissions
        /// and critical section restrictions.
        /// </summary>
        public bool InjectStageLocal(ISequenceInjection injection, SequenceStage stage = null)
        {
            if (injection == null) return false;
            
            var targetStage = stage ?? CurrentStage;
            if (targetStage == null) return false;
            
            // Critical section check
            if (!IsCriticalInjectionAllowed(injection))
                return false;
            
            // Check stage-local permissions
            var allowed = targetStage.IsInjectionAllowedLocal(injection);
            if (allowed.HasValue && !allowed.Value)
            {
                return false;
            }
            
            // Fall back to global if stage doesn't specify
            if (!allowed.HasValue && !Definition.IsInjectionAllowed(injection))
            {
                return false;
            }
            
            LogInjection(injection, "Stage-Local Manual", targetStage);
            return injection.Apply(this);
        }
        
        private void LogInjection(ISequenceInjection injection, string source, SequenceStage stage = null)
        {
            bool shouldLog = stage?.Metadata?.EnableInjectionLogging 
                ?? Definition.Metadata?.EnableInjectionLogging 
                ?? false;
            
            if (shouldLog && injection != null)
            {
                var stageName = stage?.Metadata?.Name ?? CurrentStage?.Metadata?.Name ?? "unknown";
                var criticalTag = IsCriticalSection ? " [CRITICAL]" : "";
                Debug.Log($"[TaskSequence] Injection applied: {injection.GetType().Name} (Source: {source}, Stage: {stageName}{criticalTag})");
            }
        }
        
        internal void RequestInterrupt() => _sequenceCts?.Cancel();
        
        internal bool RequestSkipCurrentStage()
        {
            if (StageIndex < 0 || !_stageSources.TryGetValue(StageIndex, out var cts))
                return false;
            cts?.Cancel();
            return true;
        }
        
        internal bool RequestSkipAndMaintain()
        {
            _maintainedStages++;
            _nextStageSignal?.TrySetResult();
            return true;
        }
        
        internal bool RequestStopMaintainedLast()
        {
            if (_stageSources.Count == 0) return false;
            var firstKey = _stageSources.Keys.OrderBy(k => k).First();
            _stageSources[firstKey]?.Cancel();
            return true;
        }
        
        internal bool RequestStopMaintainedAt(int index)
        {
            if (_stageSources.Count == 0 
                || index < 0 || index >= _stageSources.Count
                || !_stageSources.ContainsKey(index)) return false;
            _stageSources[index]?.Cancel();
            return true;
        }
        
        internal bool RequestStopMaintainedAll()
        {
            if (_stageSources.Count == 0) return false;
            foreach (var cts in _stageSources.Values) cts?.Cancel();
            return true;
        }
        
        internal bool RequestJumpToStage(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= Definition.Stages.Count)
                return false;
            _jumpToStageRequest = targetIndex;
            RequestSkipCurrentStage();
            return true;
        }
        
        /// <summary>
        /// Breaks the repeat loop of the current stage.
        /// </summary>
        internal bool RequestBreakCurrentStageRepeat()
        {
            if (CurrentStage == null) return false;
            CurrentStage.RepeatBroken = true;
            return true;
        }
        
        /// <summary>
        /// Breaks the repeat loop of a specific stage.
        /// </summary>
        internal bool RequestBreakStageRepeat(SequenceStage stage)
        {
            if (stage == null) return false;
            stage.RepeatBroken = true;
            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CLEANUP / RESET
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void Reset()
        {
            StageIndex = -1;
            CurrentStage = null;
            _maintainedStages = 0;
            _jumpToStageRequest = null;
            _pendingStageError = null;
            _criticalFlagLocks = 0;
            _criticalExitFired = false;
            _stageSources.Clear();
            _activeTasks.Clear();
            _activeStageStack.Clear();
            Definition.ResetConditions();

            // Precompute the index of the last stage-level critical stage
            _lastCriticalStageIndex = -1;
            for (int i = Definition.Stages.Count - 1; i >= 0; i--)
            {
                if (Definition.Stages[i].Metadata?.IsCritical ?? false)
                {
                    _lastCriticalStageIndex = i;
                    break;
                }
            }
        }
        
        private void Cleanup()
        {
            if (_sequenceCts is null) return;

            var sources = _stageSources.Values.ToArray();
            foreach (var cts in sources)
            {
                if (cts is null) continue;
                try { cts?.Cancel(); cts?.Dispose(); } catch { }
            }
            _stageSources.Clear();
            _activeStageStack.Clear();
            _criticalFlagLocks = 0;
            
            try { _sequenceCts?.Dispose(); } catch { }
            _sequenceCts = null;
        }
    }
}