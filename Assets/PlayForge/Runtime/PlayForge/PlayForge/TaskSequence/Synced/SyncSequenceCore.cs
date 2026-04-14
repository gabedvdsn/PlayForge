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

        public SyncedTaskSequence(List<SyncSequenceStage> stages, string name = null, bool repeat = false,
            Action<SyncedTaskSequence> onComplete = null)
        {
            _stages = stages;
            _name = name ?? "SyncSequence";
            _repeat = repeat;
            _onComplete = onComplete;
            _state = ERunnerState.Ready;
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

            while (_stageIndex < _stages.Count)
            {
                var stage = _stages[_stageIndex];

                if (!_stagePrepared)
                {
                    stage.Prepare(data);
                    _stagePrepared = true;
                }

                if (stage.Step(data, deltaTime))
                {
                    stage.Clean(data);
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
                    Reset();
                    return false;
                }

                _state = ERunnerState.Completed;
                _onComplete?.Invoke(this);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Interrupts the runner, cleaning up the current stage.
        /// </summary>
        public void Interrupt(SequenceDataPacket data)
        {
            if (_state != ERunnerState.Running) return;

            if (_stageIndex < _stages.Count && _stagePrepared)
            {
                _stages[_stageIndex].Clean(data);
            }

            _state = ERunnerState.Interrupted;
        }

        /// <summary>
        /// Resets the runner to stage 0 for reuse or repeat.
        /// </summary>
        public void Reset()
        {
            _stageIndex = 0;
            _stagePrepared = false;
            _elapsedTime = 0f;
            _state = ERunnerState.Ready;
        }

        /// <summary>
        /// Skips the current stage and advances to the next.
        /// </summary>
        public bool SkipStage(SequenceDataPacket data)
        {
            if (_stageIndex >= _stages.Count) return false;

            if (_stagePrepared)
            {
                _stages[_stageIndex].Clean(data);
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
                _stages[_stageIndex].Clean(data);
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

        private readonly bool[] _taskComplete;
        private int _completedCount;
        private float _stageElapsed;
        private bool _repeatBroken;

        /// <summary>Elapsed time for this stage.</summary>
        public float StageElapsed => _stageElapsed;

        /// <summary>Break the repeat loop for this stage.</summary>
        public void BreakRepeat() => _repeatBroken = true;

        public SyncSequenceStage(List<ISequenceTask> tasks, string name = null,
            ESyncStagePolicy policy = ESyncStagePolicy.WhenAll, bool repeat = false,
            float? maxDuration = null)
        {
            Tasks = tasks;
            Name = name;
            Policy = policy;
            Repeat = repeat;
            MaxDuration = maxDuration;
            _taskComplete = new bool[tasks.Count];
        }

        public void Prepare(SequenceDataPacket data)
        {
            ResetInternal();
            foreach (var task in Tasks)
                task.Prepare(data);
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

            // Check completion based on policy
            bool stageComplete = Policy switch
            {
                ESyncStagePolicy.WhenAll => _completedCount >= Tasks.Count,
                ESyncStagePolicy.WhenAny => _completedCount > 0,
                _ => _completedCount >= Tasks.Count
            };

            if (stageComplete && Repeat && !_repeatBroken)
            {
                // Reset for next repeat iteration
                ResetInternal();
                foreach (var task in Tasks)
                    task.Prepare(data);
                return false;
            }

            return stageComplete;
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
        WhenAny
    }
}
