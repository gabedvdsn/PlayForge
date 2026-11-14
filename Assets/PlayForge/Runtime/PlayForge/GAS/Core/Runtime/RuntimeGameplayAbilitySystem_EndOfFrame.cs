using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The end of frame and death of a RuntimeGameplayAbilitySystem. <see cref="GASCore"/> runs the ActionQueue,
    /// FrameSummary and deferred workers; this ends the entity as a runtime GAS ends, with no GameObject to destroy.
    /// </summary>
    public partial class RuntimeGameplayAbilitySystem
    {
        public AnalysisWorkerCache GetAnalysisCache() => core.AnalysisCache;
        public bool IsDead => core.IsDead;

        // ═══════════════════════════════════════════════════════════════════════════
        // ACTION QUEUE ACCESS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Queue an action for end-of-frame execution.
        /// </summary>
        public void QueueAction(IRootAction action) => core.QueueAction(action);

        /// <summary>
        /// Queue multiple actions for end-of-frame execution.
        /// </summary>
        public void QueueActions(IEnumerable<IRootAction> actions) => core.QueueActions(actions);

        /// <summary>
        /// Get the action queue (for advanced usage).
        /// </summary>
        public ActionQueue GetActionQueue() => core.ActionQueue;

        /// <summary>
        /// Get the current frame summary (for debugging/analysis).
        /// </summary>
        public FrameSummary GetFrameSummary() => core.FrameSummary;

        // ═══════════════════════════════════════════════════════════════════════════
        // DEATH HANDLING
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Mark this entity as dead. Does NOT destroy immediately.
        /// The frame will complete normally, then destruction occurs.
        /// </summary>
        public void MarkDead() => core.MarkDead();

        /// <summary>Brings it back where it stays, as its death says (TW-4).</summary>
        public bool Revive(UnityEngine.Vector3? at = null) => core.Revive(at);

        /// <summary>The body it follows, while it has one (TW-4).</summary>
        public AbstractMonoProcess GetBody() => core.Body;

        /// <summary>Gives it a body of its own at runtime, as <see cref="IGameplayAbilitySystem.UseBody"/> says (FN-18).</summary>
        public bool UseBody(AbstractMonoProcess body) => core.UseBody(body);

        /// <summary>
        /// Called after frame is fully complete, once the entity has died. Terminates the process via ProcessControl.
        /// Unlike the MonoBehaviour version, there is no GameObject to destroy.
        /// </summary>
        protected virtual void HandleDestruction()
        {
            // One stepped by hand, outside ProcessControl, has no process to end. It threw (GAS-42). Through its relay, it
            // ends where it's held: through the scene's control by its cache index, one in a world of its own ended
            // whatever the scene held under that index, and ran on itself (FEAT-10)
            Relay?.Terminate();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // IMPACT RECORDING
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Record an impact this GAS DEALT (this GAS is the ISource).
        /// Call this from the source path after impact is calculated.
        /// </summary>
        public void RecordFrameImpactDealt(ImpactData impact) => core.RecordImpactDealt(impact);

        /// <summary>
        /// Record an impact this GAS RECEIVED (this GAS is the ITarget).
        /// Call this from ModifyAttribute after impact is calculated.
        /// </summary>
        public void RecordFrameImpactReceived(ImpactData impact) => core.RecordImpactReceived(impact);

        // ═══════════════════════════════════════════════════════════════════════════
        // END-OF-FRAME PROCESSING
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Process all deferred work for this frame, then end the entity if it died.
        /// Called from WhenLateUpdate via the process system, or by whoever steps this GAS by hand.
        /// </summary>
        public void EndOfFrame()
        {
            if (core.EndOfFrame()) HandleDestruction();
        }
    }
}
