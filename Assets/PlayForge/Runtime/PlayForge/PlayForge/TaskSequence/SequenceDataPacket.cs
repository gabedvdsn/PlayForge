using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Extended ProcessDataPacket for TaskSequence execution.
    /// Provides direct access to sequence, stage, and chain control.
    /// All TaskSequence builder methods use this type for delegates.
    /// </summary>
    public class SequenceDataPacket : ProcessDataPacket
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // RUNTIME REFERENCES (set internally by TaskSequenceRuntime/Chain)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Current sequence runtime (null if not running).</summary>
        public TaskSequenceRuntime Runtime { get; internal set; }
        
        /// <summary>Current chain (null if running single sequence).</summary>
        public TaskSequenceChain Chain { get; internal set; }
        
        /// <summary>True if currently running within a chain.</summary>
        public bool IsInChain => Chain != null;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STATE ACCESSORS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Current stage index (-1 if not started).</summary>
        public int StageIndex => Runtime?.StageIndex ?? -1;
        
        /// <summary>Current stage (null if not running).</summary>
        public SequenceStage CurrentStage => Runtime?.CurrentStage;
        
        /// <summary>Elapsed time since sequence started.</summary>
        public float ElapsedTime => Runtime?.ElapsedTime ?? 0f;
        
        /// <summary>True if the sequence is currently running.</summary>
        public bool IsSequenceRunning => Runtime?.IsRunning ?? false;
        
        /// <summary>Sequence definition being executed.</summary>
        public TaskSequenceDefinition Definition => Runtime?.Definition;
        
        /// <summary>Current sequence index in chain (0 if single sequence).</summary>
        public int ChainSequenceIndex => Chain?.CurrentSequenceIndex ?? 0;
        
        /// <summary>Total sequences in chain (1 if single sequence).</summary>
        public int ChainSequenceCount => Chain?.SequenceCount ?? 1;
        
        /// <summary>Sequence name (from metadata).</summary>
        public string SequenceName => Definition?.Metadata?.Name;
        
        /// <summary>Current stage name (from metadata).</summary>
        public string StageName => CurrentStage?.Metadata?.Name;
        
        /// <summary>How many times the current stage has repeated.</summary>
        public int StageRepeatCount => CurrentStage?.RepeatCount ?? 0;
        
        /// <summary>True if the sequence is currently inside a critical section.</summary>
        public bool IsCriticalSection => Runtime?.IsCriticalSection ?? false;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SEQUENCE CONTROL
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Interrupts the current sequence immediately.
        /// </summary>
        public bool Interrupt()
        {
            return Runtime?.Inject(InterruptSequenceInjection.Instance) ?? false;
        }
        
        /// <summary>
        /// Skips the current stage and moves to the next.
        /// </summary>
        public bool SkipStage()
        {
            return Runtime?.Inject(SkipStageInjection.Instance) ?? false;
        }
        
        /// <summary>
        /// Skips current stage but lets it continue running in background.
        /// </summary>
        public bool SkipAndMaintain()
        {
            return Runtime?.Inject(SkipAndMaintainInjection.Instance) ?? false;
        }
        
        /// <summary>
        /// Jumps to a specific stage index.
        /// </summary>
        public bool JumpToStage(int stageIndex)
        {
            return Runtime?.Inject(new JumpToStageInjection(stageIndex)) ?? false;
        }
        
        /// <summary>
        /// Jumps to a stage by name (searches definition).
        /// </summary>
        public bool JumpToStage(string stageName)
        {
            if (Definition == null) return false;
            
            for (int i = 0; i < Definition.Stages.Count; i++)
            {
                if (Definition.Stages[i].Metadata?.Name == stageName)
                {
                    return JumpToStage(i);
                }
            }
            return false;
        }
        
        /// <summary>
        /// Restarts the sequence from stage 0.
        /// </summary>
        public bool RestartSequence()
        {
            return JumpToStage(0);
        }
        
        /// <summary>
        /// Injects a custom injection (global permissions).
        /// </summary>
        public bool Inject(ISequenceInjection injection, bool overridePermission = false)
        {
            return Runtime?.Inject(injection, overridePermission) ?? false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE-LOCAL INJECTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Injects using stage-local permissions (respects stage AllowedInjections).
        /// </summary>
        public bool InjectStageLocal(ISequenceInjection injection)
        {
            return Runtime?.InjectStageLocal(injection) ?? false;
        }
        
        /// <summary>
        /// Injects using stage-local permissions for a specific stage.
        /// </summary>
        public bool InjectStageLocal(ISequenceInjection injection, SequenceStage stage)
        {
            return Runtime?.InjectStageLocal(injection, stage) ?? false;
        }
        
        public async UniTask<bool> CheckInjectConditions(Func<bool> condition, ISequenceInjection injection, CancellationToken t)
        {
            if (!(condition?.Invoke() ?? false)) return false;

            await UniTask.NextFrame(t);
            Inject(injection);
            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE REPEAT CONTROL
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Breaks the current stage's repeat loop (for WithRepeat or RepeatUntilSkippedPolicy).
        /// Does not skip the stage - just stops repeating and continues normally.
        /// </summary>
        public bool BreakStageRepeat()
        {
            return Runtime?.RequestBreakCurrentStageRepeat() ?? false;
        }
        
        /// <summary>
        /// Breaks a specific stage's repeat loop.
        /// </summary>
        public bool BreakStageRepeat(SequenceStage stage)
        {
            return Runtime?.RequestBreakStageRepeat(stage) ?? false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CHAIN CONTROL
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Interrupts the entire chain (stops all sequences).
        /// Falls back to single sequence interrupt if not in chain.
        /// </summary>
        public bool InterruptChain()
        {
            return Chain?.Interrupt() ?? Interrupt();
        }
        
        /// <summary>
        /// Skips the current sequence and moves to the next in the chain.
        /// Only works if running in a chain.
        /// </summary>
        public bool SkipSequence()
        {
            return Chain?.SkipCurrentSequence() ?? false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DYNAMIC STAGE MODIFICATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Sets a timeout on the current stage (if not already set).
        /// </summary>
        public bool SetStageTimeout(float seconds, ISequenceInjection onTimeout = null)
        {
            return Runtime.RequestStartStageTimeout(new SequenceTimeout()
            {
                Seconds = seconds,
                Injection = onTimeout
            });
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected SequenceDataPacket() { }

        public SequenceDataPacket(ProcessDataPacket other) : base()
        {
            _payload = new Dictionary<Tag, List<object>>();

            if (other is null) return;
            
            foreach (var kvp in other.Payload)
            {
                _payload[kvp.Key] = new List<object>();
                foreach (object data in kvp.Value) _payload[kvp.Key].Add(data);
            }
            
            InUse = other.InUse;
        }
        
        /// <summary>
        /// Empty data packet handled by GameRoot.
        /// </summary>
        public new static SequenceDataPacket Default()
        {
            return Internal_GenerateDataPacket(false, null);
        }

        /// <summary>
        /// Data packet handled by GameRoot, automatically assigned as a child of GameRoot in-scene.
        /// </summary>
        public new static SequenceDataPacket SceneRoot()
        {
            return Internal_GenerateDataPacket(true, GameRoot.Instance.transform);
        }
        
        /// <summary>
        /// Data packet handled by GameRoot, assigned as a child of parent in-scene
        /// </summary>
        /// <param name="parent">Parent transform to assign</param>
        /// <returns></returns>
        public new static SequenceDataPacket SceneLocal(Transform parent)
        {
            return Internal_GenerateDataPacket(true, parent);
        }

        private new static SequenceDataPacket Internal_GenerateDataPacket(bool setParent, Transform parent)
        {
            var data = new SequenceDataPacket();
            
            if (setParent) data.SetPrimary(Tags.PARENT_TRANSFORM, parent);
            
            return data;
        }

        public override ProcessDataPacket CreateNew()
        {
            return new SequenceDataPacket(this);
        }

    }
}