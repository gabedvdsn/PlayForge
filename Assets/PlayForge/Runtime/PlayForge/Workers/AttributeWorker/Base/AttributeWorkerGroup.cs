using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class AttributeWorkerGroup : AbstractAttributeWorker
    {
        [SerializeReference]
        public List<AbstractAttributeWorker> ChangeEvents = new();

        public override void Intercept(WorkerContext ctx)
        {
            foreach (var fEvent in ChangeEvents)
            {
                if (!fEvent.PreValidateWorkFor(ctx.Change)) continue;
                if (!fEvent.ValidateWorkFor(ctx)) continue;

                fEvent.Intercept(ctx);
            }
        }
        public override IEnumerable<IRootAction> DeferredIntercept(WorkerContext ctx)
        {
            var actions = new List<IRootAction>();
            foreach (var fEvent in ChangeEvents) actions.AddRange(fEvent.DeferredIntercept(ctx));
            return actions;
        }

        public override EWorkerExecution Execution { get; }
        public override EChangeEventTiming Timing { get; }
        public override bool PreValidateWorkFor(ChangeValue change)
        {
            return ChangeEvents.Count > 0;
        }
        public override bool ValidateWorkFor(WorkerContext ctx)
        {
            return ChangeEvents.Count > 0;
        }

        public override bool RegisterWithHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange)
        {
            return ChangeEvents.Any(changeEvent => changeEvent.RegisterWithHandler(preChange, postChange));
        }
        public override bool DeRegisterFromHandler(AttributeChangeMomentHandler preChange, AttributeChangeMomentHandler postChange)
        {
            return ChangeEvents.Any(changeEvent => changeEvent.DeRegisterFromHandler(preChange, postChange));
        }
    }
}
