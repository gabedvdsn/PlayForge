using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// How a unit the author controls is ordered about (LAB-11, Decision 44): moved to a point, set on a unit to attack,
    /// made to cast, stopped. PlayForge has no movement of its own, so a project whose units move and cast their own way
    /// gives its own driver, on its Lab Rules, and the lab's controls and the Demo's give their orders through it. Without
    /// one, the lab's: a unit walks in a straight line and casts as the lab's units do.
    /// </summary>
    public interface ILabUnitDriver
    {
        /// <summary>Sends the unit walking to a point.</summary>
        void MoveTo(LabUnit unit, Vector3 point);

        /// <summary>Sets the unit on another, to walk into its range and attack it.</summary>
        void Attack(LabUnit unit, LabUnit target);

        /// <summary>Has the unit cast an ability, with its activation's data, or none, so its own targeting asks; whether it started.</summary>
        bool Cast(LabUnit unit, Ability ability, ProcessDataPacket data);

        /// <summary>Stops the unit: where it walks, and whom it's set on.</summary>
        void Stop(LabUnit unit);
    }

    /// <summary>
    /// A project's driver as an asset (LAB-11): each order a virtual that does as the lab's does, so a project overrides
    /// those its game gives its own way, and names the asset on its Lab Rules. Create → PlayForge → Lab Driver.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Lab Driver", fileName = "Lab Driver")]
    public class LabDriver : ScriptableObject, ILabUnitDriver
    {
        public virtual void MoveTo(LabUnit unit, Vector3 point) => LabBodyDriver.Instance.MoveTo(unit, point);

        public virtual void Attack(LabUnit unit, LabUnit target) => LabBodyDriver.Instance.Attack(unit, target);

        public virtual bool Cast(LabUnit unit, Ability ability, ProcessDataPacket data) => LabBodyDriver.Instance.Cast(unit, ability, data);

        public virtual void Stop(LabUnit unit) => LabBodyDriver.Instance.Stop(unit);
    }

    /// <summary>The lab's own driver (LAB-11): its units walk in straight lines, set on a target, and cast as the lab's do.</summary>
    public sealed class LabBodyDriver : ILabUnitDriver
    {
        public static readonly LabBodyDriver Instance = new();

        private LabBodyDriver() { }

        public void MoveTo(LabUnit unit, Vector3 point)
        {
            if (unit) unit.MoveTo(point);
        }

        public void Attack(LabUnit unit, LabUnit target)
        {
            if (unit && target) unit.AttackUnit(target);
        }

        public bool Cast(LabUnit unit, Ability ability, ProcessDataPacket data) => unit && ability && unit.Cast(ability, data);

        public void Stop(LabUnit unit)
        {
            if (unit) unit.Stop();
        }
    }
}
