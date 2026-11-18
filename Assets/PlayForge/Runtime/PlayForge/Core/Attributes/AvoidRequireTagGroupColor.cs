using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Marks an <c>AvoidRequireTagGroup</c> field with a color, given as red, green, blue and alpha from 0 to 1, as the
    /// tag requirements of abilities and effects mark their groups. The package's own drawers don't read it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class AvoidRequireTagGroupColor : System.Attribute
    {
        public Color Color;
        public AvoidRequireTagGroupColor(float r, float g, float b, float a)
        {
            Color = new Color(r, g, b, a);
        }
    }

}