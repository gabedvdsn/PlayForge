using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;

namespace FarEmerald.PlayForge
{
    public static partial class Forge
    {
        #region Contexts

        public static class Categories
        {
            /// <summary>
            /// AssetTag or primary identifier tag context
            /// </summary>
            public const string Identifier = "GC_IDENTIFIER";
            
            /// <summary>
            /// Asset gameplay tag context
            /// </summary>
            public const string Context = "GC_CONTEXT";
            
            /// <summary>
            /// Attribute impact type tag
            /// </summary>
            public const string ImpactType = "GC_IMPACT_TYPE";

            public const string Cost = "GC_COST";
            public const string Cooldown = "GC_Cooldown";

            public const string Visibility = "GC_VISIBILITY";
        }
        
        #endregion
    }
}
