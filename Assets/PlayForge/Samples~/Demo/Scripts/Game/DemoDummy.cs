using FarEmerald.PlayForge.Lab;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// The training dummy: the Creator Lab's test dummy (LAB-7), which stands and counts what it takes and mends after a
    /// few seconds left alone, by the roster's mend effect. It never dies, since no death watcher is in its worker group.
    /// The panel shows its damage per second and the last blow, colored by its damage type, which this keeps.
    /// </summary>
    public class DemoDummy : LabDummy
    {
        public static DemoDummy Current { get; private set; }

        /// <summary>The damage type of the last blow, by which the panel colors it.</summary>
        public Tag LastKind { get; private set; }

        private void Awake()
        {
            Current = this;
            Took += Kept;
        }

        protected override void OnDestroy()
        {
            Took -= Kept;
            if (Current == this) Current = null;
            base.OnDestroy();
        }

        private void Kept(LabDummy dummy, LabBlow blow)
        {
            var roster = (Unit as DemoUnit)?.Roster;
            if (roster != null) LastKind = DemoHud.KindOf(roster, blow.Types);
        }
    }
}
