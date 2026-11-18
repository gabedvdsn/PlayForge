using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Put on a scaler field, it narrows the kinds the field's Kind menu offers: those deriving from <c>RootType</c>
    /// alone, where it names one, and none deriving from <c>AvoidType</c>, where it names one. An attribute set
    /// element's sides take the cached kinds alone so, and a number field every kind but those.
    /// </summary>
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
