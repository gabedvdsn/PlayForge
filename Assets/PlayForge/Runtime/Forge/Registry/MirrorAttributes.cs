using System;


namespace FarEmerald
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class MirrorFromAttribute : System.Attribute
    {
        public Type SourceType { get; }
        public MirrorFromAttribute(Type sourceType) => SourceType = sourceType;
    }

    /// Exclude specific fields by exact name.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MirrorIgnoreAttribute : System.Attribute
    {
        public string FieldName { get; }
        public MirrorIgnoreAttribute(string fieldName) => FieldName = fieldName;
    }

    /// Substitute a concrete source type with a concrete target type.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MirrorSubstituteAttribute : System.Attribute
    {
        public Type From { get; }
        public Type To { get; }
        public MirrorSubstituteAttribute(Type from, Type to) { From = from; To = to; }
    }

    /// Substitute ANY type assignable to SourceBase with an open generic target, e.g. AssetRef<T>.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MirrorSubstituteOpenGenericAttribute : System.Attribute
    {
        public Type SourceBase { get; }
        public Type OpenGenericTarget { get; } // e.g. typeof(AssetRef<>)
        public MirrorSubstituteOpenGenericAttribute(Type sourceBase, Type openGenericTarget)
        {
            SourceBase = sourceBase; OpenGenericTarget = openGenericTarget;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class MirrorRenameAttribute : System.Attribute
    {
        public string From { get; }
        public string To { get; }
        public MirrorRenameAttribute(string from, string to)
        {
            From = from; To = to;
        }
    }
}
