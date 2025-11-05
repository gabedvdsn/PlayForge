using System;

namespace FarEmerald
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ForgeCategory : System.Attribute
    {
        public string Context { get; }

        public ForgeCategory(string context)
        {
            Context = context;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ForgeFilterName : System.Attribute
    {
        public string Name { get; }

        public ForgeFilterName(string name)
        {
            Name = name;
        }
    }
}
