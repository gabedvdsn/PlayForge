using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public static class AttributeLibrary
    {
        private static readonly Dictionary<string, Attribute> Library = new();

        /// <summary>
        /// REFACTOR THIS TO REFLECT YOUR ATTRIBUTE NAMING CONVENTION
        ///
        /// With respect to source code:
        /// An Attribute's 'Name' field should be written using logical spaces (as opposed to pascal or camel)
        ///     E.g. "Magic Resistance", "Attack Speed" (whereas the name of the SO object can be anything)
        ///     cont. these will refactor the names into "MAGIC_RESISTANCE" and "ATTACK_SPEED"
        ///     cont. these attributes can be requested using 'AttributeLibrary.GetByName('ATTACK_SPEED'
        /// This method replaces capitalizes and replaces spaces with underscores.
        /// </summary>
        /// <param name="attr">The stored name of the attribute</param>
        /// <returns></returns>
        public static string RefactorByNamingConvention(string attr)
        {
            string _name = attr.ToUpper();  // Capitalize
            _name = _name.Replace(' ', '_');  // Replace spaces with underscores
            return _name;
        }
        
        #region Internal
        
        public static bool Contains(Attribute attribute) => Contains(attribute.GetName());
        public static bool Contains(string attrName) => Library.ContainsKey(RefactorByNamingConvention(attrName));
        
        public static bool Add(Attribute attribute)
        {
            string _name = RefactorByNamingConvention(attribute.GetName());
            if (Library.ContainsKey(_name)) return false;
            
            Library[_name] = attribute;
            return true;
        }

        public static bool TryGetByName(string attrName, out Attribute attribute)
        {
            string _name = RefactorByNamingConvention(attrName);
            if (!Contains(_name))
            {
                attribute = default;
                return false;
            }

            attribute = Library[_name];
            return true;
        }

        public static Attribute GetByName(string attrName)
        {
            return TryGetByName(attrName, out var attr) ? attr : default;
        }
        
        #endregion
    }
}
