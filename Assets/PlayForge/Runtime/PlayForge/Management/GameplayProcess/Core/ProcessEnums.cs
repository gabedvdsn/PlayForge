using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Which of Unity's loops a process is stepped in while it runs, each step calling its <c>WhenUpdate</c>,
    /// <c>WhenLateUpdate</c> or <c>WhenFixedUpdate</c>: one of Update, LateUpdate and FixedUpdate, two of them, all
    /// three, or <c>None</c>, never stepped. A GAS hub steps in Update and LateUpdate: Update ticks its effects, and
    /// LateUpdate runs its end of frame. Under ProcessControl's Fixed Tick Rate, each tick steps the Update and
    /// LateUpdate processes.
    /// </summary>
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

    /// <summary>
    /// How a process starts and what follows its run: <c>SelfTerminating</c> starts running and ends as its run is
    /// done, as a projectile; <c>RunThenWait</c> starts running and waits when its run is done, to run again, as an
    /// ability; <c>RequiresControl</c> starts waiting and runs only when told, as an ability on a key; and
    /// <c>Synchronous</c> starts running with no run to await, stepped at its timing for as long as it lasts, as a
    /// health bar.
    /// </summary>
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
    
    /// <summary>
    /// Where a process steps among the others of its timing: <c>First</c>, at priority 0; <c>Last</c>, after every
    /// other; or <c>Manual</c>, at its own step priority. Lower priorities step first, and ProcessControl registers a
    /// scene's own processes by it too, parents before their children.
    /// </summary>
    public enum EProcessStepPriorityMethod
    {
        Manual,
        First,
        Last
    }
}
