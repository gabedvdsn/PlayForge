using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>A cue handler, picked by its kind, as a worker is.</summary>
    [CustomPropertyDrawer(typeof(AbstractCueHandler), true)]
    public class CueHandlerDrawer : AbstractGenericDrawer<AbstractCueHandler>
    {
    }
}
