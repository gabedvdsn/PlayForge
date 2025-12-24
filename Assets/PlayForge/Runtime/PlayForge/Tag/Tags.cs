using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace FarEmerald.PlayForge
{
    public static class Tags
    {
        private const int _NULL = -1;
        public static Tag NULL => Tag.Generate("NULL");
        
        #region Affiliation Tags
        
        /*
         * Affiliation tags are used to indicate affiliations between GAS components.
         */
        
        private const int _AFFILIATION_ROOT = -100_000_000;
        public static Tag AFFILIATION_ROOT => Tag.Generate(_AFFILIATION_ROOT);
        
        private const int _AFFILIATION_NONE = -100_000_001;
        public static Tag AFFILIATION_NONE => Tag.Generate(_AFFILIATION_NONE);
        
        #endregion
        
        #region Payload Tags
        
        /*
         * Payload tags are used in data packets to associate data with their cast-types and use case.
         */
        
        #region Ability Packets
        
        private const int _PAYLOAD_GAS = -200_000_000;
        public static Tag PAYLOAD_GAS => Tag.Generate(_PAYLOAD_GAS);
        
        private const int _PAYLOAD_TRANSFORM = -200_000_001;
        public static Tag PAYLOAD_TRANSFORM => Tag.Generate(_PAYLOAD_TRANSFORM);
        
        private const int _PAYLOAD_POSITION = -200_000_002;
        public static Tag PAYLOAD_POSITION => Tag.Generate(_PAYLOAD_POSITION);
        
        private const int _PAYLOAD_ROTATION = -200_000_003;
        public static Tag PAYLOAD_ROTATION => Tag.Generate(_PAYLOAD_ROTATION);
        
        private const int _PAYLOAD_DERIVATION = -200_000_004;
        public static Tag PAYLOAD_DERIVATION => Tag.Generate(_PAYLOAD_DERIVATION);
        
        private const int _PAYLOAD_AFFILIATION = -200_000_005;
        public static Tag PAYLOAD_AFFILIATION => Tag.Generate(_PAYLOAD_AFFILIATION);
        
        private const int _PAYLOAD_SOURCE = -200_000_006;
        public static Tag PAYLOAD_SOURCE => Tag.Generate(_PAYLOAD_SOURCE);
        
        private const int _PAYLOAD_TARGET = -200_000_007;
        public static Tag PAYLOAD_TARGET => Tag.Generate(_PAYLOAD_TARGET);
        
        private const int _PAYLOAD_DATA = -200_000_008;
        public static Tag PAYLOAD_DATA => Tag.Generate(_PAYLOAD_DATA);
        
        #endregion
        
        #region GAS Store (Coffer)
        
        private const int _STORE_DISJOINTABLE = -200_100_000;
        public static Tag TARGETED_INTENT => Tag.Generate(_STORE_DISJOINTABLE);
        
        #endregion
        
        #endregion
        
        #region Effect Tags
        
        #region Attribute Impact Retention
        
        /// <summary>
        /// Empty
        /// </summary>
        private const int _RETENTION_IGNORE = -400_000_000;
        public static Tag RETENTION_IGNORE => Tag.Generate(_RETENTION_IGNORE);
        
        /// <summary>
        /// Attribute retention level for low-level cached attribute values deriving from set & modifier declarations.
        /// E.g. initial values, attribute backed values
        /// </summary>
        private const int _RETENTION_DECLARED = -400_000_001;
        public static Tag RETENTION_DECLARED => Tag.Generate(_RETENTION_DECLARED);
        
        /// <summary>
        /// Attribute retention level for mid-level cached attribute values deriving from pseudo-permanent bonuses.
        /// E.g. items
        /// </summary>
        private const int _RETENTION_BONUS = -400_000_002;
        public static Tag RETENTION_BONUS => Tag.Generate(_RETENTION_BONUS);
        
        #endregion
        
        #region Effect Timing

        private const int _TICK_RATE_DEFAULT = -400_100_000;
        public static Tag TICK_RATE_DEFAULT => Tag.Generate(_TICK_RATE_DEFAULT);
        private const int _DELTA_TIME_DEFAULT = -400_100_001;
        public static Tag DELTA_TIME_DEFAULT => Tag.Generate(_DELTA_TIME_DEFAULT);
        
        #endregion
        
        #endregion
        
        #region Context Tags
        
        private const int _CONTEXT_GAS = -500_000_000;
        public static Tag CONTEXT_GAS => Tag.Generate(_CONTEXT_GAS);
        private const int _CONTEXT_SOURCE = -500_000_001;
        public static Tag CONTEXT_SOURCE => Tag.Generate(_CONTEXT_SOURCE); 
        
        #endregion
        
        #region General

        private const int _GENERAL_NA = -600_000_000;
        public static Tag GEN_NOT_APPLICABLE => Tag.Generate(_GENERAL_NA);
        
        private const int _GENERAL_ANY = -600_000_001;
        public static Tag GEN_ANY => Tag.Generate(_GENERAL_NA);
        
        #endregion

        public static class Category
        {
            private const int _CAT_IMPACT_TYPE = -700_000_000;
            public static Tag CAT_IMPACT_TYPE => Tag.Generate(_CAT_IMPACT_TYPE);
            
            private const int _CAT_IMPACT_MAGNITUDE = -700_000_000;
            public static Tag CAT_IMPACT_MAGNITUDE => Tag.Generate(_CAT_IMPACT_MAGNITUDE);
            
            private const int _CAT_IMPACT_ATTRIBUTE = -700_000_000;
            public static Tag CAT_IMPACT_ATTRIBUTE => Tag.Generate(_CAT_IMPACT_ATTRIBUTE);
        }
        
    }
}
