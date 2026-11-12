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

        /// <summary>True while this run is paused. See <see cref="Pause"/>.</summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// True if the sequence is currently inside a critical section.
        /// When true, injections are restricted to only those allowed by the critical policy.
        /// </summary>
        public bool IsCriticalSection => (Definition.Metadata?.IsCritical ?? false) || _criticalFlagLocks > 0;
        
        // The run each stage's token belongs to. Code inside a task often has only that token, as the library's
        // helpers do, and it's enough to ask whether the run is paused
        private static readonly Dictionary<CancellationToken, TaskSequenceRuntime> RunsByToken = new(TokenComparer.Instance);

        // Compares tokens as tokens. The default comparer boxed one on every lookup under Unity's Mono, where a token
        // isn't IEquatable, so a task waiting in a delay allocated 24 bytes a frame asking whether it was paused (TS-11)
        private sealed class TokenComparer : IEqualityComparer<CancellationToken>
        {
            public static readonly TokenComparer Instance = new();

            public bool Equals(CancellationToken x, CancellationToken y) => x.Equals(y);

            public int GetHashCode(CancellationToken token) => token.GetHashCode();
        }

        // A run cut off when play mode ended could stay here into the next session, since statics outlive play mode
        // when domain reload is off (SP-22)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRuns()
        {
            lock (RunsByToken) RunsByToken.Clear();
        }

        // Cancellation management
        private CancellationTokenSource _sequenceCts;
        private readonly Dictionary<int, CancellationTokenSource> _stageSources = new();
        private UniTaskCompletionSource _nextStageSignal;

        // The stages left running behind the current one. Each cleans its tasks as it ends, since moving on
        // cleaned them while they still ran (TS-J)
        private readonly HashSet<int> _maintainedStages = new();
        
        // Jump request
        private int? _jumpToStageRequest;
        
        // What this run has done to stages and conditions. Those belong to the definition, which every run
        // shares, so recording it on them let one run break another's repeat or re-arm its conditions (TS-I)
        private readonly HashSet<SequenceCondition> _firedConditions = new();
        private readonly HashSet<SequenceStage> _brokenRepeats = new();
        private readonly Dictionary<SequenceStage, int> _repeatCounts = new();

        // Where on its packet's path each running stage's iterations start, so a repeat takes off what the last one
        // added; and the path where this run failed, kept for its log, as the failing stage has taken its part off by
        // then. Each part stayed, so a repeat grew the path for as long as it ran (TS-5)
        private readonly Dictionary<SequenceStage, int> _iterationPathDepths = new();
        private string _failurePath;

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
        
        // Active stages for condition checking and the critical section, oldest first. A maintained stage ends
        // under the stages after it, so this can't be a stack: one that ended off the top stayed on it (TS-J).
        // Each is kept with its id in _stageSources (TS-N)
        private readonly List<(SequenceStage Stage, int Id)> _activeStages = new();

        // Sub-stages are kept in _stageSources with the top-level stages, counting down from -2 so that the two
        // never meet, and -1 stays StageIndex's "not started"
        private int _nextSubStageId = -2;

        // The stage whose own timer or condition is applying an injection. Injections act on the current stage,
        // which a maintained stage or a sub-stage isn't, so theirs acted on the wrong one (TS-K)
        private (SequenceStage Stage, int Id)? _injectionSource;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public TaskSequenceRuntime(TaskSequenceDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PAUSE
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Holds this run where it is: no stage starts or ends, and timeouts, max durations and delays stop
        /// counting. A task's own code can't be stopped from out here, so it holds if it keeps its time through
        /// <see cref="SequenceTaskLibrary"/>. If it doesn't, it finishes when it finishes, and its stage waits.
        /// </summary>
        public void Pause()
        {
            IsPaused = true;
        }

        /// <summary>Carries on from where <see cref="Pause"/> stopped.</summary>
        public void Resume()
        {
            IsPaused = false;
        }

        /// <summary>True if the token is one a stage gave its tasks, and that stage's run is paused.</summary>
        /// <summary>The packet of the run a token belongs to, or null, whose owner's clock its waits run by (FEAT-7).</summary>
        public static SequenceDataPacket DataOfRun(CancellationToken token)
        {
            lock (RunsByToken)
            {
                return RunsByToken.TryGetValue(token, out var run) ? run.Data : null;
            }
        }

        public static bool IsPausedRun(CancellationToken token)
        {
            lock (RunsByToken)
            {
                return RunsByToken.TryGetValue(token, out var run) && run.IsPaused;
            }
        }

        /// <summary>Waits for the given time of this run, which a paused frame isn't.</summary>
        public async UniTask Delay(float seconds, CancellationToken token)
        {
            float elapsed = 0f;
            do
            {
                await ForgeTick.Next(token);
                if (!IsPaused) elapsed += ForgeTime.DeltaTimeFor(Data);
            } while (elapsed < seconds);
        }

        // Not awaited unless it has to be, so that a run that isn't paused doesn't lose a frame here
        private UniTask HoldWhilePaused(CancellationToken token)
        {
            return IsPaused ? ForgeTick.WaitWhile(() => IsPaused, token) : UniTask.CompletedTask;
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
            
            // A run nested on the same packet, as a task's own sequence can be, gives it back as it found it. It
            // used to clear it, which left the outer run with no runtime and its packet marked unused (TS-F)
            var outerRuntime = Data.Runtime;
            Data.Runtime = this;

            // The run's part of the path comes off as it ends, and each time round starts from it (TS-5)
            int outerPathDepth = Data.PathDepth;
            Data.AppendPath(Definition?.PathPart ?? "Seq[]");
            int runPathDepth = Data.PathDepth;

            // Use loop for repeat - await in finally blocks can have issues with UniTask
            while (true)
            {
                Reset();
                Data.TrimPath(runPathDepth);
                _failurePath = null;
                IsRunning = true;
                ForgeCounters.SequencesRunning.Value++;
                CompletedSuccessfully = false;
                FailureException = null;
                ElapsedTime = 0f;
                
                _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
                var token = _sequenceCts.Token;
                int runStartFrame = ForgeTime.FrameCount;

                // The run keeps its own time. The sequence's timers kept it, so a sequence without one read 0
                // throughout, and one whose timer had fired stopped where it fired (TS-W)
                RunClock(token).Forget();
                
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

                    // Wait for maintained stages to complete, when there are some. The wait took a frame on every
                    // run, so a sequence ended a frame after its last stage did (TS-Z)
                    if (_maintainedStages.Count > 0)
                    {
                        await ForgeTick.WaitUntil(() => _maintainedStages.Count == 0, token);
                    }

                    // A run that repeats takes a frame if it took none, or an instant sequence repeats for ever
                    // inside a frame, as a stage would without TS-M. The wait above took that frame. Here, inside the
                    // run, an interrupt in that frame still ends it, as it did (TS-Z)
                    if ((Definition.Metadata?.Repeat ?? false) && ForgeTime.FrameCount == runStartFrame)
                    {
                        await ForgeTick.Next(token);
                    }

                    CompletedSuccessfully = true;
                }
                catch (OperationCanceledException)
                {
                    // Sequence was cancelled or interrupted
                    CompletedSuccessfully = false;
                    await StopMaintainedStages();
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
                            var path = _failurePath ?? Data.Path;
                            Debug.LogError($"[TaskSequence] {Definition.Metadata?.Name ?? "Unnamed"} {(!string.IsNullOrEmpty(path) ? $"({path})" : "")} failed: {FailureException}");
                        }
                    }

                    await StopMaintainedStages();
                }
                finally
                {
                    CleanupAllActiveTasks();
                    Cleanup();
                    IsRunning = false;
                    ForgeCounters.SequencesRunning.Value--;

                    // For whole-sequence-critical sequences, fire once when the sequence exits.
                    // Stage-level critical sequences fire earlier via PopActiveStage.
                    if (!_criticalExitFired && (Definition.Metadata?.IsCritical ?? false))
                    {
                        _criticalExitFired = true;
                        NotifyCriticalSectionExited();
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
                    Data.InUse = outerRuntime != null;
                    Data.Runtime = outerRuntime;
                    Data.TrimPath(outerPathDepth);

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
        
        /// <summary>
        /// Stops what's still maintained when a run ends early, and waits for it to stop. Each maintained stage
        /// cleans its tasks as it ends, which mustn't happen under them, and the run's cleanup cleaned them at once
        /// (TS-J). A normal end has none left.
        /// </summary>
        private async UniTask StopMaintainedStages()
        {
            if (_maintainedStages.Count == 0) return;

            _sequenceCts?.Cancel();
            await ForgeTick.WaitUntil(() => _maintainedStages.Count == 0);
        }

        private async UniTask ExecuteStagesSequentially(CancellationToken token)
        {
            while (true)
            {
                await HoldWhilePaused(token);

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

                // Past the last critical stage with nothing critical running, the critical section ends as this stage
                // starts. A critical stage that a branch skipped held it to the end of the run, as the end came only as a
                // critical stage ended (TS-7)
                ExitCriticalSectionIfPassed();
                
                try
                {
                    _nextStageSignal = new UniTaskCompletionSource();
                    _pendingStageError = null; // Reset error
                    
                    // Prepare all tasks in this stage
                    PrepareStage(stage);
                    
                    // Start stage execution (fire and forget, we wait on signal)
                    ExecuteStageAsync(stage, StageIndex, token).Forget();
                    
                    // Wait for stage to signal completion (or skip). A cancelled stage signals too, once its
                    // tasks have stopped, so this doesn't watch the token: leaving at the moment it cancelled
                    // ran CleanStage below under tasks that were still unwinding (TS-AA)
                    await _nextStageSignal.Task;
                    
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
                    // Clean all tasks in this stage, unless it's maintained: its tasks are still running, and it
                    // cleans them itself when it ends (TS-J)
                    if (!_maintainedStages.Contains(StageIndex)) CleanStage(stage);
                    CurrentStage = null;
                }
            }
        }
        
        /// <summary>
        /// Runs a top-level stage, and moves the sequence on when it ends, unless it's been maintained.
        /// </summary>
        private async UniTask ExecuteStageAsync(SequenceStage stage, int stageIndex, CancellationToken parentToken)
        {
            var error = await RunStage(stage, stageIndex, parentToken);

            // Signal completion
            if (!_maintainedStages.Remove(stageIndex))
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
                // Nothing waits on a maintained stage, so it cleans its own tasks as it ends (TS-J)
                CleanStage(stage);

                // For maintained stages with errors, we need to cancel the sequence
                if (error != null)
                {
                    _sequenceCts?.Cancel();
                }
            }
        }

        /// <summary>
        /// Runs a sub-stage, or the stage a branch chose, as one unit of the stage it's in. Its error is thrown on
        /// for that stage's policy, and so is a cancellation from that stage, while its own skip or timeout just
        /// ends it.
        /// </summary>
        internal async UniTask ExecuteSubStage(SequenceStage stage, CancellationToken parentToken)
        {
            var error = await RunStage(stage, _nextSubStageId--, parentToken);

            if (error != null) throw error;
            parentToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// What every stage does, wherever it is: its callbacks, timers, repeat and error handler, and its place on
        /// the active list for conditions and the critical section. A sub-stage ran its tasks and nothing else, so
        /// all of that was ignored for one (TS-N). Returns the error that ended the stage, if its handler didn't
        /// take it.
        /// </summary>
        private async UniTask<Exception> RunStage(SequenceStage stage, int stageId, CancellationToken parentToken)
        {
            var stageCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            _stageSources[stageId] = stageCts;
            var stageToken = stageCts.Token;

            // Push stage onto active stack for condition checking + critical tracking
            PushActiveStage(stage, stageId);

            // Reset repeat state
            _brokenRepeats.Remove(stage);
            _repeatCounts.Remove(stage);

            bool cancelled = false;
            Exception error = null;

            // The stage's part of the path, and whatever its init adds, come off as it ends, and each iteration of a
            // repeat starts after them (TS-5)
            int pathDepth = Data.PathDepth;
            Data.AppendPath(stage.PathPart);

            try
            {
                stage.OnInit?.Invoke(Data);
                _iterationPathDepths[stage] = Data.PathDepth;

                // Start stage max duration timeout if configured
                if (stage.HasMaxDuration)
                {
                    StartStageMaxDurationTimeout(stage, stageId, stageToken).Forget();
                }

                // Start timeout if configured
                if (stage.HasTimeouts)
                {
                    foreach (var timeout in stage.Timeouts)
                    {
                        StartStageTimeout(stage, timeout, stageId, stageToken).Forget();
                    }
                }

                // Execute the stage (handles repeat internally if needed)
                await ExecuteStageWithRepeat(stage, stageCts);
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
                        Debug.LogError($"[TaskSequence] Stage {stageId} ({stage.Metadata?.Name ?? "unnamed"}) error: {ex}");
                    }
                }
                // If no stage handler, let exception propagate silently to sequence level

                // Where the run failed, for its log: the innermost stage to fail keeps it first (TS-5)
                if (error != null) _failurePath ??= Data.Path;
            }
            finally
            {
                // The rest of this block has to happen, so a throwing handler can't be let past here (TS-E)
                try
                {
                    var ctx = new SequenceEventContext(Data, this, stage);

                    // Cut short as what holds it was interrupted or cancelled, it didn't succeed, though its policy took
                    // its cancelled tasks as ended: WhenAll did, and said it had (TS-AE)
                    stage.OnTerminate?.Invoke(ctx, !cancelled && error is null && !parentToken.IsCancellationRequested);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TaskSequence] Exception in stage OnTerminate handler: {ex}");
                }

                // Pop stage from active stack (also decrements critical locks if applicable)
                PopActiveStage(stage, stageId);

                // Cancelled as well as disposed: a disposed source never cancels, so the stage's timers outlived it
                // and fired into whatever ran next (TS-AD). The run's cleanup may have done both already
                if (_stageSources.Remove(stageId))
                {
                    stageCts.Cancel();
                    stageCts.Dispose();
                }

                _iterationPathDepths.Remove(stage);
                Data.TrimPath(pathDepth);
            }

            return error;
        }
        
        /// <summary>
        /// Executes a stage with repeat support.
        /// </summary>
        private async UniTask ExecuteStageWithRepeat(SequenceStage stage, CancellationTokenSource stageSource)
        {
            var token = stageSource.Token;

            // If stage has Repeat flag, we loop here
            if (stage.Repeat)
            {
                while (!token.IsCancellationRequested && !IsRepeatBroken(stage))
                {
                    int startFrame = ForgeTime.FrameCount;

                    await ExecuteStageInternal(stage, Data, token, stageSource);
                    
                    if (token.IsCancellationRequested || IsRepeatBroken(stage))
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
                    
                    _repeatCounts[stage] = GetRepeatCount(stage) + 1;

                    if (token.IsCancellationRequested || IsRepeatBroken(stage))
                        break;

                    await RestartStage(stage, startFrame, token);
                }
            }
            else
            {
                // Normal single execution (policy may handle its own repeat like RepeatUntilSkippedPolicy)
                await ExecuteStageInternal(stage, Data, token, stageSource);
            }
        }
        
        /// <summary>
        /// Readies a repeating stage for its next iteration. Each iteration is a run of its tasks, so they're
        /// cleaned and prepared again: a task that resets in Prepare otherwise finishes at once from then on.
        /// An iteration that took no frame takes one here, or a stage whose tasks finish without yielding
        /// repeats for ever inside a frame (TS-M).
        /// </summary>
        internal async UniTask RestartStage(SequenceStage stage, int startFrame, CancellationToken token)
        {
            // What the last iteration added to the path comes off, so each one finds it as the first did (TS-5)
            if (_iterationPathDepths.TryGetValue(stage, out int pathDepth)) Data.TrimPath(pathDepth);

            if (ForgeTime.FrameCount == startFrame) await ForgeTick.Next(token);

            CleanStage(stage);
            PrepareStage(stage);
        }

        /// <summary>
        /// Executes a stage (used for both top-level and sub-stages). <paramref name="stageSource"/> is the stage's own
        /// token source, which a policy that only waits for its tasks is handed.
        /// </summary>
        internal async UniTask ExecuteStageInternal(SequenceStage stage, SequenceDataPacket data, CancellationToken token,
            CancellationTokenSource stageSource = null)
        {
            // Handle branch stages. The stage chosen runs as a sub-stage does, its settings with it (TS-N)
            if (stage is BranchStage branchStage)
            {
                var selectedBranch = branchStage.Evaluate(data);
                if (selectedBranch != null)
                {
                    PrepareStage(selectedBranch);
                    try
                    {
                        await ExecuteSubStage(selectedBranch, token);
                    }
                    finally
                    {
                        CleanStage(selectedBranch);
                    }
                }
                return;
            }
            
            // No content = instant completion
            if (!stage.HasContent)
            {
                return;
            }
            
            // Use stage policy to determine completion
            var policy = stage.Policy ?? WhenAllStagePolicy.Instance;

            // The source the policy is handed, FIRST, so tasks use its token. A policy that stops the tasks still
            // running, as WhenAny and WhenN do, gets one of its own to cancel, so the stage isn't cancelled with them;
            // so does one the runtime doesn't know. One that only waits is handed the stage's own. Every stage made a
            // second source for its policy, and with its own, a three-stage ability made about 14 (TS-10)
            CancellationTokenSource ownSource = null;
            var policySource = stageSource;
            if (policySource == null || !OnlyWaits(policy))
            {
                ownSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                policySource = ownSource;
            }
            var policyToken = policySource.Token;

            lock (RunsByToken)
            {
                RunsByToken[policyToken] = this;
            }

            try
            {
                // Its start under a marker of its own in the Profiler: its tasks, up to the first that waits. A marker
                // ends where it began, so it holds only the start (FEAT-18)
                UniTask completion;
                using (ForgeProfiler.Stage.Auto())
                {
                    // Create execution units using the policy token
                    var units = stage.CreateExecutionUnits(this, stage, data, policyToken);

                    if (units.Length == 0)
                    {
                        return;
                    }

                    ForgeCounters.StagesStarted.Value++;
                    completion = policy.AwaitCompletion(this, stage, units, policySource);
                }

                await completion;

                // A task that doesn't hold can finish during a pause, but its stage ends with the pause (TS-Y)
                await HoldWhilePaused(token);
            }
            finally
            {
                lock (RunsByToken)
                {
                    RunsByToken.Remove(policyToken);
                }

                ownSource?.Dispose();
            }
        }

        // The built-in policies that only wait for their tasks and never cancel what they're handed. By exact type: a
        // policy that extends one could cancel it
        private static bool OnlyWaits(ISequenceStagePolicy policy)
        {
            var type = policy.GetType();
            return type == typeof(WhenAllStagePolicy) || type == typeof(RepeatUntilSkippedPolicy);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ACTIVE STAGE STACK (with critical section tracking)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void PushActiveStage(SequenceStage stage, int stageId)
        {
            lock (_activeStages)
            {
                _activeStages.Add((stage, stageId));
                
                if (stage.Metadata?.IsCritical ?? false)
                {
                    _criticalFlagLocks++;
                }
            }
        }
        
        private void PopActiveStage(SequenceStage stage, int stageId)
        {
            lock (_activeStages)
            {
                int index = _activeStages.IndexOf((stage, stageId));
                if (index >= 0)
                {
                    _activeStages.RemoveAt(index);

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
                            NotifyCriticalSectionExited();
                        }
                    }
                }
            }
        }
        
        // Fires the critical section's end once, as the run starts a stage past its last critical one with nothing
        // critical running: there, a critical stage that a branch skipped is behind it (TS-7). At the skip itself the run
        // still stands on the skipped stage, so what starts as the claim goes would find it there
        private void ExitCriticalSectionIfPassed()
        {
            lock (_activeStages)
            {
                if (_criticalExitFired || _criticalFlagLocks != 0 || _lastCriticalStageIndex < 0 ||
                    StageIndex <= _lastCriticalStageIndex) return;
                _criticalExitFired = true;
                NotifyCriticalSectionExited();
            }
        }

        /// <summary>
        /// Both places this fires are in cleanup that has to finish, and a listener's throw is its own (TS-E).
        /// </summary>
        private void NotifyCriticalSectionExited()
        {
            try
            {
                OnCriticalSectionExited?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TaskSequence] Exception in OnCriticalSectionExited handler: {ex}");
            }
        }

        // Innermost first, as the stack gave them
        private (SequenceStage Stage, int Id)[] GetActiveStages()
        {
            lock (_activeStages)
            {
                var stages = _activeStages.ToArray();
                Array.Reverse(stages);
                return stages;
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
            foreach (var (stage, _) in activeStages)
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
                RegisterActiveTask(task);
                task.Prepare(Data);
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
                // A stage can stop part way through preparing, and what it didn't get to has nothing to clean
                if (!UnregisterActiveTask(task)) continue;

                try
                {
                    task.Clean(Data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TaskSequence] Exception in task Clean: {ex}");
                }
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
        
        private bool UnregisterActiveTask(ISequenceTask task)
        {
            lock (_activeTasksLock)
            {
                return _activeTasks.Remove(task);
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
        
        // Counts the run's time, which a paused frame isn't
        private async UniTaskVoid RunClock(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await ForgeTick.Next(token);
                    if (!IsPaused) ElapsedTime += ForgeTime.DeltaTimeFor(Data);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async UniTaskVoid StartMaxDurationTimeout(CancellationToken token)
        {
            try
            {
                float elapsed = 0f;
                while (true)
                {
                    await ForgeTick.Next(token);
                    if (IsPaused) continue;

                    elapsed += ForgeTime.DeltaTimeFor(Data);
                    if (elapsed >= Definition.MaxDurationSeconds.Value)
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
                float elapsed = 0f;
                while (true)
                {
                    await ForgeTick.Next(token);
                    if (IsPaused) continue;

                    elapsed += ForgeTime.DeltaTimeFor(Data);
                    if (elapsed >= timeout.Seconds)
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
                await Delay(stage.MaxDurationSeconds.Value, token);
                
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
                    ApplyFromStage(stage.MaxDurationInjection, stage, stageIndex);
                }
            }
            catch (OperationCanceledException) { }
        }
        
        private async UniTaskVoid StartStageTimeout(SequenceStage stage, SequenceTimeout timeout, int stageIndex, CancellationToken token)
        {
            try
            {
                await Delay(timeout.Seconds, token);
                
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
                    ApplyFromStage(timeout.Injection, stage, stageIndex);
                }
            }
            catch (OperationCanceledException) { }
        }
        
        public bool RequestStartStageTimeout(SequenceTimeout timeout)
        {
            if (CurrentStage is null) return false;

            StartStageTimeout(CurrentStage, timeout, StageIndex, _stageSources[StageIndex].Token).Forget();

            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PER-RUN STATE
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>True if this run has broken the stage's repeat loop.</summary>
        public bool IsRepeatBroken(SequenceStage stage) => _brokenRepeats.Contains(stage);

        /// <summary>How many times the stage has repeated in this run.</summary>
        public int GetRepeatCount(SequenceStage stage) => stage is not null && _repeatCounts.TryGetValue(stage, out int count) ? count : 0;

        /// <summary>True if the condition has fired in this run.</summary>
        internal bool HasFired(SequenceCondition condition) => _firedConditions.Contains(condition);

        internal void MarkFired(SequenceCondition condition) => _firedConditions.Add(condition);

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
            foreach (var (stage, stageId) in activeStages)
            {
                if (stage.HasConditions)
                {
                    foreach (var condition in stage.Conditions)
                    {
                        // Skip if critical section blocks this injection type
                        if (!IsCriticalInjectionAllowed(condition.Injection)) continue;

                        // Use CheckAndApplyWithPermission to respect stage-local AllowedInjections
                        if (CheckFromStage(condition, stage, stageId))
                        {
                            LogInjection(condition.Injection, "Stage Condition", stage);
                        }
                    }
                }
            }
        }

        // Checks a stage's own condition, so that what it applies acts on that stage
        private bool CheckFromStage(SequenceCondition condition, SequenceStage stage, int stageId)
        {
            _injectionSource = (stage, stageId);
            try
            {
                return condition.CheckAndApplyWithPermission(Data, this, stage);
            }
            finally
            {
                _injectionSource = null;
            }
        }

        // Applies a stage's own timer's injection, so that it acts on that stage
        private bool ApplyFromStage(ISequenceInjection injection, SequenceStage stage, int stageId)
        {
            _injectionSource = (stage, stageId);
            try
            {
                return injection?.Apply(this) ?? false;
            }
            finally
            {
                _injectionSource = null;
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
            // The stage applying it, if a stage is, and otherwise the current one (TS-K)
            return CancelStage(_injectionSource?.Id ?? StageIndex);
        }
        
        internal bool RequestSkipAndMaintain()
        {
            // Only a running stage can be maintained. Past the last one, this counted a stage that nothing would
            // ever end, and the run waited on it for ever (TS-J). A maintained stage's own timer or condition finds
            // it maintained already (TS-K)
            if (CurrentStage == null || _injectionSource is { } source && _maintainedStages.Contains(source.Id)) return false;

            _maintainedStages.Add(StageIndex);
            _nextStageSignal?.TrySetResult();
            return true;
        }
        
        // These choose among the maintained stages alone. They chose among every running stage, the current one
        // too, and checked an index against how many stages ran rather than which (TS-L)

        internal bool RequestStopMaintainedLast()
        {
            if (_maintainedStages.Count == 0) return false;
            return CancelStage(_maintainedStages.Min());
        }

        internal bool RequestStopMaintainedAt(int index)
        {
            return _maintainedStages.Contains(index) && CancelStage(index);
        }

        internal bool RequestStopMaintainedAll()
        {
            if (_maintainedStages.Count == 0) return false;

            // A copy, since a stage that stops at once leaves the set while this goes through it
            foreach (int index in _maintainedStages.ToArray()) CancelStage(index);
            return true;
        }

        private bool CancelStage(int stageId)
        {
            if (!_stageSources.TryGetValue(stageId, out var cts)) return false;

            // Cancelling can start the next stage from right here, and what that injects is its own
            _injectionSource = null;
            cts?.Cancel();
            return true;
        }
        
        internal bool RequestJumpToStage(int targetIndex)
        {
            // A maintained stage is still running, and jumping to it ran its tasks again while they ran (TS-J)
            if (targetIndex < 0 || targetIndex >= Definition.Stages.Count || _maintainedStages.Contains(targetIndex))
                return false;
            _jumpToStageRequest = targetIndex;

            // From the current stage, whichever stage asked
            CancelStage(StageIndex);
            return true;
        }
        
        /// <summary>
        /// Breaks the repeat loop of the current stage.
        /// </summary>
        internal bool RequestBreakCurrentStageRepeat()
        {
            // The stage applying it, if a stage is, and otherwise the innermost one that repeats: with a maintained
            // stage or a sub-stage repeating, that needn't be the current one (TS-K)
            var stage = _injectionSource?.Stage ?? InnermostRepeatingStage() ?? CurrentStage;
            if (stage == null) return false;
            _brokenRepeats.Add(stage);
            return true;
        }

        private SequenceStage InnermostRepeatingStage()
        {
            foreach (var (stage, _) in GetActiveStages())
            {
                if (stage.ShouldRepeat) return stage;
            }

            return null;
        }
        
        /// <summary>
        /// Breaks the repeat loop of a specific stage.
        /// </summary>
        internal bool RequestBreakStageRepeat(SequenceStage stage)
        {
            if (stage == null) return false;
            _brokenRepeats.Add(stage);
            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CLEANUP / RESET
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void Reset()
        {
            StageIndex = -1;
            CurrentStage = null;
            _maintainedStages.Clear();
            _nextSubStageId = -2;
            _jumpToStageRequest = null;
            _pendingStageError = null;
            _criticalFlagLocks = 0;
            _criticalExitFired = false;
            _stageSources.Clear();
            _activeTasks.Clear();
            _activeStages.Clear();
            _firedConditions.Clear();
            _brokenRepeats.Clear();
            _repeatCounts.Clear();

            // Precompute the index of the last stage-level critical stage. A critical sub-stage counts for the stage
            // it's in, now that it holds the critical section (TS-N)
            _lastCriticalStageIndex = -1;
            for (int i = Definition.Stages.Count - 1; i >= 0; i--)
            {
                if (HoldsCriticalStage(Definition.Stages[i]))
                {
                    _lastCriticalStageIndex = i;
                    break;
                }
            }
        }
        
        private static bool HoldsCriticalStage(SequenceStage stage)
        {
            if (stage.Metadata?.IsCritical ?? false) return true;

            foreach (var subStage in stage.SubStages)
            {
                if (HoldsCriticalStage(subStage)) return true;
            }

            if (stage is BranchStage branch)
            {
                foreach (var branchCase in branch.Branches)
                {
                    if (HoldsCriticalStage(branchCase.Stage)) return true;
                }
                if (branch.DefaultBranch != null && HoldsCriticalStage(branch.DefaultBranch)) return true;
            }

            return false;
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
            _activeStages.Clear();
            _criticalFlagLocks = 0;
            
            // Cancelled as well as disposed, or the run's own timers go on and fire into its next round (TS-AD)
            try { _sequenceCts?.Cancel(); _sequenceCts?.Dispose(); } catch { }
            _sequenceCts = null;
        }
    }
}