using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The end of frame and death of a GameplayAbilitySystem. <see cref="GASCore"/> runs the ActionQueue, FrameSummary
    /// and deferred workers; this ends the entity as a GameObject's GAS ends.
    /// </summary>
    public partial class GameplayAbilitySystem
    {
        public AnalysisWorkerCache GetAnalysisCache() => core?.AnalysisCache;
        public bool IsDead => core != null && core.IsDead;

        /// <summary>
        /// Queue an action for end-of-frame execution.
        /// </summary>
        public void QueueAction(IRootAction action) => core?.QueueAction(action);

        /// <summary>
        /// Queue multiple actions for end-of-frame execution.
        /// </summary>
        public void QueueActions(IEnumerable<IRootAction> actions) => core?.QueueActions(actions);

        /// <summary>
        /// Get the action queue (for advanced usage).
        /// </summary>
        public ActionQueue GetActionQueue() => core?.ActionQueue;

        /// <summary>
        /// Get the current frame summary (for debugging/analysis).
        /// </summary>
        public FrameSummary GetFrameSummary() => core?.FrameSummary;

        /// <summary>
        /// Mark this entity as dead. Does NOT destroy immediately.
        /// The frame will complete normally, then destruction occurs.
        /// </summary>
        public void MarkDead() => core?.MarkDead();

        /// <summary>Brings it back where it stays, as its death says (TW-4).</summary>
        public bool Revive(Vector3? at = null) => core?.Revive(at) ?? false;

        /// <summary>The body it follows, while it has one (TW-4).</summary>
        public AbstractMonoProcess GetBody() => core?.Body;

        /// <summary>
        /// Called after frame is fully complete, once the entity has died.
        /// </summary>
        private void HandleDestruction()
        {
            // As the runtime hub's: one with no process to end is destroyed outright (GAS-42)
            // Through its relay, where it's held (FEAT-10)
            if (Relay != null && Relay.TerminateImmediate()) return;

            Destroy(gameObject);
        }

        /// <summary>
        /// Record an impact this GAS DEALT (this GAS is the ISource).
        /// Call this from the source path after impact is calculated.
        /// </summary>
        public void RecordFrameImpactDealt(ImpactData impact) => core?.RecordImpactDealt(impact);

        /// <summary>
        /// Record an impact this GAS RECEIVED (this GAS is the ITarget).
        /// Call this from ModifyAttribute after impact is calculated.
        /// </summary>
        public void RecordFrameImpactReceived(ImpactData impact) => core?.RecordImpactReceived(impact);

        /// <summary>
        /// Process all deferred work for this frame, then end the entity if it died.
        /// Called from WhenLateUpdate.
        /// </summary>
        private void EndOfFrame()
        {
            if (core.EndOfFrame()) HandleDestruction();
        }
    }
}
