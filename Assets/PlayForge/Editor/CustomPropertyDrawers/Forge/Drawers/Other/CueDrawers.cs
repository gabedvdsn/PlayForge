using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>A cue handler, picked by its kind, as a worker is (FEAT-1).</summary>
    [CustomPropertyDrawer(typeof(AbstractCueHandler), true)]
    public class CueHandlerDrawer : AbstractGenericDrawer<AbstractCueHandler>
    {
    }
}
