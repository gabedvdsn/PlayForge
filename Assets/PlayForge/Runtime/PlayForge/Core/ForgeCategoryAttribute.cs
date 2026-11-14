using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The group a kind's picker lists it under, as an ability task's: Effects, Process, Sequence (FN-24). A picker whose
    /// kinds name groups lists each group as a submenu, its kinds in name order, and the kinds that name none after them; one
    /// whose kinds name none lists them flat. Its search finds a kind whatever its group. A class's group holds for the
    /// classes made from it, unless one names its own.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class ForgeCategoryAttribute : System.Attribute
    {
        /// <summary>The group's name, as its picker shows it.</summary>
        public string Name { get; }

        public ForgeCategoryAttribute(string name) => Name = name;
    }
}
