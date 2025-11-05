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
    
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeValidation : System.Attribute
    {
        public EValidationCode PassWhenFail;

        public ForgeValidation(EValidationCode passWhenFail)
        {
            PassWhenFail = passWhenFail;
        }
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

    /// <summary>
    /// Indicates ref fields
    /// </summary>
    public sealed class ForgeDataType : System.Attribute
    {
        public EDataType Kind;
        public ForgeDataType(EDataType kind) => Kind = kind;
    }
    
    /// <summary>
    /// Overwrites the display name for a field in the Forge editor
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeLabelAttribute : System.Attribute
    {
        public string Label;
        public ForgeLabelAttribute(string label) => Label = label;
    }

    

    /// <summary>
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeLinkField : System.Attribute
    {
        public string link;
        public ForgeLinkField(string link)
        {
            this.link = link;
        }
    }
    
    /// <summary>
    /// Links a Tag field value to another field
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeLinkTag : System.Attribute
    {
        public string link;
        public ForgeLinkTag(string link)
        {
            this.link = link;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeCompositeLabel : System.Attribute
    {
        public string Label;
        public ForgeCompositeLabel(string label)
        {
            Label = label;
        }
    }

    /// <summary>
    /// Indicates what order this field should appear in the editor
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeOrderAttribute : System.Attribute
    {
        public int Order;
        public ForgeOrderAttribute(int order) => Order = order;
    }

    /// <summary>
    /// Indicates a field that should not appear in the editor
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForgeHiddenAttribute : System.Attribute
    {
    } // hide in editor
}
