using System;

namespace FarEmerald
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ForgeFilterCategory : System.Attribute
    {
        public string[] Categories { get; }

        public ForgeFilterCategory(params string[] categories)
        {
            Categories = categories;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ForgeFilterName : System.Attribute
    {
        public string[] Names { get; }

        public ForgeFilterName(params string[] names)
        {
            Names = names;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
    public sealed class ForgeFilterTag : System.Attribute
    {
        public string[] Tags { get; }

        public ForgeFilterTag(params string[] tags)
        {
            Tags = tags;
        }
    }
}
