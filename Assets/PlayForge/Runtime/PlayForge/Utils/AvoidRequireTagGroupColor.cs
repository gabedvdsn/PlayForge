using System;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended
{
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