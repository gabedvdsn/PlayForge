using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace FarEmerald.PlayForge
{
    public static class Tags
    {
        private static Dictionary<string, int> Library;
        private static HashSet<int> used;

        private const int _NULL = -1;
        public static Tag NULL => Tag.Generate("NULL");

        public static void Initialize()
        {
            Library = new Dictionary<string, int>();
            used = new HashSet<int>();

            RegisterDefined("NULL", _NULL);
            
            RegisterDefined();
        }

        #region Registration
        
        private static int nextFree = 0;
        private static int last = 0;
        private static string last_name = "";
        
        public static bool Register(string name)
        {
            if (Library.ContainsKey(name)) return false;
            
            return Register(name, nextFree++);
        }
        
        public static bool Register(string name, int key)
        {
            int _key = key;
            while (used.Contains(_key)) _key += 1;
            
            if (_key >= nextFree) nextFree = _key + 1;
            
            last = _key;
            last_name = name;

            Library[name] = _key;
            used.Add(_key);

            return true;
        }
        
        private static int c_nextFree = 0;
        private static int c_last = 0;
        private static string c_last_name = "";
        
        public static void RegisterDefined(IEnumerable<string> names)
        {
            foreach (string name in names) RegisterDefined(name);
        }

        private static void RegisterDefined(int key)
        {
            if (used.Contains(key)) return;
            
            RegisterDefined($"DEF_{key}");
        }
        
        private static void RegisterDefined(string name)
        {
            if (Library.ContainsKey(name)) return;
            
            RegisterDefined(name, c_nextFree);   
        }

        private static void RegisterDefined(string name, int key)
        {
            int _key = key;
            while (used.Contains(_key)) _key -= 1;
            
            if (_key <= c_nextFree) c_nextFree = _key - 1;
            
            c_last = _key;
            c_last_name = name;

            Library[name] = _key;
            used.Add(_key);
        }
        
        #endregion
        
        #region Open
        
        public static Tag Get(string name)
        {
            return Tag.Generate(Library[name], name);
        }

        public static bool TryGet(string name, out Tag tag)
        {
            if (Library.TryGetValue(name, out int key))
            {
                tag = Tag.Generate(key, name);
                return true;
            }

            tag = default;
            return false;
        }

        public static IEnumerable<Tag> All()
        {
            return Library.Keys.Select(name => Tag.Generate(Library[name], name));
        }
        
        #endregion
        
        #region Closed

        private static Tag Get(int key, string name = "")
        {
            return Tag.Generate(key, name);
        }

        #region System Defined

        private static void RegisterDefined()
        {
            RegisterDefined(_AFFILIATION_ROOT);
            
            RegisterDefined(_PAYLOAD_GAS);
            RegisterDefined(_PAYLOAD_TRANSFORM);
            RegisterDefined(_PAYLOAD_POSITION);
            RegisterDefined(_PAYLOAD_ROTATION);
            RegisterDefined(_PAYLOAD_DERIVATION);
            RegisterDefined(_PAYLOAD_AFFILIATION);
            RegisterDefined(_PAYLOAD_SOURCE);
            RegisterDefined(_PAYLOAD_TARGET);
            RegisterDefined(_PAYLOAD_DATA);
            
            RegisterDefined(_STORE_DISJOINTABLE);
            
            RegisterDefined(_RETENTION_IGNORE);
            RegisterDefined(_RETENTION_DECLARED);
            RegisterDefined(_RETENTION_BONUS);
            
            RegisterDefined(_TICK_RATE_DEFAULT);
            RegisterDefined(_DELTA_TIME_DEFAULT);
            
            RegisterDefined(_CONTEXT_GAS);
            RegisterDefined(_CONTEXT_SOURCE);
            
            
        }
        
        #region Affiliation Tags
        
        /*
         * Affiliation tags are used to indicate affiliations between GAS components.
         */
        
        private const int _AFFILIATION_ROOT = -100_000_000;
        public static Tag AFFILIATION_ROOT => Get(_AFFILIATION_ROOT);
        
        private const int _AFFILIATION_NONE = -100_000_000;
        public static Tag AFFILIATION_NONE => Get(_AFFILIATION_NONE);
        
        #endregion
        
        #region Payload Tags
        
        /*
         * Payload tags are used in data packets to associate data with their cast-types and use case.
         */
        
        #region Ability Packets
        
        private const int _PAYLOAD_GAS = -200_000_000;
        public static Tag PAYLOAD_GAS => Get(_PAYLOAD_GAS);
        
        private const int _PAYLOAD_TRANSFORM = -200_000_001;
        public static Tag PAYLOAD_TRANSFORM => Get(_PAYLOAD_TRANSFORM);
        
        private const int _PAYLOAD_POSITION = -200_000_002;
        public static Tag PAYLOAD_POSITION => Get(_PAYLOAD_POSITION);
        
        private const int _PAYLOAD_ROTATION = -200_000_003;
        public static Tag PAYLOAD_ROTATION => Get(_PAYLOAD_ROTATION);
        
        private const int _PAYLOAD_DERIVATION = -200_000_004;
        public static Tag PAYLOAD_DERIVATION => Get(_PAYLOAD_DERIVATION);
        
        private const int _PAYLOAD_AFFILIATION = -200_000_005;
        public static Tag PAYLOAD_AFFILIATION => Get(_PAYLOAD_AFFILIATION);
        
        private const int _PAYLOAD_SOURCE = -200_000_006;
        public static Tag PAYLOAD_SOURCE => Get(_PAYLOAD_SOURCE);
        
        private const int _PAYLOAD_TARGET = -200_000_007;
        public static Tag PAYLOAD_TARGET => Get(_PAYLOAD_TARGET);
        
        private const int _PAYLOAD_DATA = -200_000_008;
        public static Tag PAYLOAD_DATA => Get(_PAYLOAD_DATA);
        
        #endregion
        
        #region GAS Store (Coffer)
        
        private const int _STORE_DISJOINTABLE = -200_100_000;
        public static Tag TARGETED_INTENT => Get(_STORE_DISJOINTABLE);
        
        #endregion
        
        #endregion
        
        #region Ability Tags

        #region Injections
        
        // Cancel the ability runtime entirely
        private const int _INJECT_INTERRUPT = -300_000_000;
        public static Tag INJECT_INTERRUPT => Get(_AFFILIATION_ROOT);
        
        // Cancel the active proxy stage runtime and moves to the next
        private const int _INJECT_BREAK_STAGE = -300_000_001;
        public static Tag INJECT_BREAK_STAGE => Get(_AFFILIATION_ROOT);
        
        // Same as BREAK_STAGE BUT the active proxy stage runtime CONTINUES until a STOP_MAINTAIN/_ALL injection, or the runtime reaches its natural conclusion
        private const int _INJECT_MAINTAIN_STAGE = -300_000_002;
        public static Tag INJECT_MAINTAIN_STAGE => Get(_AFFILIATION_ROOT);
        
        // Cancels the least recent maintained proxy stage runtime
        private const int _INJECT_STOP_MAINTAIN = -300_000_003;
        public static Tag INJECT_STOP_MAINTAIN => Get(_AFFILIATION_ROOT);
        
        // Cancels all maintained proxy stage runtimes
        private const int _INJECT_STOP_MAINTAIN_ALL = -300_000_004;
        public static Tag INJECT_STOP_MAINTAIN_ALL => Get(_AFFILIATION_ROOT);
        
        #endregion
        
        #endregion
        
        #region Effect Tags
        
        #region Attribute Impact Retention
        
        /// <summary>
        /// Empty
        /// </summary>
        private const int _RETENTION_IGNORE = -400_000_000;
        public static Tag RETENTION_IGNORE => Get(_RETENTION_IGNORE);
        
        /// <summary>
        /// Attribute retention level for low-level cached attribute values deriving from set & modifier declarations.
        /// E.g. initial values, attribute backed values
        /// </summary>
        private const int _RETENTION_DECLARED = -400_000_001;
        public static Tag RETENTION_DECLARED => Get(_RETENTION_DECLARED);
        
        /// <summary>
        /// Attribute retention level for mid-level cached attribute values deriving from pseudo-permanent bonuses.
        /// E.g. items
        /// </summary>
        private const int _RETENTION_BONUS = -400_000_002;
        public static Tag RETENTION_BONUS => Get(_RETENTION_BONUS);
        
        #endregion
        
        #region Effect Timing

        private const int _TICK_RATE_DEFAULT = -400_100_000;
        public static Tag TICK_RATE_DEFAULT => Get(_TICK_RATE_DEFAULT);
        private const int _DELTA_TIME_DEFAULT = -400_100_000;
        public static Tag DELTA_TIME_DEFAULT => Get(_DELTA_TIME_DEFAULT);
        
        #endregion
        
        #endregion
        
        #region Context Tags
        
        private const int _CONTEXT_GAS = -500_000_000;
        public static Tag CONTEXT_GAS => Get(_CONTEXT_GAS);
        private const int _CONTEXT_SOURCE = -500_000_001;
        public static Tag CONTEXT_SOURCE => Get(_CONTEXT_SOURCE); 
        
        #endregion
        
        #region General

        private const int _GENERAL_NA = -600_000_000;
        public static Tag GEN_NOT_APPLICABLE => Get(_GENERAL_NA);
        
        private const int _GENERAL_ANY = -600_000_000;
        public static Tag GEN_ANY => Get(_GENERAL_NA);
        
        #endregion

        #endregion

        #endregion
    }
}
