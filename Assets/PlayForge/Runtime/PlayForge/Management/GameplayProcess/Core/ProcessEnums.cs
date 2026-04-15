using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public enum EProcessStepTiming
    {
        None = 0,
        Update = 1,
        LateUpdate = 2,
        FixedUpdate = 3,
        
        UpdateAndLate = 4,
        UpdateAndFixed = 5,
        LateAndFixed = 6,
        UpdateFixedAndLate = 7
    }

    public enum EProcessLifecycle
    {
        SelfTerminating,
        RunThenWait,
        RequiresControl,
        
        /// <summary>
        /// No async-related overhead. Only attaches to update cycle(s) as indicated by process step timing.
        /// </summary>
        Synchronous
    }
    
    public enum EProcessStepPriorityMethod
    {
        Manual,
        First,
        Last
    }
}
