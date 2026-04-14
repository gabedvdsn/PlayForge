using System;

namespace FarEmerald.PlayForge
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ScalerRootAssignment : System.Attribute
    {
        public Type RootType;
        public Type AvoidType;
        public ScalerRootAssignment(Type rootType, Type avoidType = null)
        {
            RootType = rootType;
            AvoidType = avoidType;
        }
    }
}
