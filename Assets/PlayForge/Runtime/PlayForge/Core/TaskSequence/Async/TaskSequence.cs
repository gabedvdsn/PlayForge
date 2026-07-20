using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    // ═══════════════════════════════════════════════════════════════════════════
    // TASK SEQUENCE
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A reusable async task sequence that can be executed with a data packet.
    /// Wraps a TaskSequenceDefinition with execution capabilities.
    /// </summary>
    public class TaskSequence : IActiveSequence
    {
        /// <summary>The immutable definition of this sequence.</summary>
        public TaskSequenceDefinition Definition { get; }
        
        /// <summary>The current runtime (null when not running).</summary>
        public TaskSequenceRuntime Runtime { get; private set; }
        
        /// <summary>True if the sequence is currently executing.</summary>
        public bool IsRunning => Runtime?.IsRunning ?? false;
        
        /// <summary>True if conditions need to be checked each update.</summary>
        public bool HasConditions => Definition.HasAnyConditions;
        
        public TaskSequence(TaskSequenceDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public TaskSequence(TaskSequence other)
        {
            Definition = other.Definition ?? throw new ArgumentNullException(nameof(other.Definition));
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EXECUTION
        // ═══════════════════════════════════════════════════════════════════════════

        public ProcessRelay RegisterAndRun(SequenceDataPacket data)
        {
            ProcessControl.Register(this, data, out var relay);
            return relay;
        }
        
        /// <summary>
        /// Runs the sequence with the provided data packet.
        /// </summary>
        public async UniTask Run(ProcessDataPacket data, CancellationToken token = default)
        {
            Runtime = new TaskSequenceRuntime(Definition);
            Runtime.OnCriticalSectionExited = OnCriticalSectionExited;

            try
            {
                await Runtime.Run(data, token);
            }
            finally
            {
                Runtime = null;
            }
        }

        /// <summary>
        /// Runs the sequence, returning success status.
        /// </summary>
        public async UniTask<bool> TryRun(ProcessDataPacket data, CancellationToken token = default)
        {
            Runtime = new TaskSequenceRuntime(Definition);
            Runtime.OnCriticalSectionExited = OnCriticalSectionExited;

            try
            {
                await Runtime.Run(data, token);
                return Runtime.CompletedSuccessfully;
            }
            finally
            {
                Runtime = null;
            }
        }

        public bool IsCriticalSection => IsRunning ? Runtime.IsCriticalSection : false;

        /// <summary>
        /// Fired once when the last critical section in this sequence exits.
        /// Set this before calling Run() — it is forwarded to the runtime on each run.
        /// </summary>
        public Action OnCriticalSectionExited { get; set; }

        // ═══════════════════════════════════════════════════════════════════════════
        // INJECTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Applies an injection to the running sequence.
        /// </summary>
        public bool Inject(ISequenceInjection injection)
        {
            return Runtime?.Inject(injection) ?? false;
        }
        
        /// <summary>
        /// Interrupts the running sequence.
        /// </summary>
        public bool Interrupt()
        {
            return Inject(InterruptSequenceInjection.Instance);
        }
        
        /// <summary>
        /// Skips the current stage.
        /// </summary>
        public bool SkipCurrentStage()
        {
            return Inject(SkipStageInjection.Instance);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CONDITION CHECKING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Checks conditions and applies injections. Called from WhenUpdate.
        /// </summary>
        public void CheckConditions()
        {
            Runtime?.CheckConditions();
        }
        
        /// <summary>
        /// Checks external conditions against the current runtime.
        /// </summary>
        public void CheckConditions(IReadOnlyList<SequenceCondition> conditions)
        {
            Runtime?.CheckConditions(conditions);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CHAINING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Chains this sequence with another, running sequentially with shared data.
        /// </summary>
        public TaskSequenceChain Then(TaskSequence next)
        {
            return new TaskSequenceChain(this).Then(next);
        }
        
        /// <summary>
        /// Chains this sequence with another built inline.
        /// </summary>
        public TaskSequenceChain Then(Action<TaskSequenceBuilder> configure)
        {
            var builder = new TaskSequenceBuilder();
            configure(builder);
            return Then(builder.BuildSequence());
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // TASK SEQUENCE CHAIN
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A chain of sequences that execute sequentially with shared data.
    /// Supports chain-level conditions, repeat, and interrupt.
    /// </summary>
    public class TaskSequenceChain : IActiveSequence
    {
        private readonly List<TaskSequence> _sequences = new();
        private readonly List<SequenceCondition> _conditions = new();
        
        private int _currentIndex = -1;
        private TaskSequence _currentSequence;
        private CancellationTokenSource _chainCts;
        private bool _interrupted;
        private bool _repeats;
        private bool _enableLogging;
        private EProcessStepTiming _conditionStepTiming = EProcessStepTiming.Update;
        
        /// <summary>The data packet being used (set during Run).</summary>
        public SequenceDataPacket Data { get; private set; }
        
        /// <summary>Current sequence index.</summary>
        public int CurrentIndex => _currentIndex;
        
        /// <summary>Current sequence index (alias for CurrentIndex).</summary>
        public int CurrentSequenceIndex => _currentIndex;
        
        /// <summary>Currently executing sequence.</summary>
        public TaskSequence CurrentSequence => _currentSequence;
        
        /// <summary>True if the chain is currently running.</summary>
        public bool IsRunning { get; private set; }
        
        /// <summary>Total number of sequences in the chain.</summary>
        public int Count => _sequences.Count;
        
        /// <summary>Total number of sequences in the chain (alias for Count).</summary>
        public int SequenceCount => _sequences.Count;
        
        /// <summary>True if the chain was interrupted.</summary>
        public bool WasInterrupted => _interrupted;
        
        /// <summary>Timing for condition checking.</summary>
        public EProcessStepTiming ConditionStepTiming => _conditionStepTiming;
        
        /// <summary>True if the chain or any sequence has conditions.</summary>
        public bool HasConditions
        {
            get
            {
                if (_conditions.Count > 0) return true;
                return _sequences.Any(seq => seq.HasConditions);
            }
        }
        
        public TaskSequenceChain() { }
        
        public TaskSequenceChain(TaskSequence first)
        {
            _sequences.Add(first);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // BUILDER METHODS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Adds a sequence to the chain.
        /// </summary>
        public TaskSequenceChain Then(TaskSequence sequence)
        {
            _sequences.Add(sequence);
            return this;
        }
        
        /// <summary>
        /// Adds a sequence built inline.
        /// </summary>
        public TaskSequenceChain Then(Action<TaskSequenceBuilder> configure)
        {
            var builder = new TaskSequenceBuilder();
            configure(builder);
            return Then(builder.BuildSequence());
        }
        
        /// <summary>
        /// Enables repeating the chain after successful completion.
        /// </summary>
        public TaskSequenceChain WithRepeat(bool enabled)
        {
            _repeats = enabled;
            return this;
        }
        
        /// <summary>
        /// Enables logging for chain events.
        /// </summary>
        public TaskSequenceChain WithLogging(bool enabled)
        {
            _enableLogging = enabled;
            return this;
        }
        
        /// <summary>
        /// Sets the timing for condition checking.
        /// </summary>
        public TaskSequenceChain WithConditionCheckTiming(EProcessStepTiming timing)
        {
            _conditionStepTiming = timing;
            return this;
        }
        
        /// <summary>
        /// Adds a chain-level condition that triggers an injection when met.
        /// </summary>
        public TaskSequenceChain InjectWhen(
            Func<SequenceDataPacket, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
        {
            _conditions.Add(new SequenceCondition(predicate, injection, fireOnce));
            return this;
        }
        
        /// <summary>
        /// Adds a chain-level condition with runtime access.
        /// </summary>
        public TaskSequenceChain InjectWhen(
            Func<SequenceDataPacket, TaskSequenceRuntime, bool> predicate,
            ISequenceInjection injection,
            bool fireOnce = false)
        {
            _conditions.Add(new SequenceCondition(predicate, injection, fireOnce));
            return this;
        }
        
        /// <summary>
        /// Adds a chain-level interrupt condition. Interrupts the ENTIRE chain.
        /// </summary>
        public TaskSequenceChain InterruptWhen(Func<SequenceDataPacket, bool> predicate, bool fireOnce = true)
        {
            // Use ChainInterruptInjection marker - handled specially
            _conditions.Add(new SequenceCondition(predicate, ChainInterruptInjection.Instance, fireOnce));
            return this;
        }
        
        /// <summary>
        /// Adds a chain-level skip condition. Skips current sequence, continues to next.
        /// </summary>
        public TaskSequenceChain SkipSequenceWhen(Func<SequenceDataPacket, bool> predicate, bool fireOnce = false)
        {
            return InjectWhen(predicate, InterruptSequenceInjection.Instance, fireOnce);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EXECUTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Runs all sequences in order with shared data.
        /// Breaks on chain interrupt or unrecoverable failure.
        /// </summary>
        public async UniTask Run(ProcessDataPacket data, CancellationToken externalToken = default)
        {
            if (data is not SequenceDataPacket seqData)
            {
                throw new InvalidOperationException("TaskSequenceChain requires SequenceDataPacket");
            }
            
            Data = seqData;
            seqData.Chain = this;
            
            _currentIndex = -1;
            _currentSequence = null;
            _interrupted = false;
            IsRunning = true;
            
            // Create chain-level CTS
            _chainCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var chainToken = _chainCts.Token;
            
            bool completedAllSequences = false;
            
            try
            {
                // Reset all conditions
                foreach (var condition in _conditions)
                {
                    condition.Reset();
                }
                
                for (int i = 0; i < _sequences.Count; i++)
                {
                    _currentIndex = i;
                    _currentSequence = _sequences[i];
                    
                    // Check if chain was interrupted before starting sequence
                    if (chainToken.IsCancellationRequested || _interrupted)
                    {
                        if (_enableLogging)
                        {
                            Debug.Log($"[TaskSequenceChain] Chain interrupted before sequence {i}");
                        }
                        break;
                    }
                    
                    if (_enableLogging)
                    {
                        Debug.Log($"[TaskSequenceChain] Starting sequence {i}: {_currentSequence.Definition.Metadata?.Name ?? "unnamed"}");
                    }
                    
                    // Run the sequence with the CHAIN's token
                    bool success = await _currentSequence.TryRun(Data, chainToken);
                    
                    // Check if chain was interrupted during sequence
                    if (_interrupted)
                    {
                        if (_enableLogging)
                        {
                            Debug.Log($"[TaskSequenceChain] Chain was interrupted during sequence {i}");
                        }
                        break;
                    }
                    
                    // If sequence failed but chain wasn't interrupted, it might be a skip
                    // Continue to next sequence (skip behavior)
                    if (!success && !chainToken.IsCancellationRequested)
                    {
                        if (_enableLogging)
                        {
                            Debug.Log($"[TaskSequenceChain] Sequence {i} was skipped/cancelled, continuing chain");
                        }
                        continue;
                    }
                    
                    // If chain token was cancelled externally, break
                    if (chainToken.IsCancellationRequested)
                    {
                        break;
                    }
                    
                    if (_enableLogging)
                    {
                        Debug.Log($"[TaskSequenceChain] Sequence {i} completed successfully");
                    }
                }
                
                // Mark as completed if we ran all sequences without interrupt
                completedAllSequences = _currentIndex >= _sequences.Count - 1 && !_interrupted && !chainToken.IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                if (_enableLogging)
                {
                    Debug.Log("[TaskSequenceChain] Chain was cancelled");
                }
                completedAllSequences = false;
            }
            finally
            {
                _currentSequence = null;
                IsRunning = false;
                
                _chainCts?.Dispose();
                _chainCts = null;
            }
            
            // Handle repeat - ONLY if completed successfully and not interrupted
            if (_repeats && completedAllSequences && !_interrupted)
            {
                if (_enableLogging)
                {
                    Debug.Log("[TaskSequenceChain] Repeating chain...");
                }
                await Run(data, externalToken);
            }
            else
            {
                Data.Chain = null;
            }
        }
        
        /// <summary>
        /// Runs the chain, returning success status.
        /// </summary>
        public async UniTask<bool> TryRun(ProcessDataPacket data, CancellationToken token = default)
        {
            await Run(data, token);
            return !_interrupted && _currentIndex >= _sequences.Count - 1;
        }

        public bool IsCriticalSection => IsRunning && _currentSequence.Runtime.IsCriticalSection;

        // ═══════════════════════════════════════════════════════════════════════════
        // CONDITION CHECKING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Checks conditions on the currently running sequence and chain-level conditions.
        /// Called from TaskSequenceProcess.WhenUpdate.
        /// </summary>
        public void CheckConditions()
        {
            if (!IsRunning || Data == null) return;
            
            // Check sequence-level conditions first
            _currentSequence?.CheckConditions();
            
            // Then check chain-level conditions
            CheckChainConditions();
        }
        
        private void CheckChainConditions()
        {
            if (Data == null) return;
            
            foreach (var condition in _conditions)
            {
                if (condition.FireOnce && condition.HasFired) continue;
                
                // Get runtime for predicate (may be null between sequences)
                var runtime = _currentSequence?.Runtime;
                
                bool conditionMet = condition.Predicate(Data, runtime);
                
                if (conditionMet)
                {
                    condition.MarkFired();
                    
                    if (_enableLogging)
                    {
                        Debug.Log($"[TaskSequenceChain] Condition triggered: {condition.Injection.GetType().Name}");
                    }
                    
                    // Special handling for chain interrupt
                    if (condition.Injection is ChainInterruptInjection)
                    {
                        Interrupt();
                    }
                    else if (runtime != null)
                    {
                        // Apply other injections to current sequence runtime
                        condition.Injection.Apply(runtime);
                    }
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // INJECTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Injects into the currently running sequence.
        /// </summary>
        public bool Inject(ISequenceInjection injection)
        {
            return _currentSequence?.Inject(injection) ?? false;
        }
        
        /// <summary>
        /// Interrupts the entire chain (cancels current and all remaining sequences).
        /// </summary>
        public bool Interrupt()
        {
            if (!IsRunning) return false;
            
            _interrupted = true;
            
            // Cancel the chain CTS - this stops everything
            _chainCts?.Cancel();
            
            if (_enableLogging)
            {
                Debug.Log("[TaskSequenceChain] Chain interrupted!");
            }
            
            return true;
        }
        
        /// <summary>
        /// Skips the current sequence and moves to the next in the chain.
        /// </summary>
        public bool SkipCurrentSequence()
        {
            // Interrupt current sequence but don't set _interrupted flag
            // so chain continues to next sequence
            return _currentSequence?.Interrupt() ?? false;
        }
    }
}