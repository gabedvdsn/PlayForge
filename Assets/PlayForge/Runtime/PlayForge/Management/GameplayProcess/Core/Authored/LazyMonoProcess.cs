using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The Lazy process is a process (typically self-terminating) that runs until Waited or Terminated.
    /// </summary>
    public class LazyMonoProcess : AbstractMonoProcess
    {
        public override void WhenUpdate(ProcessRelay relay)
        {
            
        }

        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            processActive = true;
            await UniTask.WaitWhile(() => processActive, cancellationToken: token);
        }

        public override UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }


        public override void RunCompositeBehaviour(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller)
        {
            
        }
        public override UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
    }
}
