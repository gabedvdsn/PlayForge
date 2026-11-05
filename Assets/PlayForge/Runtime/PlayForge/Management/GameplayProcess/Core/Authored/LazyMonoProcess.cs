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
        public override void WhenUpdate()
        {
            
        }

        public override async UniTask RunProcess(CancellationToken token)
        {
            processActive = true;
            // Kept alive tick by tick: in a world of its own, its steps; elsewhere, Unity's frames, as the wait was (FEAT-10)
            while (processActive) await ForgeTick.Next(token);
        }
    }
}
