using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════
    // DELEGATE SYNC TASK (implements unified ISequenceTask, sync-first)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wraps a delegate as a sync-first sequence task implementing the unified ISequenceTask.
    /// Step is the primary execution path; Execute bridges by looping Step with per-frame yields.
    /// </summary>
    public class DelegateSyncTask : ISequenceTask
    {
        private readonly Func<SequenceDataPacket, float, bool> _step;
        private readonly Action<SequenceDataPacket> _prepare;
        private readonly Action<SequenceDataPacket> _clean;

        public DelegateSyncTask(
            Func<SequenceDataPacket, float, bool> step,
            Action<SequenceDataPacket> prepare = null,
            Action<SequenceDataPacket> clean = null)
        {
            _step = step ?? throw new ArgumentNullException(nameof(step));
            _prepare = prepare;
            _clean = clean;
        }

        /// <summary>
        /// Creates a task from a step function that does not need the data packet.
        /// </summary>
        public DelegateSyncTask(Func<float, bool> step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            _step = (_, dt) => step(dt);
        }

        /// <summary>
        /// Creates a one-shot task from a synchronous action (completes immediately).
        /// </summary>
        public DelegateSyncTask(Action<SequenceDataPacket> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _step = (data, _) => { action(data); return true; };
        }

        public void Prepare(SequenceDataPacket data) => _prepare?.Invoke(data);

        /// <summary>
        /// Bridge: loops Step with per-frame yields for async execution.
        /// </summary>
        public async UniTask Execute(SequenceDataPacket data, System.Threading.CancellationToken token)
        {
            while (!Step(data, UnityEngine.Time.deltaTime))
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        public bool Step(SequenceDataPacket data, float deltaTime) => _step(data, deltaTime);
        public void Clean(SequenceDataPacket data) => _clean?.Invoke(data);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SYNC SEQUENCE RUNNER
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drives a list of synchronous sequence stages frame-by-frame.
    /// Uses the unified ISequenceTask interface (calls Step on each task).
    /// Designed to be called from a process's WhenUpdate.
    ///
    /// Usage:
    ///   var runner = TaskSequenceBuilder.Create("MyRunner")
    ///       .Stage(s => s.Task(...))
    ///       .BuildSyncRunner();
    ///
    ///   // In WhenUpdate:
    ///   if (runner.Step(data, Time.deltaTime))
    ///       relay.Terminate();
    /// </summary>
    public class SyncedTaskSequence
    {
        public enum ERunnerState
        {
            Ready,
            Running,
            Completed,
            Interrupted
        }

        private readonly List<SyncSequenceStage> _stages;
        private readonly string _name;
        private readonly bool _repeat;
        private readonly Action<SyncedTaskSequence> _onComplete;

        private int _stageIndex;
        private bool _stagePrepared;
        private float _elapsedTime;
        private ERunnerState _state;
        private int _lastCriticalStageIndex;
        private bool _criticalExitFired;

        /// <summary>Current stage index.</summary>
        public int StageIndex => _stageIndex;

        /// <summary>Total number of stages.</summary>
        public int StageCount => _stages.Count;

        /// <summary>Elapsed time since the runner started.</summary>
        public float ElapsedTime => _elapsedTime;

        /// <summary>Current runner state.</summary>
        public ERunnerState State => _state;

        /// <summary>True if the runner has completed all stages.</summary>
        public bool IsComplete => _state == ERunnerState.Completed;

        /// <summary>True if the runner is actively stepping.</summary>
        public bool IsRunning => _state == ERunnerState.Running;

        /// <summary>Runner name for debugging.</summary>
        public string Name => _name;

        /// <summary>
        /// Fired once when the last critical stage in this sync runner completes.
        /// </summary>
        public Action OnCriticalSectionExited { get; set; }

        /// <summary>
        /// Fired as the sequence finishes or is interrupted, with whether it finished. A repeating sequence fires it
        /// each time round, as an async one does.
        /// </summary>
        public Action<SequenceDataPacket, bool> OnComplete { get; set; }

        public SyncedTaskSequence(List<SyncSequenceStage> stages, string name = null, bool repeat = false,
            Action<SyncedTaskSequence> onComplete = null)
        {
            _stages = stages;
            _name = name ?? "SyncSequence";
            _repeat = repeat;
            _onComplete = onComplete;
            _state = ERunnerState.Ready;

            // Precompute the last critical stage index
            _lastCriticalStageIndex = -1;
            for (int i = _stages.Count - 1; i >= 0; i--)
            {
                if (_stages[i].IsCritical)
                {
                    _lastCriticalStageIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// Advances the runner by one frame. Returns true when all stages are complete.
        /// Call this from WhenUpdate on a process.
        /// </summary>
        public bool Step(SequenceDataPacket data, float deltaTime)
        {
            if (_state == ERunnerState.Completed || _state == ERunnerState.Interrupted)
                return true;

            if (_state == ERunnerState.Ready)
                _state = ERunnerState.Running;

            _elapsedTime += deltaTime;

            // So that a task can break its stage's repeat through the packet, as it would in an async sequence (TS-Q)
            if (data != null) data.SyncRunner = this;

            while (_stageIndex < _stages.Count)
            {
                var stage = _stages[_stageIndex];

                if (!_stagePrepared)
                {
                    stage.Prepare(data);
                    _stagePrepared = true;
                }

                bool stageComplete = stage.Step(data, deltaTime);

                // A task can end the sequence from inside its step, and the stage it was in is cleaned already (TS-P)
                if (_state == ERunnerState.Interrupted) return true;

                if (stageComplete)
                {
                    // Fire critical section exit when the last critical stage completes.
                    // Must fire before advancing _stageIndex so the index is still valid.
                    if (!_criticalExitFired &&
                        stage.IsCritical &&
                        _lastCriticalStageIndex >= 0 &&
                        _stageIndex >= _lastCriticalStageIndex)
                    {
                        _criticalExitFired = true;
                        OnCriticalSectionExited?.Invoke();
                    }

                    stage.End(data, !stage.TimedOut);
                    _stageIndex++;
                    _stagePrepared = false;

                    // Don't step the next stage on the same frame — give it a fresh delta
                    break;
                }
                else
                {
                    // Stage is still running, wait for next frame
                    return false;
                }
            }

            if (_stageIndex >= _stages.Count)
            {
                if (_repeat)
                {
                    _onComplete?.Invoke(this);
                    NotifyComplete(data, true);
                    Reset();
                    return false;
                }

                _state = ERunnerState.Completed;
                _onComplete?.Invoke(this);
                NotifyComplete(data, true);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Interrupts the runner, cleaning up the current stage. False if it had already ended.
        /// </summary>
        public bool Interrupt(SequenceDataPacket data)
        {
            // One that hasn't stepped yet is stopped too: it would otherwise start on its next step as if nothing
            // had happened (TS-P)
            if (_state is ERunnerState.Completed or ERunnerState.Interrupted) return false;

            bool started = _state == ERunnerState.Running;

            if (_stageIndex < _stages.Count && _stagePrepared)
            {
                _stagePrepared = false;
                _stages[_stageIndex].End(data, false);
            }

            _state = ERunnerState.Interrupted;
            if (started) NotifyComplete(data, false);
            return true;
        }

        /// <summary>
        /// Resets the runner to stage 0 for reuse or repeat.
        /// </summary>
        public void Reset()
        {
            _stageIndex = 0;
            _stagePrepared = false;
            _elapsedTime = 0f;
            _criticalExitFired = false;
            _state = ERunnerState.Ready;
        }

        /// <summary>
        /// Breaks the current stage's repeat loop. This is where <see cref="SequenceDataPacket.BreakStageRepeat()"/>
        /// goes in a sync sequence, which has no runtime for it to go to.
        /// </summary>
        public bool BreakStageRepeat()
        {
            if (_stageIndex >= _stages.Count) return false;

            _stages[_stageIndex].BreakRepeat();
            return true;
        }

        // A handler's throw is its own, and this is on the way out of the sequence
        private void NotifyComplete(SequenceDataPacket data, bool completed)
        {
            try
            {
                OnComplete?.Invoke(data, completed);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SyncSequence] Exception in OnComplete handler: {ex}");
            }
        }

        /// <summary>
        /// Skips the current stage and advances to the next.
        /// </summary>
        public bool SkipStage(SequenceDataPacket data)
        {
            if (_stageIndex >= _stages.Count) return false;

            if (_stagePrepared)
            {
                _stages[_stageIndex].End(data, false);
            }

            _stageIndex++;
            _stagePrepared = false;
            return true;
        }

        /// <summary>
        /// Jumps to a specific stage index.
        /// </summary>
        public bool JumpToStage(SequenceDataPacket data, int index)
        {
            if (index < 0 || index >= _stages.Count) return false;

            if (_stageIndex < _stages.Count && _stagePrepared)
            {
                _stages[_stageIndex].End(data, false);
            }

            _stageIndex = index;
            _stagePrepared = false;
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SYNC SEQUENCE STAGE (uses unified ISequenceTask)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A stage containing one or more ISequenceTask instances driven synchronously.
    /// All tasks Step in parallel each frame; stage completes per its policy.
    /// </summary>
    public class SyncSequenceStage
    {
        public readonly List<ISequenceTask> Tasks = new();
        public readonly string Name;
        public readonly ESyncStagePolicy Policy;
        public readonly bool Repeat;
        public readonly float? MaxDuration;
        public readonly bool IsCritical;

        private readonly bool[] _taskComplete;
        private int _completedCount;
        private float _stageElapsed;
        private bool _repeatBroken;
        private bool _timedOut;

        /// <summary>How many tasks end a <see cref="ESyncStagePolicy.WhenN"/> stage.</summary>
        public int RequiredCount { get; set; }

        /// <summary>Called as the stage starts, with its tasks prepared.</summary>
        public Action<SequenceDataPacket> OnInit { get; set; }

        /// <summary>Called after each time round a repeating stage. It can break the repeat from here.</summary>
        public Action<SequenceEventContext> OnRepeat { get; set; }

        /// <summary>Called as the stage ends, with whether it got to its end rather than being stopped or timing out.</summary>
        public Action<SequenceEventContext, bool> OnTerminate { get; set; }

        /// <summary>Elapsed time for this stage.</summary>
        public float StageElapsed => _stageElapsed;

        /// <summary>True if the stage ended at its max duration rather than by its tasks.</summary>
        public bool TimedOut => _timedOut;

        /// <summary>Break the repeat loop for this stage.</summary>
        public void BreakRepeat() => _repeatBroken = true;

        public SyncSequenceStage(List<ISequenceTask> tasks, string name = null,
            ESyncStagePolicy policy = ESyncStagePolicy.WhenAll, bool repeat = false,
            float? maxDuration = null, bool isCritical = false)
        {
            Tasks = tasks;
            Name = name;
            Policy = policy;
            Repeat = repeat;
            MaxDuration = maxDuration;
            IsCritical = isCritical;
            _taskComplete = new bool[tasks.Count];
        }

        public void Prepare(SequenceDataPacket data)
        {
            ResetInternal();

            // Broken for one run of the stage only: a repeat of the sequence runs it again
            _repeatBroken = false;

            foreach (var task in Tasks)
                task.Prepare(data);

            OnInit?.Invoke(data);
        }

        /// <summary>
        /// Steps all incomplete tasks. Returns true when the stage is complete per its policy.
        /// </summary>
        public bool Step(SequenceDataPacket data, float deltaTime)
        {
            _stageElapsed += deltaTime;

            // Max duration check
            if (MaxDuration.HasValue && _stageElapsed >= MaxDuration.Value)
            {
                _timedOut = true;
                return true;
            }

            // Step all incomplete tasks
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (_taskComplete[i]) continue;

                if (Tasks[i].Step(data, deltaTime))
                {
                    _taskComplete[i] = true;
                    _completedCount++;
                }
            }

            // Check completion based on policy. A stage with no tasks has nothing to wait for, whatever its policy
            bool stageComplete = Tasks.Count == 0 || Policy switch
            {
                ESyncStagePolicy.WhenAll => _completedCount >= Tasks.Count,
                ESyncStagePolicy.WhenAny => _completedCount > 0,
                ESyncStagePolicy.WhenN => _completedCount >= Math.Min(RequiredCount, Tasks.Count),
                _ => _completedCount >= Tasks.Count
            };

            if (stageComplete && Repeat && !_repeatBroken)
            {
                try
                {
                    OnRepeat?.Invoke(new SequenceEventContext(data, null));
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[SyncSequence] Exception in stage OnRepeat handler: {ex}");
                }

                // OnRepeat can break the repeat, and then this was the last time round
                if (_repeatBroken) return true;

                // Reset for next repeat iteration, which is a run of the tasks like the last one
                Clean(data);
                ResetInternal();
                foreach (var task in Tasks)
                    task.Prepare(data);
                return false;
            }

            return stageComplete;
        }

        /// <summary>Ends the stage: tells OnTerminate how, then cleans its tasks.</summary>
        public void End(SequenceDataPacket data, bool completed)
        {
            // The clean below has to happen, so a handler's throw stops here
            try
            {
                OnTerminate?.Invoke(new SequenceEventContext(data, null), completed);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SyncSequence] Exception in stage OnTerminate handler: {ex}");
            }

            Clean(data);
        }

        public void Clean(SequenceDataPacket data)
        {
            foreach (var task in Tasks)
                task.Clean(data);
        }

        private void ResetInternal()
        {
            _completedCount = 0;
            _stageElapsed = 0f;
            _timedOut = false;
            for (int i = 0; i < _taskComplete.Length; i++)
                _taskComplete[i] = false;
        }
    }

    /// <summary>
    /// Completion policy for synchronous stages.
    /// </summary>
    public enum ESyncStagePolicy
    {
        /// <summary>Stage completes when all tasks are complete.</summary>
        WhenAll,

        /// <summary>Stage completes when any task is complete.</summary>
        WhenAny,

        /// <summary>Stage completes when <see cref="SyncSequenceStage.RequiredCount"/> tasks are complete.</summary>
        WhenN
    }
}
