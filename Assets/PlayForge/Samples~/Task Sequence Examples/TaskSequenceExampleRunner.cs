using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// Runs a task sequence example from a scene. Pick a sequence, any static method marked [TaskSequenceMethod], enter
    /// Play mode, and press the run key; the pause key holds it and lets it go, and the stop key ends it. Each run takes a
    /// fresh sequence from its method and a fresh packet, SceneRoot's, which carries the Clock picked, where one is. A
    /// sequence runs through ProcessControl, as an
    /// ability's does, so its conditions are asked each frame and its process pauses it. A chain runs here, awaited, and
    /// this asks its conditions each frame, as ProcessControl would. The scene needs PlayForge up, a ProcessControl and a
    /// GameRoot, as a Bootstrapper brings them.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Example Runner")]
    public sealed class TaskSequenceExampleRunner : MonoBehaviour
    {
        [Tooltip("The example it runs: a static method marked [TaskSequenceMethod] that returns a sequence or a chain")]
        public TaskSequenceReference Sequence;

        [Tooltip("The key that runs it, read through ForgeInput")]
        public KeyCode RunKey = KeyCode.R;

        [Tooltip("The key that pauses it, and lets it go again")]
        public KeyCode PauseKey = KeyCode.P;

        [Tooltip("The key that stops it")]
        public KeyCode StopKey = KeyCode.Backspace;

        [Tooltip("The clock its run goes by, on its packet, before the example's own: none for the example's own or the game's")]
        [SerializeReference] public AbstractClockChoice Clock;

        // A sequence's run, through its process
        private ProcessRelay _relay;

        // A chain's run, here
        private TaskSequenceChain _chain;
        private CancellationTokenSource _chainCancel;

        private bool _paused;

        private string Name => Sequence.GetDisplayName();

        private bool SequenceRuns => _relay != null && _relay.ProcessActive;

        private bool ChainRuns => _chain != null && _chain.IsRunning;

        private void Update()
        {
            var input = ForgeInput.Current;
            if (input.KeyPressed(RunKey)) Run();
            else if (input.KeyPressed(PauseKey)) TogglePause();
            else if (input.KeyPressed(StopKey)) Stop();

            AskChainConditions(EProcessStepTiming.Update);
        }

        private void LateUpdate() => AskChainConditions(EProcessStepTiming.LateUpdate);

        private void FixedUpdate() => AskChainConditions(EProcessStepTiming.FixedUpdate);

        private void OnDestroy()
        {
            // The chain's run is this component's, and ends with it. A sequence's process is ProcessControl's
            _chainCancel?.Cancel();
        }

        private void Run()
        {
            if (SequenceRuns || ChainRuns)
            {
                ForgeLog.Info($"{Name} is running: {StopKey} stops it.", this);
                return;
            }

            if (!Sequence.IsValid)
            {
                ForgeLog.Warning("Pick a sequence to run.", this);
                return;
            }

            if (!ProcessControl.Instance || !GameRoot.Instance)
            {
                ForgeLog.Warning("PlayForge isn't up: the scene needs a Bootstrapper.", this);
                return;
            }

            // The method builds a new sequence each time it's called, so each run starts as the method made it
            IActiveSequence active = Sequence.GetActiveSequence();
            var data = SequenceDataPacket.SceneRoot();
            // The clock picked rides on the run's packet, before the example's own clock; none leaves it that one
            data.Clock = Clock?.ClockFor(data);
            _paused = false;

            if (Sequence.IsChain() && active is TaskSequenceChain chain) RunChain(chain, data).Forget();
            else if (active is TaskSequence sequence) RunSequence(sequence, data);
            else ForgeLog.Warning($"{Name} made nothing this runner can run.", this);
        }

        private void RunSequence(TaskSequence sequence, SequenceDataPacket data)
        {
            _relay = sequence.RegisterAndRun(data);
            if (_relay == null)
            {
                ForgeLog.Warning($"ProcessControl refused {Name}.", this);
                return;
            }

            ForgeLog.Info($"Running {Name}. {PauseKey} pauses it, {StopKey} stops it.", this);
            SayWhenEnded(_relay).Forget();
        }

        private async UniTaskVoid SayWhenEnded(ProcessRelay relay)
        {
            try
            {
                await relay.WhenTerminated(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            ForgeLog.Info($"{Name} has ended.", this);
        }

        private async UniTaskVoid RunChain(TaskSequenceChain chain, SequenceDataPacket data)
        {
            _chain = chain;
            _chainCancel = new CancellationTokenSource();
            ForgeLog.Info($"Running the chain {Name}. {PauseKey} pauses it, {StopKey} stops it.", this);

            try
            {
                await chain.Run(data, _chainCancel.Token);
                ForgeLog.Info($"The chain {Name} {(chain.WasInterrupted ? "was interrupted" : "has ended")}.", this);
            }
            catch (Exception exception)
            {
                ForgeLog.Error($"The chain {Name} failed: {exception.Message}", this);
            }
            finally
            {
                _chainCancel.Dispose();
                _chainCancel = null;
                _chain = null;
            }
        }

        // A chain run outside ProcessControl has its conditions asked by what runs it: each frame, at the timing it asks
        // for, Update unless it asks for LateUpdate or FixedUpdate
        private void AskChainConditions(EProcessStepTiming timing)
        {
            if (!ChainRuns || !_chain.HasConditions) return;

            var wanted = _chain.ConditionStepTiming is EProcessStepTiming.LateUpdate or EProcessStepTiming.FixedUpdate
                ? _chain.ConditionStepTiming
                : EProcessStepTiming.Update;
            if (timing == wanted) _chain.CheckConditions();
        }

        private void TogglePause()
        {
            if (ChainRuns)
            {
                _paused = !_paused;
                if (_paused) _chain.Pause();
                else _chain.Resume();
            }
            else if (SequenceRuns)
            {
                // Pausing a sequence's process pauses its run, which holds where it is
                _paused = !_paused;
                if (_paused) _relay.Pause();
                else _relay.Unpause();
            }
            else return;

            ForgeLog.Info(_paused ? $"Paused {Name}." : $"Resumed {Name}.", this);
        }

        private void Stop()
        {
            if (ChainRuns) _chain.Interrupt();
            // At once, whatever its stages allow: Terminate asks the run to stop as an interrupt does, which a stage that
            // refuses injections holds off
            else if (SequenceRuns) _relay.TerminateImmediate();
            else return;

            _paused = false;
            ForgeLog.Info($"Stopped {Name}.", this);
        }
    }
}
