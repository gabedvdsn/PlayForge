using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Specifies a keyword that customizes labels in ScalerMagnitudeOperation 
    /// and ScalerIntegerMagnitudeOperation property drawers.
    /// 
    /// The keyword is used to:
    /// - Set the magnitude label to "Base {Keyword}" or "Additional {Keyword}"
    /// - Set the operation label to "Real {Keyword}" or "Use {Keyword}"
    /// - Set the scaler label to "{Keyword} Scaler" (via DeriveScalerName)
    /// </summary>
    /// <example>
    /// [ScalerOperationKeyword("Magnitude")]
    /// public ScalerMagnitudeOperation MagnitudeScaling;
    /// // Shows: "Base Magnitude", "Real Magnitude", "Magnitude Scaler"
    /// 
    /// [ScalerOperationKeyword("Duration")]
    /// public ScalerMagnitudeOperation DurationScaling;
    /// // Shows: "Base Duration", "Real Duration", "Duration Scaler"
    /// 
    /// [ScalerOperationKeyword("Execute Ticks")]
    /// public ScalerIntegerMagnitudeOperation ExecuteTicksScaling;
    /// // Shows: "Additional Execute Ticks", "Use Execute Ticks", "Execute Ticks Scaler"
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public class ScalerOperationKeyword : System.Attribute
    {
        public string Keyword { get; }
        
        public ScalerOperationKeyword(string keyword)
        {
            Keyword = keyword;
        }
    }

    /// <summary>
    /// When placed on a Scaler field inside a ScalerMagnitudeOperation,
    /// the scaler drawer will look up the parent property's ScalerOperationKeyword
    /// attribute and use "{Keyword} Scaler" as the display name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DeriveScalerName : System.Attribute { }
    
    /// <summary>
    /// Explicitly sets the display name for a Scaler field.
    /// Takes precedence over DeriveScalerName.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ScalerDisplayName : System.Attribute
    {
        public string Name { get; }
        
        public ScalerDisplayName(string name)
        {
            Name = name;
        }
    }
}