using System;

namespace FarEmerald.PlayForge
{
    public enum EValidationCode
    {
        Ok = 0,
        Warn = 1,
        Error = 2,
        None = -1
    }

    /// <summary>
    /// Indicates a category tag for a certain field
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeCategory : System.Attribute
    {
        public string Context;
        public ForgeCategory(string context) => Context = context;
    } 
}
