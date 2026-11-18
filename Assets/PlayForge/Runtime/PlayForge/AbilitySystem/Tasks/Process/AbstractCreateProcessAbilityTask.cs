using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of an ability task that creates processes, as Create Mono Process spawns a projectile: a kind made from
    /// it sits under the Process submenu of a stage's task picker. A GameRoot's <c>CreateProcessTasks</c> take them
    /// too, each run once, with the GameRoot's own data, as the Bootstrapper brings the game up.
    /// </summary>
    [ForgeMenu("Process")]
    public abstract class AbstractCreateProcessAbilityTask : AbstractAbilityTask
    {
        
    }
}
