using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Wraps a TaskSequence or TaskSequenceChain for execution via ProcessControl.
    /// Handles condition checking in WhenUpdate when conditions are present.
    /// Uses SequenceDataPacket for direct sequence/stage control.
    /// </summary>
    public class TaskSequenceProcess : LazyRuntimeProcess
    {
        private readonly TaskSequence _sequence;
        private readonly TaskSequenceChain _chain;
        private readonly bool _hasConditions;
        
        /// <summary>The sequence being executed (null if using chain).</summary>
        public TaskSequence Sequence => _sequence;
        
        /// <summary>The chain being executed (null if using single sequence).</summary>
        public TaskSequenceChain Chain => _chain;
        
        /// <summary>True if running a chain, false if running a single sequence.</summary>
        public bool IsChain => _chain != null;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a process for a single sequence.
        /// </summary>
        public TaskSequenceProcess(TaskSequence sequence) 
            : base(
                sequence.Definition.Metadata?.Name ?? "TaskSequence",
                EProcessStepPriorityMethod.First,
                0,
                sequence.HasConditions ? EProcessStepTiming.Update : EProcessStepTiming.None,
                EProcessLifecycle.SelfTerminating)
        {
            _sequence = sequence;
            _hasConditions = sequence.HasConditions;
        }
        
        /// <summary>
        /// Creates a process for a sequence chain.
        /// </summary>
        public TaskSequenceProcess(TaskSequenceChain chain, string name = "TaskSequenceChain")
            : base(
                name,
                EProcessStepPriorityMethod.First,
                0,
                chain.HasConditions ? EProcessStepTiming.Update : EProcessStepTiming.None,
                EProcessLifecycle.SelfTerminating)
        {
            _chain = chain;
            _hasConditions = chain.HasConditions;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STATIC FACTORY METHODS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates and registers a TaskSequenceProcess with ProcessControl.
        /// Uses SequenceDataPacket for full sequence control access.
        /// </summary>
        public static ProcessRelay Register(TaskSequence sequence, SequenceDataPacket data = null)
        {
            var process = new TaskSequenceProcess(sequence);
            var _data = data ?? SequenceDataPacket.RootDefault();
            ProcessControl.Instance.Register(process, _data, out var relay);
            return relay;
        }
        
        /// <summary>
        /// Creates and registers a TaskSequenceProcess from a builder.
        /// Uses SequenceDataPacket for full sequence control access.
        /// </summary>
        public static ProcessRelay Register(TaskSequenceBuilder builder, SequenceDataPacket data = null)
        {
            return Register(builder.BuildSequence(), data);
        }
        
        /// <summary>
        /// Creates and registers a TaskSequenceProcess for a chain.
        /// Uses SequenceDataPacket for full sequence control access.
        /// </summary>
        public static ProcessRelay Register(TaskSequenceChain chain, SequenceDataPacket data = null, string name = null)
        {
            var process = new TaskSequenceProcess(chain, name ?? "TaskSequenceChain");
            var _data = data ?? SequenceDataPacket.RootDefault();
            ProcessControl.Instance.Register(process, _data, out var relay);
            return relay;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Checks conditions each update tick if conditions are present.
        /// </summary>
        public override void WhenUpdate(ProcessRelay relay)
        {
            if (!_hasConditions) return;
            
            if (IsChain)
            {
                _chain.CheckConditions();
            }
            else
            {
                _sequence.CheckConditions();
            }
        }
        
        /// <summary>
        /// Runs the sequence or chain.
        /// </summary>
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            processActive = true;
            
            try
            {
                if (IsChain)
                {
                    await _chain.Run(regData, token);
                }
                else
                {
                    await _sequence.Run(regData, token);
                }
            }
            finally
            {
                processActive = false;
            }
        }

        public override void WhenWait(ProcessRelay relay)
        {
            Time.timeScale = 0f;
        }

        public override bool HandlePause(ProcessRelay relay)
        {
            return processActive;
        }

        public override bool HandleResume(ProcessRelay relay)
        {
            
            
            if (!processActive) return false;
            
            Time.timeScale = 1f;
            return true;
        }

        /// <summary>
        /// Returns the step timing based on whether conditions are present.
        /// </summary>
        public override EProcessStepTiming StepTiming => GetStepTiming();

        private EProcessStepTiming GetStepTiming()
        {
            if (!_hasConditions) return EProcessStepTiming.None;
            
            return IsChain ? _chain.ConditionStepTiming : _sequence.Definition.ConditionStepTiming;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // INJECTION PASSTHROUGH
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Injects into the running sequence/chain.
        /// </summary>
        public bool Inject(ISequenceInjection injection)
        {
            return IsChain ? _chain.Inject(injection) : _sequence.Inject(injection);
        }
        
        /// <summary>
        /// Interrupts the running sequence/chain.
        /// For chains, this interrupts the entire chain (not just the current sequence).
        /// </summary>
        public bool Interrupt()
        {
            return IsChain ? _chain.Interrupt() : _sequence.Interrupt();
        }
        
        /// <summary>
        /// Skips the current sequence in the chain and moves to the next.
        /// Only applicable for chains.
        /// </summary>
        public bool SkipCurrentSequence()
        {
            return IsChain && _chain.SkipCurrentSequence();
        }
    }
}